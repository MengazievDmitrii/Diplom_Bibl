using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplom_Bibl.Controls
{
    /// <summary>
    /// Типы кнопок для MessageBox
    /// </summary>
    public enum CustomMessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
        AbortRetryIgnore
    }

    /// <summary>
    /// Типы иконок для MessageBox
    /// </summary>
    public enum CustomMessageBoxImage
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }

    /// <summary>
    /// Результаты нажатия кнопок
    /// </summary>
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

    /// <summary>
    /// Кастомное диалоговое окно вместо стандартного MessageBox
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        private CustomMessageBoxResult _result = CustomMessageBoxResult.None;

        public CustomMessageBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Показывает сообщение с одной кнопкой OK
        /// </summary>
        public static CustomMessageBoxResult Show(string message)
        {
            return ShowInternal(message, string.Empty, CustomMessageBoxButton.OK, CustomMessageBoxImage.None, null);
        }

        /// <summary>
        /// Показывает сообщение с заголовком
        /// </summary>
        public static CustomMessageBoxResult Show(string message, string caption)
        {
            return ShowInternal(message, caption, CustomMessageBoxButton.OK, CustomMessageBoxImage.None, null);
        }

        /// <summary>
        /// Показывает сообщение с заголовком и типом кнопок
        /// </summary>
        public static CustomMessageBoxResult Show(string message, string caption, CustomMessageBoxButton button)
        {
            return ShowInternal(message, caption, button, CustomMessageBoxImage.None, null);
        }

        /// <summary>
        /// Показывает сообщение с заголовком, кнопками и иконкой
        /// </summary>
        public static CustomMessageBoxResult Show(string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            return ShowInternal(message, caption, button, icon, null);
        }

        /// <summary>
        /// Показывает сообщение с владельцем
        /// </summary>
        public static CustomMessageBoxResult Show(Window owner, string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            return ShowInternal(message, caption, button, icon, owner);
        }

        /// <summary>
        /// Внутренний метод для отображения окна
        /// </summary>
        private static CustomMessageBoxResult ShowInternal(string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon, Window owner)
        {
            var messageBox = new CustomMessageBox
            {
                Title = string.IsNullOrEmpty(caption) ? "Сообщение" : caption
            };

            // Устанавливаем текст сообщения
            messageBox.MessageText.Text = message;

            // Устанавливаем иконку
            messageBox.SetIcon(icon);

            // Создаем кнопки
            messageBox.CreateButtons(button);

            // Устанавливаем владельца
            if (owner != null)
            {
                messageBox.Owner = owner;
            }

            messageBox.ShowDialog();
            return messageBox._result;
        }

        /// <summary>
        /// Устанавливает иконку в зависимости от типа
        /// </summary>
        private void SetIcon(CustomMessageBoxImage icon)
        {
            // Скрываем иконку по умолчанию, покажем если тип не None
            MessageIcon.Visibility = Visibility.Collapsed;

            if (icon == CustomMessageBoxImage.None)
                return;

            // Создаем цветной круг с символом программно
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // Цвет фона
                var bgColor = Colors.Transparent;
                var fillColor = Colors.Gray;
                var symbol = "";

                switch (icon)
                {
                    case CustomMessageBoxImage.Information:
                        fillColor = Color.FromRgb(0, 123, 255); // AccentColor
                        symbol = "ℹ";
                        break;
                    case CustomMessageBoxImage.Warning:
                        fillColor = Color.FromRgb(255, 193, 7); // WarningColor
                        symbol = "⚠";
                        break;
                    case CustomMessageBoxImage.Error:
                        fillColor = Color.FromRgb(220, 53, 69); // DangerColor
                        symbol = "✕";
                        break;
                    case CustomMessageBoxImage.Question:
                        fillColor = Color.FromRgb(0, 123, 255); // AccentColor
                        symbol = "?";
                        break;
                }

                // Рисуем круг
                context.DrawEllipse(new SolidColorBrush(fillColor), null, new Point(24, 24), 20, 20);

                // Рисуем символ
                var formattedText = new FormattedText(
                    symbol,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    28,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                context.DrawText(formattedText, new Point(24 - formattedText.Width / 2, 24 - formattedText.Height / 2));
            }

            var renderTargetBitmap = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            MessageIcon.Source = renderTargetBitmap;
            MessageIcon.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Создает кнопки в зависимости от типа
        /// </summary>
        private void CreateButtons(CustomMessageBoxButton buttonType)
        {
            ButtonsPanel.Children.Clear();

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

        /// <summary>
        /// Создает кнопку
        /// </summary>
        private void CreateButton(string text, CustomMessageBoxResult result, bool isDefault)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 80,
                Height = 36,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = result
            };

            // Применяем стиль из App.xaml
            button.Style = (Style)FindResource("SaveButtonStyle");

            if (isDefault)
            {
                button.Focus();
            }

            button.Click += (sender, e) =>
            {
                _result = result;
                Close();
            };

            ButtonsPanel.Children.Add(button);
        }

        /// <summary>
        /// Обработчик нажатия на кнопку закрытия
        /// </summary>
        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            _result = CustomMessageBoxResult.Cancel;
            Close();
        }

        /// <summary>
        /// Закрытие по ESC
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                _result = CustomMessageBoxResult.Cancel;
                Close();
            }

            if (e.Key == Key.Enter)
            {
                // Ищем кнопку по умолчанию
                foreach (var child in ButtonsPanel.Children)
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
