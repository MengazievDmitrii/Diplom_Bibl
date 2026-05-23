using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Diplom_Bibl.Converters
{
    /// <summary>
    /// Конвертер для преобразования null или пустой строки в Visibility
    /// </summary>
    public class IsNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;

            if (parameter != null && parameter.ToString() == "inverse")
            {
                // Обратный режим: пусто = Visible, не пусто = Collapsed
                return string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Прямой режим: не пусто = Visible, пусто = Collapsed
            return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}