using System;
using System.Globalization;
using System.Windows.Data;

namespace Diplom_Bibl // Убедитесь, что namespace совпадает с остальным проектом
{
    // Конвертер для отображения ID в DataGrid
    public class IdDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ReportRow row)
            {
                if (row.IsSummary)
                {
                    return "Итог"; // Отображаем "Итог" в столбце ID для строки итога
                }
                return row.IdRecord.ToString(); // Или просто row.IdRecord, если Binding на TextBlock
            }
            return value?.ToString() ?? "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack не нужен для отображения, только для редактирования
            throw new NotImplementedException();
        }
        

        
    }

        
    }