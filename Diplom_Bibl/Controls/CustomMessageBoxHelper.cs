using System.Windows;

namespace Diplom_Bibl.Controls
{
    /// <summary>
    /// Класс-помощник для замены стандартного MessageBox на кастомный
    /// Используется для быстрой замены  CustomMessageBox.Show() на CustomMessageBoxHelper.Show()
    /// </summary>
    public static class CustomMessageBoxHelper
    {
        /// <summary>
        /// Показывает сообщение с одной кнопкой OK
        /// </summary>
        public static MessageBoxResult Show(string message)
        {
            var result =  CustomMessageBox.Show(message);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком
        /// </summary>
        public static MessageBoxResult Show(string message, string caption)
        {
            var result =  CustomMessageBox.Show(message, caption);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком и кнопками
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, CustomMessageBoxButton button)
        {
            var customButton = ConvertButton(button);
            var result =  CustomMessageBox.Show(message, caption, customButton);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с заголовком, кнопками и иконкой
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            var customButton = ConvertButton(button);
            var customIcon = ConvertIcon(icon);
            var result =  CustomMessageBox.Show(message, caption, customButton, customIcon);
            return ConvertResult(result);
        }

        /// <summary>
        /// Показывает сообщение с владельцем
        /// </summary>
        public static MessageBoxResult Show(Window owner, string message, string caption, CustomMessageBoxButton button, CustomMessageBoxImage icon)
        {
            var customButton = ConvertButton(button);
            var customIcon = ConvertIcon(icon);
            var result =  CustomMessageBox.Show(owner, message, caption, customButton, customIcon);
            return ConvertResult(result);
        }

        /// <summary>
        /// Конвертирует стандартную CustomMessageBoxButton в CustomMessageBoxButton
        /// </summary>
        private static CustomMessageBoxButton ConvertButton(CustomMessageBoxButton button)
        {
            switch (button)
            {
                case CustomMessageBoxButton.OK:
                    return CustomMessageBoxButton.OK;
                case CustomMessageBoxButton.OKCancel:
                    return CustomMessageBoxButton.OKCancel;
                case CustomMessageBoxButton.YesNo:
                    return CustomMessageBoxButton.YesNo;
                case CustomMessageBoxButton.YesNoCancel:
                    return CustomMessageBoxButton.YesNoCancel;
                default:
                    return CustomMessageBoxButton.OK;
            }
        }

        /// <summary>
        /// Конвертирует стандартную CustomMessageBoxImage в CustomMessageBoxImage
        /// </summary>
        private static CustomMessageBoxImage ConvertIcon(CustomMessageBoxImage icon)
        {
            switch (icon)
            {
                case CustomMessageBoxImage.Information:
                    return CustomMessageBoxImage.Information;
                case CustomMessageBoxImage.Warning:
                    return CustomMessageBoxImage.Warning;
                case CustomMessageBoxImage.Error:
                    return CustomMessageBoxImage.Error;
                case CustomMessageBoxImage.Question:
                    return CustomMessageBoxImage.Question;
                case CustomMessageBoxImage.None:
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
