using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для ReportsPage.xaml
    /// </summary>
    public partial class ReportsPage : Page
    {
        // --- Поля ---
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        public ReportsPage()
        {
            InitializeComponent();
        }

        // --- Обработчики для кнопок на карточке "Информационное обслуживание" ---
        private void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            DatePicker startDatePicker = StartDatePicker;
            DatePicker endDatePicker = EndDatePicker;

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

            Windows.ReportViewWindow reportWindow = new Windows.ReportViewWindow(start, end);
            reportWindow.ShowDialog();
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            DatePicker startDatePicker = StartDatePicker;
            DatePicker endDatePicker = EndDatePicker;

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

            ExportReportToExcel(start, end);
        }

        // --- Метод загрузки данных для отчёта ---
        private ObservableCollection<InfoRecord> LoadReportData(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<InfoRecord>();
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
                                var record = new InfoRecord
                                {
                                    IdRecord = (int)reader["id_record"],
                                    DateRecord = (DateTime)reader["date_record"],
                                    DeliveryBook = (int)reader["delivery_book"],
                                    DeliveryCertificate = (int)reader["delivery_certificate"],
                                    EventsCount = (int)reader["events_count"]
                                };
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
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке данных для экспорта: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            return data;
        }

        private void ExportReportToExcel(DateTime start, DateTime end)
        {
            var reportData = LoadReportData(start, end);

            if (reportData == null || reportData.Count == 0)
            {
                if (reportData != null)
                {
                    MessageBox.Show("Нет данных за выбранный период для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                    ExportToExcel(reportData, saveFileDialog.FileName);

                    Excel.Application excelApp = new Excel.Application();
                    excelApp.Visible = true;
                    Excel.Workbook workbook = excelApp.Workbooks.Open(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте в Excel или при открытии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToExcel(ObservableCollection<InfoRecord> data, string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = false;

                workbook = excelApp.Workbooks.Add();
                worksheet = workbook.ActiveSheet;
                worksheet.Name = "Отчёт";

                // Заголовки
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Дата";
                worksheet.Cells[1, 3].Value = "Книги";
                worksheet.Cells[1, 4].Value = "Справки";
                worksheet.Cells[1, 5].Value = "Мероприятия (чел.)";

                // Данные
                int row = 2;
                foreach (var record in data)
                {
                    worksheet.Cells[row, 1].Value = record.IdRecord;
                    worksheet.Cells[row, 2].Value = record.DateRecord;
                    worksheet.Cells[row, 3].Value = record.DeliveryBook;
                    worksheet.Cells[row, 4].Value = record.DeliveryCertificate;
                    worksheet.Cells[row, 5].Value = record.EventsCount;
                    row++;
                }

                // Итог
                int summaryRow = row;
                worksheet.Cells[summaryRow, 1].Value = "Итого:";
                int lastDataRow = summaryRow - 1;
                worksheet.Cells[summaryRow, 3].Formula = $"=SUM(C2:C{lastDataRow})";
                worksheet.Cells[summaryRow, 4].Formula = $"=SUM(D2:D{lastDataRow})";
                worksheet.Cells[summaryRow, 5].Formula = $"=SUM(E2:E{lastDataRow})";

                worksheet.Columns.AutoFit();

                if (File.Exists(filePath))
                {
                    var result = MessageBox.Show($"Файл {filePath} уже существует. Перезаписать?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                    {
                        throw new Exception("Операция отменена пользователем.");
                    }
                }
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Interop: {ex.Message}", ex);
            }
            finally
            {
                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                    worksheet = null;
                }
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                    workbook = null;
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                    excelApp = null;
                }
            }
        }

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
            return null;
        }
    }
}