using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Diplom_Bibl
{
    public class IsNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, является ли значение строкой и пуста ли она
            if (value is string str)
            {
                return string.IsNullOrEmpty(str) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Если значение не строка, проверяем на null
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Обратное преобразование не требуется
            return Binding.DoNothing;
        }
    }
}