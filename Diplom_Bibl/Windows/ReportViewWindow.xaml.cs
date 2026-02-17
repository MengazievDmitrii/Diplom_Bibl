using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Excel = Microsoft.Office.Interop.Excel; // Создаём псевдоним для удобства
using System.Diagnostics;

namespace Diplom_Bibl.Windows
{
    /// <summary>
    /// Логика взаимодействия для ReportViewWindow.xaml
    /// </summary>
    public partial class ReportViewWindow : Window
    {
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;"; // ЗАМЕНИТЬ!
        // Изменён тип коллекции
        private ObservableCollection<ReportRow> reportData;
        private DateTime startDate;
        private DateTime endDate;

        public ReportViewWindow(DateTime start, DateTime end)
        {
            InitializeComponent();
            startDate = start.Date; // Убираем время
            endDate = end.Date;     // Убираем время
            PeriodLabel.Text = $"Отчёт за период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
            reportData = new ObservableCollection<ReportRow>();
            ReportDataGrid.ItemsSource = reportData;
            LoadReportData();
        }

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
        private void LoadReportData()
        {

            reportData.Clear();
            string query = @"
                SELECT id_record, date_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record BETWEEN @StartDate AND @EndDate 
                ORDER BY date_record, id_record";

            int totalBooks = 0;
            int totalRefs = 0;
            int totalEvents = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Проверяем на DBNull
                                int id = reader["id_record"] == DBNull.Value ? 0 : (int)reader["id_record"]; // Или другое значение по умолчанию
                                DateTime? date = reader["date_record"] == DBNull.Value ? (DateTime?)null : (DateTime)reader["date_record"];
                                int books = reader["delivery_book"] == DBNull.Value ? 0 : (int)reader["delivery_book"];
                                int refs = reader["delivery_certificate"] == DBNull.Value ? 0 : (int)reader["delivery_certificate"];
                                int events = reader["events_count"] == DBNull.Value ? 0 : (int)reader["events_count"];

                                var record = new ReportRow(
                                    id: id,
                                    date: date.Value, // передаём nullable DateTime
                                    books: books,
                                    refs: refs,
                                    events: events
                                );
                                reportData.Add(record);

                                // Накапливаем суммы для итога
                                totalBooks += record.DeliveryBook;
                                totalRefs += record.DeliveryCertificate;
                                totalEvents += record.EventsCount;
                            }
                        }
                    }
                }

                // Добавляем строку итога в конец коллекции
                reportData.Add(new ReportRow(totalBooks, totalRefs, totalEvents));
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка базы данных при загрузке отчёта: {sqlEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке отчёта: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, есть ли данные для экспорта (проверяем, есть ли что-то кроме строки итога)
            if (reportData.Count <= 1) // Если только строка итога или пусто
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                FileName = $"Отчёт_Информационное_обслуживание_{startDate:yyyyMMdd}_-_ {endDate:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Передаём новую коллекцию
                    ExportToExcel(reportData, saveFileDialog.FileName);
                    MessageBox.Show($"Отчёт успешно экспортирован в:\n{saveFileDialog.FileName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Метод для экспорта данных в Excel через Interop
        // Принимает ObservableCollection<ReportRow>
        private void ExportToExcel(ObservableCollection<ReportRow> data, string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                // Создаём экземпляр Excel приложения
                excelApp = new Excel.Application();
                excelApp.Visible = false; // Не показываем Excel пользователю

                // Создаём новую книгу
                workbook = excelApp.Workbooks.Add();
                // Получаем первый лист (Worksheet)
                worksheet = workbook.ActiveSheet;
                worksheet.Name = "Отчёт";

                // Заголовки (первая строка)
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Дата";
                worksheet.Cells[1, 3].Value = "Книги";
                worksheet.Cells[1, 4].Value = "Справки";
                worksheet.Cells[1, 5].Value = "Мероприятия (чел.)";

                // Данные (начиная со второй строки)
                int row = 2; // Начинаем с 2 строки
                foreach (var reportRow in data)
                {
                    if (reportRow.IsSummary)
                    {
                        // Строка итога
                        worksheet.Cells[row, 1].Value = "Итого:";
                        worksheet.Cells[row, 2].Value = ""; // Пустая дата
                        worksheet.Cells[row, 3].Value = reportRow.DeliveryBook; // Сумма
                        worksheet.Cells[row, 4].Value = reportRow.DeliveryCertificate; // Сумма
                        worksheet.Cells[row, 5].Value = reportRow.EventsCount; // Сумма
                        // Можно добавить форматирование (жирный шрифт) для строки итога
                        worksheet.Cells[row, 1].EntireRow.Font.Bold = true;
                    }
                    else
                    {
                        // Обычная строка данных
                        worksheet.Cells[row, 1].Value = reportRow.IdRecord;
                        worksheet.Cells[row, 2].Value = reportRow.DateRecord;
                        worksheet.Cells[row, 3].Value = reportRow.DeliveryBook;
                        worksheet.Cells[row, 4].Value = reportRow.DeliveryCertificate;
                        worksheet.Cells[row, 5].Value = reportRow.EventsCount;
                    }
                    row++;
                }

                // Автоширина столбцов (опционально)
                worksheet.Columns.AutoFit();

                // Сохраняем книгу по указанному пути
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook); // Указываем формат .xlsx
            }
            catch (Exception ex)
            {
                // Перебрасываем исключение, чтобы оно было поймано в ExportExcelButton_Click
                throw new Exception($"Ошибка Interop: {ex.Message}", ex);
            }
            finally
            {
                // Обязательно освобождаем ресурсы COM
                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                }
                if (workbook != null)
                {
                    workbook.Close(false); // false - не сохранять изменения при закрытии
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit(); // Завершаем процесс Excel
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
            }
        }
    }
}