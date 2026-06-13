using System;
using System.Globalization;
using System.Windows.Data;

namespace Diplom_Bibl
{
    public class DateDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ReportRow row)
            {
                if (row.IsSummary)
                {
                    return ""; // Пустая строка для строки итога
                }
                // Форматируем дату для обычных строк
                // Проверяем, не null ли DateRecord и не равно ли значению по умолчанию
                if (row.DateRecord.HasValue && row.DateRecord.Value != DateTime.MinValue && row.DateRecord.Value != default(DateTime))
                {
                    return row.DateRecord.Value.ToString("dd.MM.yyyy"); // Вызываем ToString у значения
                }
                return ""; // Если дата null, по умолчанию и не итог
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
