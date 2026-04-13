using System.Windows;

namespace Diplom_Bibl.Controls
{
    /// <summary>
    /// Класс-помощник для замены стандартного MessageBox на кастомный
    /// Используется для быстрой замены MessageBox.Show() на CustomMessageBoxHelper.Show()
    /// </summary>
    public static class CustomMessageBoxHelper
    {
        /// <summary>
        /// Показывает сообщение с одной кнопкой OK
        /// </summary>
        public static MessageBoxResult Show(string message)
        {
            var result = CustomMessageBox.Show(message);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком
        /// </summary>
        public static MessageBoxResult Show(string message, string caption)
        {
            var result = CustomMessageBox.Show(message, caption);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком и кнопками
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button)
        {
            var customButton = ConvertButton(button);
            var result = CustomMessageBox.Show(message, caption, customButton);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком, кнопками и иконкой
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var customButton = ConvertButton(button);
            var customIcon = ConvertIcon(icon);
            var result = CustomMessageBox.Show(message, caption, customButton, customIcon);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с владельцем
        /// </summary>
        public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var customButton = ConvertButton(button);
            var customIcon = ConvertIcon(icon);
            var result = CustomMessageBox.Show(owner, message, caption, customButton, customIcon);
            return ConvertResult(result);
        }

        /// <summary>
        /// Конвертирует стандартную MessageBoxButton в CustomMessageBoxButton
        /// </summary>
        private static CustomMessageBoxButton ConvertButton(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    return CustomMessageBoxButton.OK;
                case MessageBoxButton.OKCancel:
                    return CustomMessageBoxButton.OKCancel;
                case MessageBoxButton.YesNo:
                    return CustomMessageBoxButton.YesNo;
                case MessageBoxButton.YesNoCancel:
                    return CustomMessageBoxButton.YesNoCancel;
                default:
                    return CustomMessageBoxButton.OK;
            }
        }

        /// <summary>
        /// Конвертирует стандартную MessageBoxImage в CustomMessageBoxImage
        /// </summary>
        private static CustomMessageBoxImage ConvertIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Information:
                    return CustomMessageBoxImage.Information;
                case MessageBoxImage.Warning:
                    return CustomMessageBoxImage.Warning;
                case MessageBoxImage.Error:
                    return CustomMessageBoxImage.Error;
                case MessageBoxImage.Question:
                    return CustomMessageBoxImage.Question;
                case MessageBoxImage.None:
                default:
                    return CustomMessageBoxImage.None;
            }
        }

        /// <summary>
        /// Конвертирует CustomMessageBoxResult в стандартный MessageBoxResult
        /// </summary>
        private static MessageBoxResult ConvertResult(CustomMessageBoxResult result)
        {
            switch (result)
            {
                case CustomMessageBoxResult.OK:
                    return MessageBoxResult.OK;
                case CustomMessageBoxResult.Cancel:
                    return MessageBoxResult.Cancel;
                case CustomMessageBoxResult.Yes:
                    return MessageBoxResult.Yes;
                case CustomMessageBoxResult.No:
                    return MessageBoxResult.No;
                case CustomMessageBoxResult.Abort:
                    return MessageBoxResult.No;
                case CustomMessageBoxResult.Retry:
                    return MessageBoxResult.Yes;
                case CustomMessageBoxResult.Ignore:
                    return MessageBoxResult.None;
                default:
                    return MessageBoxResult.None;
            }
        }
    }
}
