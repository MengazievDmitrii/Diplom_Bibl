using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    public partial class ReportsPage : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private readonly string _excelExportPath;

        public ReportsPage()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            EndDatePicker.SelectedDate = DateTime.Now.Date;
            _excelExportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        // ==================== ОТЧЁТ 1: ВЫДАЧА ЛИТЕРАТУРЫ ====================
        private void ViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;

            var data = LoadBookIssuanceReport(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Отчёт по выдаче литературы");
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;

            var data = LoadBookIssuanceReport(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportBookIssuanceToExcel(data);
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;

            var data = LoadBookIssuanceReport(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportBookIssuanceToExcel(data, true);
        }

        private ObservableCollection<BookIssuanceReport> LoadBookIssuanceReport(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<BookIssuanceReport>();

            string query = @"
                SELECT 
                    i.IssuanceDate,
                    b.TitleBook,
                    b.InventaryNumber,
                    CONCAT(r.LastName, ' ', r.FirstName, ' ', ISNULL(r.MiddleName, '')) AS ReaderName,
                    rt.TypeName AS ReaderType,
                    CASE 
                        WHEN rt.TypeName = 'Студент' THEN ISNULL(g.GroupCode, '')
                        WHEN rt.TypeName = 'Сотрудник' THEN ISNULL(p.Post_Name, '')
                        ELSE ''
                    END AS GroupOrPost,
                    i.PlannedReturnDate,
                    i.ReturnDate,
                    i.Status,
                    CASE 
                        WHEN i.ReturnDate IS NULL AND i.PlannedReturnDate < GETDATE() 
                        THEN DATEDIFF(DAY, i.PlannedReturnDate, GETDATE())
                        WHEN i.ReturnDate > i.PlannedReturnDate
                        THEN DATEDIFF(DAY, i.PlannedReturnDate, i.ReturnDate)
                        ELSE 0
                    END AS DaysOverdue
                FROM BookIssuance i
                INNER JOIN Books b ON i.BookId = b.IDBook
                INNER JOIN ReaderForm r ON i.ReaderId = r.ReaderID
                INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
                LEFT JOIN Groups g ON r.GroupID = g.GroupID
                LEFT JOIN Post p ON r.Post = p.ID_Post
                WHERE CAST(i.IssuanceDate AS DATE) BETWEEN @StartDate AND @EndDate
                ORDER BY i.IssuanceDate DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", start.Date);
                        cmd.Parameters.AddWithValue("@EndDate", end.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                data.Add(new BookIssuanceReport
                                {
                                    IssuanceDate = reader.GetDateTime(0),
                                    BookTitle = reader.GetString(1),
                                    InventoryNumber = reader.GetString(2),
                                    ReaderName = reader.GetString(3),
                                    ReaderType = reader.GetString(4),
                                    GroupOrPost = reader.GetString(5),
                                    PlannedReturnDate = reader.GetDateTime(6),
                                    ReturnDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                    Status = reader.GetString(8),
                                    DaysOverdue = reader.GetInt32(9)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            return data;
        }

        // ==================== ОТЧЁТ 2: ЧИТАТЕЛЬСКАЯ АКТИВНОСТЬ ====================
        private void ViewReaderActivity_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadReaderActivityReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Читательская активность");
        }

        private void ExportReaderActivityExcel_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadReaderActivityReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportReaderActivityToExcel(data);
        }

        private void ExportReaderActivityPDF_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadReaderActivityReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportReaderActivityToExcel(data);
        }

        private ObservableCollection<ReaderActivityReport> LoadReaderActivityReport()
        {
            var data = new ObservableCollection<ReaderActivityReport>();

            string query = @"
                SELECT 
                    r.ReaderID,
                    r.CardNumber,
                    CONCAT(r.LastName, ' ', r.FirstName, ' ', ISNULL(r.MiddleName, '')) AS FullName,
                    rt.TypeName,
                    CASE 
                        WHEN rt.TypeName = 'Студент' THEN ISNULL(g.GroupCode, '')
                        WHEN rt.TypeName = 'Сотрудник' THEN ISNULL(p.Post_Name, '')
                        ELSE ''
                    END AS GroupOrPost,
                    COUNT(CASE WHEN i.Status = 'Возвращена' THEN 1 END) AS BooksReturned,
                    COUNT(CASE WHEN i.Status = 'Выдана' THEN 1 END) AS CurrentlyIssued,
                    SUM(CASE WHEN i.ReturnDate IS NULL AND i.PlannedReturnDate < GETDATE() THEN 1 ELSE 0 END) AS OverdueBooks,
                    MAX(i.IssuanceDate) AS LastIssuanceDate
                FROM ReaderForm r
                INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
                LEFT JOIN Groups g ON r.GroupID = g.GroupID
                LEFT JOIN Post p ON r.Post = p.ID_Post
                LEFT JOIN BookIssuance i ON r.ReaderID = i.ReaderId
                GROUP BY r.ReaderID, r.CardNumber, r.LastName, r.FirstName, r.MiddleName, 
                         rt.TypeName, g.GroupCode, p.Post_Name
                ORDER BY CurrentlyIssued DESC, BooksReturned DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int booksReturned = reader.GetInt32(5);
                            int currentlyIssued = reader.GetInt32(6);

                            data.Add(new ReaderActivityReport
                            {
                                ReaderID = reader.GetInt32(0),
                                CardNumber = reader.GetString(1),
                                FullName = reader.GetString(2),
                                TypeName = reader.GetString(3),
                                GroupOrPost = reader.GetString(4),
                                BooksReturned = booksReturned,
                                CurrentlyIssued = currentlyIssued,
                                OverdueBooks = reader.GetInt32(7),
                                LastIssuanceDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                BooksIssued = booksReturned + currentlyIssued
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            return data;
        }

        // ==================== ОТЧЁТ 3: ФОНД БИБЛИОТЕКИ ====================
        private void ViewFundReport_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadBookFundReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Состояние библиотечного фонда");
        }

        private void ExportFundReportPDF_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadBookFundReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportFundReportToExcel(data);
        }

        private ObservableCollection<BookFundReport> LoadBookFundReport()
        {
            var data = new ObservableCollection<BookFundReport>();

            string query = @"
                SELECT 
                    b.IDBook,
                    b.InventaryNumber,
                    b.TitleBook,
                    CONCAT(a.SurnameAuthor, ' ', a.NameAuthor, ' ', ISNULL(a.PatronymicAuthor, '')) AS AuthorName,
                    ISNULL(ph.TitlePublishingHouse, '') AS Publisher,
                    b.YearPublishing AS Year,
                    ISNULL(g.TitleGener, '') AS Genre,
                    ISNULL(s.Title_Status, '') AS Status,
                    b.Price,
                    b.TotalCopies,
                    b.AvailableCopies,
                    b.CopiesInUse
                FROM Books b
                INNER JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
                LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                ORDER BY b.TitleBook";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal? price = null;
                            if (!reader.IsDBNull(8))
                            {
                                price = reader.GetDecimal(8);
                            }

                            data.Add(new BookFundReport
                            {
                                IDBook = reader.GetInt32(0),
                                InventoryNumber = reader.GetString(1),
                                Title = reader.GetString(2),
                                AuthorName = reader.GetString(3),
                                Publisher = reader.GetString(4),
                                Year = reader.GetInt32(5),
                                Genre = reader.GetString(6),
                                Status = reader.GetString(7),
                                Price = price,
                                TotalCopies = reader.GetInt32(9),
                                AvailableCopies = reader.GetInt32(10),
                                CopiesInUse = reader.GetInt32(11)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            return data;
        }

        // ==================== ОТЧЁТ 4: ИНФОРМАЦИОННОЕ ОБСЛУЖИВАНИЕ ====================
        private void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;

            var data = LoadInfoServiceReport(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Информационное обслуживание");
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;

            var data = LoadInfoServiceReport(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ExportInfoServiceToExcel(data);
        }

        private ObservableCollection<InfoServiceReport> LoadInfoServiceReport(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<InfoServiceReport>();
            string query = @"
                SELECT id_record, date_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record BETWEEN @StartDate AND @EndDate 
                ORDER BY date_record, id_record";

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", start.Date);
                        command.Parameters.AddWithValue("@EndDate", end.Date);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                data.Add(new InfoServiceReport
                                {
                                    IdRecord = reader.GetInt32(0),
                                    DateRecord = reader.GetDateTime(1),
                                    DeliveryBook = reader.GetInt32(2),
                                    DeliveryCertificate = reader.GetInt32(3),
                                    EventsCount = reader.GetInt32(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            return data;
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================
        private bool ValidateDates()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                CustomMessageBox.Show("Пожалуйста, выберите обе даты (с и по).", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            DateTime start = StartDatePicker.SelectedDate.Value.Date;
            DateTime end = EndDatePicker.SelectedDate.Value.Date;

            if (start > end)
            {
                CustomMessageBox.Show("Дата 'с' не может быть позже даты 'по'.", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ShowReportWindow(object data, string title)
        {
            // Приводим data к IEnumerable
            var enumerableData = data as System.Collections.IEnumerable;
            if (enumerableData == null)
            {
                CustomMessageBox.Show("Ошибка: данные не являются коллекцией.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                return;
            }

            var reportWindow = new Window
            {
                Title = title,
                Width = 1200,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = true,
                IsReadOnly = true,
                Margin = new Thickness(10),
                ItemsSource = enumerableData,  // Используем приведенную переменную
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var grid = new Grid();
            grid.Children.Add(dataGrid);
            reportWindow.Content = grid;
            reportWindow.ShowDialog();
        }

        // ==================== ЭКСПОРТ В EXCEL ====================
        private void ExportBookIssuanceToExcel(ObservableCollection<BookIssuanceReport> data, bool openAfterExport = false)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Выдача_литературы_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToExcelGeneric(data, saveFileDialog.FileName, new[]
                    {
                        "Дата выдачи", "Название книги", "Инв. номер", "Читатель",
                        "Тип читателя", "Группа/Должность", "План. возврат", "Дата возврата",
                        "Статус", "Дней просрочки"
                    }, item => new object[]
                    {
                        item.IssuanceDate.ToString("dd.MM.yyyy HH:mm"),
                        item.BookTitle,
                        item.InventoryNumber,
                        item.ReaderName,
                        item.ReaderType,
                        item.GroupOrPost,
                        item.PlannedReturnDate.ToString("dd.MM.yyyy"),
                        item.ReturnDate?.ToString("dd.MM.yyyy") ?? "Не возвращена",
                        item.Status,
                        item.DaysOverdue
                    });

                    if (openAfterExport)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                    }
                    else
                    {
                        CustomMessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ExportReaderActivityToExcel(ObservableCollection<ReaderActivityReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Читательская_активность_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToExcelGeneric(data, saveFileDialog.FileName, new[]
                    {
                        "№ билета", "ФИО", "Тип", "Группа/Должность", "Всего выдано",
                        "Возвращено", "На руках", "Просрочено", "Последняя выдача"
                    }, item => new object[]
                    {
                        item.CardNumber,
                        item.FullName,
                        item.TypeName,
                        item.GroupOrPost,
                        item.BooksIssued,
                        item.BooksReturned,
                        item.CurrentlyIssued,
                        item.OverdueBooks,
                        item.LastIssuanceDate?.ToString("dd.MM.yyyy") ?? "Нет выдач"
                    });

                    CustomMessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ExportFundReportToExcel(ObservableCollection<BookFundReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Фонд_библиотеки_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToExcelGeneric(data, saveFileDialog.FileName, new[]
                    {
                        "Инв. номер", "Название", "Автор", "Издательство", "Год",
                        "Жанр", "Статус", "Цена", "Всего", "Доступно", "В использовании"
                    }, item => new object[]
                    {
                        item.InventoryNumber,
                        item.Title,
                        item.AuthorName,
                        item.Publisher,
                        item.Year,
                        item.Genre,
                        item.Status,
                        item.Price.HasValue ? item.Price.Value.ToString("F2") : "0.00",
                        item.TotalCopies,
                        item.AvailableCopies,
                        item.CopiesInUse
                    });

                    CustomMessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ExportInfoServiceToExcel(ObservableCollection<InfoServiceReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Информационное_обслуживание_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToExcelGeneric(data, saveFileDialog.FileName, new[]
                    {
                        "ID", "Дата", "Выдано книг", "Выдано справок", "Обслужено на мероприятиях (чел.)"
                    }, item => new object[]
                    {
                        item.IdRecord,
                        item.DisplayDate,
                        item.DeliveryBook,
                        item.DeliveryCertificate,
                        item.EventsCount
                    });

                    CustomMessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ExportToExcelGeneric<T>(ObservableCollection<T> data, string filePath, string[] headers, Func<T, object[]> getValues)
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
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1] = headers[i];
                    var range = (Excel.Range)worksheet.Cells[1, i + 1];
                    range.Font.Bold = true;
                }

                // Данные
                int row = 2;
                foreach (var item in data)
                {
                    var values = getValues(item);
                    for (int col = 0; col < values.Length; col++)
                    {
                        worksheet.Cells[row, col + 1] = values[col];
                    }
                    row++;
                }

                // Итоговая строка
                worksheet.Cells[row, 1] = "Итого:";
                var итогRange = (Excel.Range)worksheet.Cells[row, 1];
                итогRange.Font.Bold = true;

                worksheet.Columns.AutoFit();
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                if (worksheet != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
            }
        }
    }

    // Модели данных для отчетов
    public class BookIssuanceReport
    {
        public DateTime IssuanceDate { get; set; }
        public string BookTitle { get; set; } = "";
        public string InventoryNumber { get; set; } = "";
        public string ReaderName { get; set; } = "";
        public string ReaderType { get; set; } = "";
        public string GroupOrPost { get; set; } = "";
        public DateTime PlannedReturnDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string Status { get; set; } = "";
        public int DaysOverdue { get; set; }
    }

    public class ReaderActivityReport
    {
        public int ReaderID { get; set; }
        public string CardNumber { get; set; } = "";
        public string FullName { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string GroupOrPost { get; set; } = "";
        public int BooksIssued { get; set; }
        public int BooksReturned { get; set; }
        public int CurrentlyIssued { get; set; }
        public int OverdueBooks { get; set; }
        public DateTime? LastIssuanceDate { get; set; }
    }

    public class BookFundReport
    {
        public int IDBook { get; set; }
        public string InventoryNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Publisher { get; set; } = "";
        public int Year { get; set; }
        public string Genre { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal? Price { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public int CopiesInUse { get; set; }
    }

    public class InfoServiceReport
    {
        public int IdRecord { get; set; }
        public DateTime DateRecord { get; set; }
        public int DeliveryBook { get; set; }
        public int DeliveryCertificate { get; set; }
        public int EventsCount { get; set; }
        public string DisplayDate => DateRecord.ToString("dd.MM.yyyy");
    }
}