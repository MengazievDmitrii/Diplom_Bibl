using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Diplom_Bibl.Controls
{
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
        private Image _messageIcon;
        private TextBlock _messageText;
        private StackPanel _buttonsPanel;

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
            _messageIcon = GetTemplateChild("MessageIcon") as Image;
            _messageText = GetTemplateChild("MessageText") as TextBlock;
            _buttonsPanel = GetTemplateChild("ButtonsPanel") as StackPanel;

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
            // Если вызвано не из UI-потока, переключаемся
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return (CustomMessageBoxResult)Application.Current.Dispatcher.Invoke(() =>
                    ShowInternal(message, caption, button, icon, owner));
            }

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
            _messageIcon.Source = null;

            if (icon == CustomMessageBoxImage.None)
                return;

            _messageIcon.Visibility = Visibility.Visible;
            _messageIcon.Width = 32;
            _messageIcon.Height = 32;
            _messageIcon.Margin = new Thickness(0, 0, 10, 0);

            string symbol = "●";
            string colorHex = "#888888";

            switch (icon)
            {
                case CustomMessageBoxImage.Information:
                    symbol = "ℹ";
                    colorHex = "#007BFF";
                    break;
                case CustomMessageBoxImage.Warning:
                    symbol = "⚠";
                    colorHex = "#FFC107";
                    break;
                case CustomMessageBoxImage.Error:
                    symbol = "✕";
                    colorHex = "#DC3545";
                    break;
                case CustomMessageBoxImage.Question:
                    symbol = "?";
                    colorHex = "#007BFF";
                    break;
            }

            // Используем TextBlock вместо сложной графики (избегаем проблем с потоками)
            var textBlock = new TextBlock
            {
                Text = symbol,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _messageIcon.Source = null;
            // Image не может содержать TextBlock, поэтому используем другой подход
            // Просто скрываем Image и показываем текст рядом
            _messageIcon.Visibility = Visibility.Collapsed;
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
                Height = 32,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = result,
                IsDefault = isDefault,
                // Используем стили из ресурсов
                Style = (Style)FindResource("MessageBoxButtonStyle")
            };

            // Для кнопок "Нет" и "Отмена" используем другой стиль
            if (result == CustomMessageBoxResult.Cancel || result == CustomMessageBoxResult.No)
            {
                button.Style = (Style)FindResource("MessageBoxSecondaryButtonStyle");
            }

            if (isDefault)
            {
                button.Focus();
                // Делаем кнопку подсвеченной
                button.Background = (Brush)FindResource("PrimaryColor");
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