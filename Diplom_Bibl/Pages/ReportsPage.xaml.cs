using System;
using System.Collections.ObjectModel; // Для ObservableCollection
using System.Data.SqlClient; // Или Microsoft.Data.SqlClient
using System.Diagnostics; // Для Process.Start (если нужно открыть через ассоциацию)
using System.IO; // Для Path, FileInfo
using System.Windows;
using System.Windows.Controls; // Для Button, DatePicker
using Microsoft.Win32; // Для SaveFileDialog
using Excel = Microsoft.Office.Interop.Excel; // Создаём псевдоним для удобства

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для ReportsPage.xaml
    /// </summary>
    public partial class ReportsPage : Page
    {
        // --- Поля ---
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;"; // ЗАМЕНИТЬ!

        public ReportsPage()
        {
            // Установка лицензии НЕ нужна для Interop
            InitializeComponent();
        }

        // --- Обработчики для кнопок на карточке "Информационное обслуживание" ---
        // Эти обработчики должны соответствовать Click="..." в XAML ReportsPage.xaml

        private void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранные даты из фильтров ИСПОЛЬЗУЯ x:Name из XAML
            // Убедитесь, что в XAML вы добавили x:Name="StartDatePicker" и x:Name="EndDatePicker"
            DatePicker startDatePicker = StartDatePicker; // Прямой доступ через имя
            DatePicker endDatePicker = EndDatePicker;   // Прямой доступ через имя

            if (startDatePicker?.SelectedDate == null || endDatePicker?.SelectedDate == null)
            {
                MessageBox.Show("Пожалуйста, выберите обе даты (с и по).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime start = startDatePicker.SelectedDate.Value.Date;
            DateTime end = endDatePicker.SelectedDate.Value.Date;

            if (start > end)
            {
                MessageBox.Show("Дата 'с' не может быть позже даты 'по'.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Открываем окно просмотра отчёта с переданными датами
            Windows.ReportViewWindow reportWindow = new Windows.ReportViewWindow(start, end);
            reportWindow.ShowDialog(); // Показываем модально
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранные даты из фильтров ИСПОЛЬЗУЯ x:Name из XAML
            DatePicker startDatePicker = StartDatePicker; // Прямой доступ через имя
            DatePicker endDatePicker = EndDatePicker;   // Прямой доступ через имя

            if (startDatePicker?.SelectedDate == null || endDatePicker?.SelectedDate == null)
            {
                MessageBox.Show("Пожалуйста, выберите обе даты (с и по).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime start = startDatePicker.SelectedDate.Value.Date;
            DateTime end = endDatePicker.SelectedDate.Value.Date;

            if (start > end)
            {
                MessageBox.Show("Дата 'с' не может быть позже даты 'по'.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- ВАЖНО: Экспорт выполняется ВНУТРИ ReportsPage ---
            ExportReportToExcel(start, end);
        }

        // --- Метод загрузки данных для отчёта ---
        private ObservableCollection<InformationServicePage.InfoRecord> LoadReportData(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<InformationServicePage.InfoRecord>();
            string query = @"
                SELECT id_record, date_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record BETWEEN @StartDate AND @EndDate 
                ORDER BY date_record, id_record";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", start);
                        command.Parameters.AddWithValue("@EndDate", end);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var record = new InformationServicePage.InfoRecord
                                (
                                    id: (int)reader["id_record"],
                                    date: (DateTime)reader["date_record"],
                                    books: (int)reader["delivery_book"],
                                    refs: (int)reader["delivery_certificate"],
                                    events: (int)reader["events_count"] // Убедитесь, что столбец events_count существует
                                );
                                data.Add(record);
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка базы данных при загрузке данных для экспорта: {sqlEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null; // Возвращаем null в случае ошибки
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке данных для экспорта: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null; // Возвращаем null в случае ошибки
            }

            return data; // Возвращаем загруженные данные
        }

        // --- Метод экспорта данных в Excel и открытия в Excel ---
        // --- Метод экспорта данных в Excel и открытия в Excel ---
        private void ExportReportToExcel(DateTime start, DateTime end)
        {
            var reportData = LoadReportData(start, end);

            if (reportData == null || reportData.Count == 0)
            {
                // LoadReportData уже покажет сообщение об ошибке, если произошла ошибка.
                // Если данных нет, покажем соответствующее сообщение.
                if (reportData != null) // Если не null, значит, просто пусто
                {
                    MessageBox.Show("Нет данных за выбранный период для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                // Если reportData == null, сообщение об ошибке уже показано в LoadReportData
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                FileName = $"Отчёт_Информационное_обслуживание_{start:yyyyMMdd}_-_ {end:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Вызываем метод экспорта (теперь с Interop)
                    // Этот метод теперь полностью закрывает все свои ресурсы COM
                    ExportToExcel(reportData, saveFileDialog.FileName);

                    // --- Открываем СОЗДАННЫЙ файл в НОВОМ Excel ---
                    // Создаём НОВЫЙ экземпляр Excel приложения
                    Excel.Application excelApp = new Excel.Application();
                    excelApp.Visible = true; // Показываем Excel пользователю

                    // Открываем книгу из файла
                    Excel.Workbook workbook = excelApp.Workbooks.Open(saveFileDialog.FileName);

                    // ПРИМЕЧАНИЕ: ВАЖНО!
                    // Не освобождаем excelApp и workbook здесь через Marshal.ReleaseComObject,
                    // так как мы хотим, чтобы Excel остался открытым с книгой.
                    // Пользователь сам закроет Excel.
                    // Это означает, что процесс EXCEL.EXE останется в памяти до тех пор,
                    // пока пользователь не закроет Excel вручную.
                    // Это компромисс для удобства пользователя.

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте в Excel или при открытии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- Метод экспорта с использованием Interop ---
        // Теперь этот метод полностью создает/перезаписывает файл и освобождает свои ресурсы COM.
        private void ExportToExcel(ObservableCollection<InformationServicePage.InfoRecord> data, string filePath)
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
                foreach (var record in data)
                {
                    worksheet.Cells[row, 1].Value = record.IdRecord;
                    worksheet.Cells[row, 2].Value = record.DateRecord;
                    worksheet.Cells[row, 3].Value = record.DeliveryBook;
                    worksheet.Cells[row, 4].Value = record.DeliveryCertificate;
                    worksheet.Cells[row, 5].Value = record.EventsCount;
                    row++;
                }

                // --- Добавляем строку Итог ---
                int summaryRow = row; // Номер строки для "Итог"
                worksheet.Cells[summaryRow, 1].Value = "Итого:";

                // Формулы суммы для столбцов 3 (Книги), 4 (Справки), 5 (Мероприятия)
                int lastDataRow = summaryRow - 1; // Номер последней строки с данными
                worksheet.Cells[summaryRow, 3].Formula = $"=SUM(C2:C{lastDataRow})";
                worksheet.Cells[summaryRow, 4].Formula = $"=SUM(D2:D{lastDataRow})";
                worksheet.Cells[summaryRow, 5].Formula = $"=SUM(E2:E{lastDataRow})";

                // Автоширина столбцов (опционально)
                worksheet.Columns.AutoFit();

                // Сохраняем книгу по указанному пути
                // Проверяем, существует ли файл, и если да, то спрашиваем пользователя *до* сохранения через Interop
                if (File.Exists(filePath))
                {
                    var result = MessageBox.Show($"Файл {filePath} уже существует. Перезаписать?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                    {
                        throw new Exception("Операция отменена пользователем.");
                    }
                    // Если файл существует и пользователь согласен, Interop попытается его перезаписать.
                    // Убедитесь, что файл не открыт в другом экземпляре Excel.
                }
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook); // Указываем формат .xlsx
                                                                                 // ВАЖНО: SaveAs завершает операцию записи.
            }
            catch (Exception ex)
            {
                // Перебрасываем исключение, чтобы оно было поймано в ExportReportToExcel
                throw new Exception($"Ошибка Interop: {ex.Message}", ex);
            }
            finally
            {
                // Обязательно освобождаем ресурсы COM, связанные с созданием/записью файла
                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                    worksheet = null; // Очищаем ссылку
                }
                if (workbook != null)
                {
                    workbook.Close(false); // Закрываем книгу без сохранения (мы уже сохранили)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                    workbook = null; // Очищаем ссылку
                }
                if (excelApp != null)
                {
                    excelApp.Quit(); // Завершаем приложение
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                    excelApp = null; // Очищаем ссылку
                }
            }
            // Метод завершён, все ресурсы, связанные с созданием файла, освобождены.
            // Процесс EXCEL.EXE, созданный для записи, должен завершиться.
        }
        

        // Вспомогательный метод для поиска DatePicker по Grid.Column
        // Убедитесь, что в XAML ReportsPage.xaml у Border с фильтрами есть x:Name="FilterBorder"
        private DatePicker FindDatePickerByColumn(int column)
        {
            var filterBorder = this.FindName("FilterBorder") as Border;
            if (filterBorder != null)
            {
                var grid = filterBorder.Child as Grid;
                if (grid != null)
                {
                    foreach (UIElement child in grid.Children)
                    {
                        if (Grid.GetColumn(child) == column && child is DatePicker dp)
                        {
                            return dp;
                        }
                    }
                }
            }
            return null; // Не найден
        }
    }
}