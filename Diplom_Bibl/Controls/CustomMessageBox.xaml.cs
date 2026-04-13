using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplom_Bibl.Controls
{
    // Убедитесь, что используются эти enum, а не системные
    public enum CustomMessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
        AbortRetryIgnore
    }

    public enum CustomMessageBoxImage
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }

    public enum CustomMessageBoxResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 6,
        No = 7,
        Abort = 3,
        Retry = 4,
        Ignore = 5
    }

    public partial class CustomMessageBox : Window
    {
        private CustomMessageBoxResult _result = CustomMessageBoxResult.None;

        // Объявляем без "?", так как C# 7.3
        private Image _messageIcon;
        private TextBlock _messageText;
        private StackPanel _buttonsPanel;

        // Временное хранение данных до загрузки шаблона
        private string _pendingMessage;
        private CustomMessageBoxImage _pendingIcon;
        private CustomMessageBoxButton _pendingButtons;

        public CustomMessageBox()
        {
            InitializeComponent();
            this.Loaded += CustomMessageBox_Loaded;
        }

        private void CustomMessageBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Получаем элементы из шаблона
            _messageIcon = GetTemplateChild("MessageIcon") as Image;
            _messageText = GetTemplateChild("MessageText") as TextBlock;
            _buttonsPanel = GetTemplateChild("ButtonsPanel") as StackPanel;

            // Если данные были переданы до загрузки, применяем их сейчас
            if (_messageText != null && !string.IsNullOrEmpty(_pendingMessage))
            {
                _messageText.Text = _pendingMessage;
            }

            ApplyIcon(_pendingIcon);
            CreateButtons(_pendingButtons);
        }

        public static CustomMessageBoxResult Show(string message)
        {
            return ShowInternal(message, string.Empty, CustomMessageBoxButton.OK, CustomMessageBoxImage.None, null);
        }

        public static CustomMessageBoxResult Show(string message, string caption)
        {
            return ShowInternal(message, caption, CustomMessageBoxButton.OK, CustomMessageBoxImage.None, null);
        }

        public static CustomMessageBoxResult Show(string message, string caption, CustomMessageBoxButton button)
        {
            return ShowInternal(message, caption, button, CustomMessageBoxImage.None, null);
        }

        public static CustomMessageBoxResult Show(string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            return ShowInternal(message, caption, button, icon, null);
        }

        public static CustomMessageBoxResult Show(Window owner, string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            return ShowInternal(message, caption, button, icon, owner);
        }

        private static CustomMessageBoxResult ShowInternal(string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon, Window owner)
        {
            var messageBox = new CustomMessageBox
            {
                Title = string.IsNullOrEmpty(caption) ? "Сообщение" : caption,
                _pendingMessage = message,
                _pendingIcon = icon,
                _pendingButtons = button
            };

            if (owner != null)
            {
                messageBox.Owner = owner;
                messageBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                messageBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            messageBox.ShowDialog();
            return messageBox._result;
        }

        private void ApplyIcon(CustomMessageBoxImage icon)
        {
            if (_messageIcon == null) return;

            _messageIcon.Visibility = Visibility.Collapsed;

            if (icon == CustomMessageBoxImage.None)
                return;

            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                var fillColor = Colors.Gray;
                var symbol = "";

                switch (icon)
                {
                    case CustomMessageBoxImage.Information:
                        fillColor = Color.FromRgb(0, 123, 255);
                        symbol = "ℹ";
                        break;
                    case CustomMessageBoxImage.Warning:
                        fillColor = Color.FromRgb(255, 193, 7);
                        symbol = "⚠";
                        break;
                    case CustomMessageBoxImage.Error:
                        fillColor = Color.FromRgb(220, 53, 69);
                        symbol = "✕";
                        break;
                    case CustomMessageBoxImage.Question:
                        fillColor = Color.FromRgb(0, 123, 255);
                        symbol = "?";
                        break;
                }

                context.DrawEllipse(new SolidColorBrush(fillColor), null, new Point(24, 24), 20, 20);

                var formattedText = new FormattedText(
                    symbol,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    28,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                context.DrawText(formattedText, new Point(24 - formattedText.Width / 2, 24 - formattedText.Height / 2));
            }

            var renderTargetBitmap = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            _messageIcon.Source = renderTargetBitmap;
            _messageIcon.Visibility = Visibility.Visible;
        }

        private void CreateButtons(CustomMessageBoxButton buttonType)
        {
            if (_buttonsPanel == null) return;

            _buttonsPanel.Children.Clear();

            switch (buttonType)
            {
                case CustomMessageBoxButton.OK:
                    CreateButton("OK", CustomMessageBoxResult.OK, true);
                    break;
                case CustomMessageBoxButton.OKCancel:
                    CreateButton("OK", CustomMessageBoxResult.OK, true);
                    CreateButton("Отмена", CustomMessageBoxResult.Cancel, false);
                    break;
                case CustomMessageBoxButton.YesNo:
                    CreateButton("Да", CustomMessageBoxResult.Yes, true);
                    CreateButton("Нет", CustomMessageBoxResult.No, false);
                    break;
                case CustomMessageBoxButton.YesNoCancel:
                    CreateButton("Да", CustomMessageBoxResult.Yes, true);
                    CreateButton("Нет", CustomMessageBoxResult.No, false);
                    CreateButton("Отмена", CustomMessageBoxResult.Cancel, false);
                    break;
                case CustomMessageBoxButton.AbortRetryIgnore:
                    CreateButton("Прервать", CustomMessageBoxResult.Abort, true);
                    CreateButton("Повторить", CustomMessageBoxResult.Retry, false);
                    CreateButton("Игнорировать", CustomMessageBoxResult.Ignore, false);
                    break;
            }
        }

        private void CreateButton(string text, CustomMessageBoxResult result, bool isDefault)
        {
            if (_buttonsPanel == null) return;

            var button = new Button
            {
                Content = text,
                MinWidth = 80,
                Height = 36,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = result,
                IsDefault = isDefault
            };

            // Пытаемся найти стиль, если не найден - кнопка будет стандартной, но рабочей
            if (Application.Current.TryFindResource("SaveButtonStyle") is Style btnStyle)
            {
                button.Style = btnStyle;
            }
            // Если стиля нет, можно задать базовые свойства здесь, чтобы кнопка точно была видна
            else
            {
                button.Background = Brushes.LightGray;
                button.Foreground = Brushes.Black;
                button.BorderBrush = Brushes.Gray;
                button.BorderThickness = new Thickness(1);
            }

            if (isDefault)
            {
                button.Focus();
            }

            button.Click += (sender, e) =>
            {
                _result = result;
                Close();
            };

            _buttonsPanel.Children.Add(button);
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            _result = CustomMessageBoxResult.Cancel;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                _result = CustomMessageBoxResult.Cancel;
                Close();
            }

            if (e.Key == Key.Enter && _buttonsPanel != null)
            {
                foreach (var child in _buttonsPanel.Children)
                {
                    if (child is Button btn && btn.IsDefault)
                    {
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                }
            }
        }
    }
}