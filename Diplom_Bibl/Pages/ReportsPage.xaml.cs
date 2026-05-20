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
using PdfSharp.Pdf;
using PdfSharp.Drawing;

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

        //  ОТЧЁТ 1: ВЫДАЧА ЛИТЕРАТУРЫ 
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

            ShowReportWindow(data, "Отчёт по выдаче литературы", StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
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

            ExportBookIssuanceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
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

            CreateAndShowPDF(data, "Отчёт_Выдача_литературы", StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value, new[]
            {
                "Дата выдачи", "Название книги", "Инв. номер", "Читатель",
                "Группа/Должность", "План. возврат", "Дата возврата", "Статус", "Дней просрочки"
            }, item => new string[]
            {
                ((BookIssuanceReport)item).IssuanceDate.ToString("dd.MM.yyyy HH:mm"),
                ((BookIssuanceReport)item).BookTitle,
                ((BookIssuanceReport)item).InventoryNumber,
                ((BookIssuanceReport)item).ReaderName,
                ((BookIssuanceReport)item).GroupOrPost,
                ((BookIssuanceReport)item).PlannedReturnDate.ToString("dd.MM.yyyy"),
                ((BookIssuanceReport)item).ReturnDate?.ToString("dd.MM.yyyy") ?? "Не возвращена",
                ((BookIssuanceReport)item).Status,
                ((BookIssuanceReport)item).DaysOverdue.ToString()
            });
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
                                var report = new BookIssuanceReport
                                {
                                    IssuanceDate = reader.GetDateTime(0),
                                    BookTitle = reader.GetString(1),
                                    InventoryNumber = reader.GetString(2),
                                    ReaderName = reader.GetString(3),
                                    GroupOrPost = reader.GetString(4),
                                    PlannedReturnDate = reader.GetDateTime(5),
                                    ReturnDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                    Status = reader.GetString(7),
                                    DaysOverdue = reader.GetInt32(8)
                                };
                                data.Add(report);
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

        //  ОТЧЁТ 2: ЧИТАТЕЛЬСКАЯ АКТИВНОСТЬ 
        private void ViewReaderActivity_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadReaderActivityReport();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Читательская активность", null, null);
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

            CreateAndShowPDF(data, "Отчёт_Читательская_активность", null, null, new[]
            {
                "№ билета", "ФИО", "Тип", "Группа/Должность", "Всего выдано",
                "Возвращено", "На руках", "Просрочено", "Последняя выдача"
            }, item => new string[]
            {
                ((ReaderActivityReport)item).CardNumber,
                ((ReaderActivityReport)item).FullName,
                ((ReaderActivityReport)item).TypeName,
                ((ReaderActivityReport)item).GroupOrPost,
                ((ReaderActivityReport)item).BooksIssued.ToString(),
                ((ReaderActivityReport)item).BooksReturned.ToString(),
                ((ReaderActivityReport)item).CurrentlyIssued.ToString(),
                ((ReaderActivityReport)item).OverdueBooks.ToString(),
                ((ReaderActivityReport)item).LastIssuanceDate?.ToString("dd.MM.yyyy") ?? "Нет выдач"
            });
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

                            var report = new ReaderActivityReport
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
                            };
                            data.Add(report);
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

        //  ОТЧЁТ 3: ФОНД БИБЛИОТЕКИ (С ГРУППИРОВКОЙ) 
        private void ViewFundReport_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadBookFundReportGrouped();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            ShowReportWindow(data, "Состояние библиотечного фонда", null, null);
        }

        private void ExportFundReportPDF_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadBookFundReportGrouped();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                return;
            }

            CreateAndShowPDF(data, "Отчёт_Фонд_библиотеки", null, null, new[]
            {
                "Название книги", "Автор", "Издательство", "Год", "Жанр",
                "Всего", "Доступно", "В использовании"
            }, item => new string[]
            {
                ((BookFundGroupedReport)item).Title,
                ((BookFundGroupedReport)item).AuthorName,
                ((BookFundGroupedReport)item).Publisher,
                ((BookFundGroupedReport)item).Year.ToString(),
                ((BookFundGroupedReport)item).Genre,
                ((BookFundGroupedReport)item).TotalCopies.ToString(),
                ((BookFundGroupedReport)item).AvailableCopies.ToString(),
                ((BookFundGroupedReport)item).CopiesInUse.ToString()
            });
        }

        private ObservableCollection<BookFundGroupedReport> LoadBookFundReportGrouped()
        {
            var data = new ObservableCollection<BookFundGroupedReport>();

            string query = @"
                SELECT 
                    b.TitleBook,
                    MAX(CONCAT(a.SurnameAuthor, ' ', a.NameAuthor, ' ', ISNULL(a.PatronymicAuthor, ''))) AS AuthorName,
                    MAX(ISNULL(ph.TitlePublishingHouse, '')) AS Publisher,
                    MAX(b.YearPublishing) AS Year,
                    MAX(ISNULL(g.TitleGener, '')) AS Genre,
                    SUM(b.TotalCopies) AS TotalCopies,
                    SUM(b.AvailableCopies) AS AvailableCopies,
                    SUM(b.CopiesInUse) AS CopiesInUse
                FROM Books b
                INNER JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
                LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                GROUP BY b.TitleBook
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
                            var report = new BookFundGroupedReport
                            {
                                Title = reader.GetString(0),
                                AuthorName = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                Year = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                TotalCopies = reader.GetInt32(5),
                                AvailableCopies = reader.GetInt32(6),
                                CopiesInUse = reader.GetInt32(7)
                            };
                            data.Add(report);
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

        //  ОТЧЁТ 4: ИНФОРМАЦИОННОЕ ОБСЛУЖИВАНИЕ 
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

            var dataWithTotal = AddTotalRow(data);
            ShowInfoServiceReportWindow(dataWithTotal, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
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

            ExportInfoServiceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
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
                                var report = new InfoServiceReport
                                {
                                    IdRecord = reader.GetInt32(0),
                                    DateRecord = reader.GetDateTime(1),
                                    DeliveryBook = reader.GetInt32(2),
                                    DeliveryCertificate = reader.GetInt32(3),
                                    EventsCount = reader.GetInt32(4)
                                };
                                data.Add(report);
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

        private ObservableCollection<InfoServiceReportWithTotal> AddTotalRow(ObservableCollection<InfoServiceReport> data)
        {
            var result = new ObservableCollection<InfoServiceReportWithTotal>();

            foreach (var item in data)
            {
                result.Add(new InfoServiceReportWithTotal
                {
                    DateRecord = item.DateRecord,
                    DeliveryBook = item.DeliveryBook,
                    DeliveryCertificate = item.DeliveryCertificate,
                    EventsCount = item.EventsCount,
                    IsTotal = false
                });
            }

            int totalBooks = data.Sum(x => x.DeliveryBook);
            int totalCertificates = data.Sum(x => x.DeliveryCertificate);
            int totalEvents = data.Sum(x => x.EventsCount);

            result.Add(new InfoServiceReportWithTotal
            {
                DateRecord = DateTime.MinValue,
                DeliveryBook = totalBooks,
                DeliveryCertificate = totalCertificates,
                EventsCount = totalEvents,
                IsTotal = true
            });

            return result;
        }

        private void ShowInfoServiceReportWindow(ObservableCollection<InfoServiceReportWithTotal> data, DateTime startDate, DateTime endDate)
        {
            var reportWindow = new Window
            {
                Title = "Информационное обслуживание",
                Width = 1000,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var mainPanel = new StackPanel();

            var headerPanel = new StackPanel { Margin = new Thickness(10, 10, 10, 5) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Информационное обслуживание",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 3, 0, 0)
            });

            mainPanel.Children.Add(headerPanel);

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Margin = new Thickness(10),
                ItemsSource = data,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Дата",
                Binding = new System.Windows.Data.Binding("DisplayDate"),
                Width = new DataGridLength(120)
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Выдано книг",
                Binding = new System.Windows.Data.Binding("DeliveryBook"),
                Width = new DataGridLength(100)
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Выдано справок",
                Binding = new System.Windows.Data.Binding("DeliveryCertificate"),
                Width = new DataGridLength(100)
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Обслужено на мероприятиях (чел.)",
                Binding = new System.Windows.Data.Binding("EventsCount"),
                Width = new DataGridLength(180)
            });

            dataGrid.LoadingRow += (s, e) =>
            {
                var item = e.Row.DataContext as InfoServiceReportWithTotal;
                if (item != null && item.IsTotal)
                {
                    e.Row.Background = System.Windows.Media.Brushes.LightGray;
                    e.Row.FontWeight = FontWeights.Bold;
                }
            };

            mainPanel.Children.Add(dataGrid);
            reportWindow.Content = mainPanel;
            reportWindow.ShowDialog();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new InventoryActReport());
        }

        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ 
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

        private void ShowReportWindow(object data, string title, DateTime? startDate, DateTime? endDate)
        {
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
                Width = 1300,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var mainPanel = new StackPanel();

            var headerPanel = new StackPanel { Margin = new Thickness(10, 10, 10, 5) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            if (startDate.HasValue && endDate.HasValue)
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"Период: с {startDate.Value:dd.MM.yyyy} по {endDate.Value:dd.MM.yyyy}",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
            }

            headerPanel.Children.Add(new TextBlock
            {
                Text = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 3, 0, 0)
            });

            mainPanel.Children.Add(headerPanel);

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = true,
                IsReadOnly = true,
                Margin = new Thickness(10),
                ItemsSource = enumerableData,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            dataGrid.AutoGeneratingColumn += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case "IssuanceDate": e.Column.Header = "Дата выдачи"; break;
                    case "BookTitle": e.Column.Header = "Название книги"; break;
                    case "InventoryNumber": e.Column.Header = "Инв. номер"; break;
                    case "ReaderName": e.Column.Header = "Читатель"; break;
                    case "GroupOrPost": e.Column.Header = "Группа/Должность"; break;
                    case "PlannedReturnDate": e.Column.Header = "План. возврат"; break;
                    case "ReturnDate": e.Column.Header = "Дата возврата"; break;
                    case "Status": e.Column.Header = "Статус"; break;
                    case "DaysOverdue": e.Column.Header = "Дней просрочки"; break;
                    case "CardNumber": e.Column.Header = "№ билета"; break;
                    case "FullName": e.Column.Header = "ФИО"; break;
                    case "TypeName": e.Column.Header = "Тип"; break;
                    case "BooksIssued": e.Column.Header = "Всего выдано"; break;
                    case "BooksReturned": e.Column.Header = "Возвращено"; break;
                    case "CurrentlyIssued": e.Column.Header = "На руках"; break;
                    case "OverdueBooks": e.Column.Header = "Просрочено"; break;
                    case "LastIssuanceDate": e.Column.Header = "Последняя выдача"; break;
                    case "AuthorName": e.Column.Header = "Автор"; break;
                    case "Publisher": e.Column.Header = "Издательство"; break;
                    case "Year": e.Column.Header = "Год"; break;
                    case "Genre": e.Column.Header = "Жанр"; break;
                    case "TotalCopies": e.Column.Header = "Всего"; break;
                    case "AvailableCopies": e.Column.Header = "Доступно"; break;
                    case "CopiesInUse": e.Column.Header = "В использовании"; break;
                    case "Title": e.Column.Header = "Название книги"; break;
                    case "ReaderID": e.Column.Visibility = Visibility.Collapsed; break;
                    case "IDBook": e.Column.Visibility = Visibility.Collapsed; break;
                    case "IdRecord": e.Column.Visibility = Visibility.Collapsed; break;
                    case "IsTotal": e.Column.Visibility = Visibility.Collapsed; break;
                    case "DisplayDate": e.Column.Header = "Дата"; break;
                    case "DeliveryBook": e.Column.Header = "Выдано книг"; break;
                    case "DeliveryCertificate": e.Column.Header = "Выдано справок"; break;
                    case "EventsCount": e.Column.Header = "Обслужено на мероприятиях (чел.)"; break;
                }

                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                if (e.PropertyName == "IssuanceDate" || e.PropertyName == "PlannedReturnDate" || e.PropertyName == "DateRecord")
                {
                    if (e.Column is DataGridTextColumn textColumn)
                    {
                        textColumn.Binding.StringFormat = "dd.MM.yyyy HH:mm";
                    }
                }
                else if (e.PropertyName == "ReturnDate" || e.PropertyName == "LastIssuanceDate")
                {
                    if (e.Column is DataGridTextColumn textColumn)
                    {
                        textColumn.Binding.StringFormat = "dd.MM.yyyy";
                    }
                }
            };

            mainPanel.Children.Add(dataGrid);
            reportWindow.Content = mainPanel;
            reportWindow.ShowDialog();
        }

        //  ГЕНЕРАЦИЯ PDF 

        private void CreateAndShowPDF<T>(ObservableCollection<T> data, string reportName, DateTime? startDate, DateTime? endDate, string[] headers, Func<T, string[]> getValues)
        {
            string tempFile = Path.GetTempFileName() + ".pdf";

            try
            {
                GeneratePDFWithHeader(data, tempFile, reportName, startDate, endDate, headers, getValues);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка создания PDF: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void GeneratePDFWithHeader<T>(ObservableCollection<T> data, string filePath, string title, DateTime? startDate, DateTime? endDate, string[] headers, Func<T, string[]> getValues)
        {
            using (PdfDocument document = new PdfDocument())
            {
                document.Info.Title = title;
                document.Info.Author = "Библиотечная система";
                document.Info.CreationDate = DateTime.Now;

                PdfPage page = document.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    XFont titleFont = new XFont("Arial", 16, XFontStyle.Bold);
                    XFont headerFont = new XFont("Arial", 10, XFontStyle.Bold);
                    XFont dataFont = new XFont("Arial", 9, XFontStyle.Regular);
                    XFont italicFont = new XFont("Arial", 9, XFontStyle.Italic);

                    double yPoint = 40;
                    double leftMargin = 30;
                    double rowHeight = 25;

                    double[] columnWidths = CalculateAutoColumnWidths(gfx, headers, data, getValues, dataFont, page.Width.Point - 60);

                    gfx.DrawString(title, titleFont, XBrushes.Black, new XRect(0, yPoint, page.Width.Point, 30), XStringFormats.TopCenter);
                    yPoint += 35;

                    if (startDate.HasValue && endDate.HasValue)
                    {
                        string periodText = $"Период: с {startDate.Value:dd.MM.yyyy} по {endDate.Value:dd.MM.yyyy}";
                        gfx.DrawString(periodText, italicFont, XBrushes.Gray,
                            new XRect(leftMargin, yPoint, page.Width.Point - 60, 20), XStringFormats.TopLeft);
                        yPoint += 18;
                    }

                    string dateGenerated = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                    gfx.DrawString(dateGenerated, italicFont, XBrushes.Gray,
                        new XRect(leftMargin, yPoint, page.Width.Point - 60, 20), XStringFormats.TopLeft);
                    yPoint += 25;

                    double currentX = leftMargin;
                    for (int i = 0; i < headers.Length; i++)
                    {
                        gfx.DrawRectangle(XBrushes.LightGray, currentX, yPoint, columnWidths[i], rowHeight);
                        gfx.DrawString(headers[i], headerFont, XBrushes.Black,
                            new XRect(currentX, yPoint, columnWidths[i], rowHeight), XStringFormats.Center);
                        currentX += columnWidths[i];
                    }
                    yPoint += rowHeight;

                    foreach (var item in data)
                    {
                        string[] values = getValues(item);
                        currentX = leftMargin;

                        for (int i = 0; i < values.Length; i++)
                        {
                            gfx.DrawRectangle(XBrushes.White, currentX, yPoint, columnWidths[i], rowHeight);
                            gfx.DrawString(TruncateString(values[i], (int)(columnWidths[i] / 5)), dataFont, XBrushes.Black,
                                new XRect(currentX + 3, yPoint, columnWidths[i] - 6, rowHeight), XStringFormats.TopLeft);
                            currentX += columnWidths[i];
                        }
                        yPoint += rowHeight;

                        if (yPoint > page.Height.Point - 50)
                        {
                            page = document.AddPage();
                            page.Orientation = PdfSharp.PageOrientation.Landscape;
                            using (XGraphics newGfx = XGraphics.FromPdfPage(page))
                            {
                                yPoint = 40;
                                currentX = leftMargin;
                                for (int i = 0; i < headers.Length; i++)
                                {
                                    newGfx.DrawRectangle(XBrushes.LightGray, currentX, yPoint, columnWidths[i], rowHeight);
                                    newGfx.DrawString(headers[i], headerFont, XBrushes.Black,
                                        new XRect(currentX, yPoint, columnWidths[i], rowHeight), XStringFormats.Center);
                                    currentX += columnWidths[i];
                                }
                                yPoint += rowHeight;
                            }
                        }
                    }
                }

                document.Save(filePath);
            }
        }

        private double[] CalculateAutoColumnWidths<T>(XGraphics gfx, string[] headers, ObservableCollection<T> data, Func<T, string[]> getValues, XFont font, double totalWidth)
        {
            double[] widths = new double[headers.Length];

            for (int i = 0; i < headers.Length; i++)
            {
                widths[i] = gfx.MeasureString(headers[i], font).Width + 10;
            }

            foreach (var item in data)
            {
                string[] values = getValues(item);
                for (int i = 0; i < values.Length && i < headers.Length; i++)
                {
                    double textWidth = gfx.MeasureString(values[i], font).Width + 15;
                    if (textWidth > widths[i])
                    {
                        widths[i] = Math.Min(textWidth, 150);
                    }
                }
            }

            return widths;
        }

        private string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        //  ЭКСПОРТ В EXCEL 
        private void ExportBookIssuanceToExcel(ObservableCollection<BookIssuanceReport> data, DateTime startDate, DateTime endDate, bool openAfterExport = false)
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
                    ExportToExcelGeneric(data, saveFileDialog.FileName, startDate, endDate, "Отчёт по выдаче литературы", new[]
                    {
                        "Дата выдачи", "Название книги", "Инв. номер", "Читатель",
                        "Группа/Должность", "План. возврат", "Дата возврата", "Статус", "Дней просрочки"
                    }, item => new object[]
                    {
                        ((BookIssuanceReport)item).IssuanceDate.ToString("dd.MM.yyyy HH:mm"),
                        ((BookIssuanceReport)item).BookTitle,
                        ((BookIssuanceReport)item).InventoryNumber,
                        ((BookIssuanceReport)item).ReaderName,
                        ((BookIssuanceReport)item).GroupOrPost,
                        ((BookIssuanceReport)item).PlannedReturnDate.ToString("dd.MM.yyyy"),
                        ((BookIssuanceReport)item).ReturnDate?.ToString("dd.MM.yyyy") ?? "Не возвращена",
                        ((BookIssuanceReport)item).Status,
                        ((BookIssuanceReport)item).DaysOverdue
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
                    ExportToExcelGeneric(data, saveFileDialog.FileName, null, null, "Читательская активность", new[]
                    {
                        "№ билета", "ФИО", "Тип", "Группа/Должность", "Всего выдано",
                        "Возвращено", "На руках", "Просрочено", "Последняя выдача"
                    }, item => new object[]
                    {
                        ((ReaderActivityReport)item).CardNumber,
                        ((ReaderActivityReport)item).FullName,
                        ((ReaderActivityReport)item).TypeName,
                        ((ReaderActivityReport)item).GroupOrPost,
                        ((ReaderActivityReport)item).BooksIssued,
                        ((ReaderActivityReport)item).BooksReturned,
                        ((ReaderActivityReport)item).CurrentlyIssued,
                        ((ReaderActivityReport)item).OverdueBooks,
                        ((ReaderActivityReport)item).LastIssuanceDate?.ToString("dd.MM.yyyy") ?? "Нет выдач"
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

        private void ExportInfoServiceToExcel(ObservableCollection<InfoServiceReport> data, DateTime startDate, DateTime endDate)
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
                    ExportInfoServiceToExcelGeneric(data, saveFileDialog.FileName, startDate, endDate);
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

        private void ExportInfoServiceToExcelGeneric(ObservableCollection<InfoServiceReport> data, string filePath, DateTime startDate, DateTime endDate)
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
                worksheet.Name = "Информационное обслуживание";

                int currentRow = 1;

                worksheet.Cells[currentRow, 1] = "Информационное обслуживание";
                var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 16;
                titleRange.Merge(worksheet.Cells[currentRow, 4]);
                currentRow += 2;

                worksheet.Cells[currentRow, 1] = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                currentRow++;

                worksheet.Cells[currentRow, 1] = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                currentRow += 2;

                string[] headers = { "Дата", "Выдано книг", "Выдано справок", "Обслужено на мероприятиях (чел.)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[currentRow, i + 1] = headers[i];
                    var range = (Excel.Range)worksheet.Cells[currentRow, i + 1];
                    range.Font.Bold = true;
                    range.Interior.Color = System.Drawing.Color.LightGray.ToArgb();
                }
                currentRow++;

                int totalBooks = 0;
                int totalCertificates = 0;
                int totalEvents = 0;

                foreach (var item in data)
                {
                    worksheet.Cells[currentRow, 1] = item.DisplayDate;
                    worksheet.Cells[currentRow, 2] = item.DeliveryBook;
                    worksheet.Cells[currentRow, 3] = item.DeliveryCertificate;
                    worksheet.Cells[currentRow, 4] = item.EventsCount;

                    totalBooks += item.DeliveryBook;
                    totalCertificates += item.DeliveryCertificate;
                    totalEvents += item.EventsCount;

                    currentRow++;
                }

                currentRow++;
                worksheet.Cells[currentRow, 1] = "ИТОГО:";
                var totalRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                totalRange.Font.Bold = true;
                worksheet.Cells[currentRow, 2] = totalBooks;
                worksheet.Cells[currentRow, 3] = totalCertificates;
                worksheet.Cells[currentRow, 4] = totalEvents;

                ((Excel.Range)worksheet.Cells[currentRow, 2]).Font.Bold = true;
                ((Excel.Range)worksheet.Cells[currentRow, 3]).Font.Bold = true;
                ((Excel.Range)worksheet.Cells[currentRow, 4]).Font.Bold = true;

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

        private void ExportToExcelGeneric<T>(ObservableCollection<T> data, string filePath, DateTime? startDate, DateTime? endDate, string title, string[] headers, Func<T, object[]> getValues)
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

                int currentRow = 1;

                worksheet.Cells[currentRow, 1] = title;
                var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 16;
                titleRange.Merge(worksheet.Cells[currentRow, headers.Length]);
                currentRow += 2;

                if (startDate.HasValue && endDate.HasValue)
                {
                    worksheet.Cells[currentRow, 1] = $"Период: с {startDate.Value:dd.MM.yyyy} по {endDate.Value:dd.MM.yyyy}";
                    currentRow++;
                }

                worksheet.Cells[currentRow, 1] = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                currentRow += 2;

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[currentRow, i + 1] = headers[i];
                    var range = (Excel.Range)worksheet.Cells[currentRow, i + 1];
                    range.Font.Bold = true;
                    range.Interior.Color = System.Drawing.Color.LightGray.ToArgb();
                }
                currentRow++;

                foreach (var item in data)
                {
                    var values = getValues(item);
                    for (int col = 0; col < values.Length; col++)
                    {
                        worksheet.Cells[currentRow, col + 1] = values[col];
                    }
                    currentRow++;
                }

                if (data.Count > 0)
                {
                    worksheet.Cells[currentRow, 1] = "Итого записей:";
                    worksheet.Cells[currentRow, 2] = data.Count;
                    var итогRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                    итогRange.Font.Bold = true;
                }

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

    public class BookFundGroupedReport
    {
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Publisher { get; set; } = "";
        public int Year { get; set; }
        public string Genre { get; set; } = "";
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

    public class InfoServiceReportWithTotal
    {
        public DateTime DateRecord { get; set; }
        public int DeliveryBook { get; set; }
        public int DeliveryCertificate { get; set; }
        public int EventsCount { get; set; }
        public bool IsTotal { get; set; }
        public string DisplayDate => IsTotal ? "ИТОГО:" : DateRecord.ToString("dd.MM.yyyy");
    }
}