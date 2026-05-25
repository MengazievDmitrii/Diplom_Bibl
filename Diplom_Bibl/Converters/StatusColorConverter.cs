// Файл: Converters/StatusColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Diplom_Bibl.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            switch (status)
            {
                case "returned":
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF"));
                case "overdue":
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                default:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}