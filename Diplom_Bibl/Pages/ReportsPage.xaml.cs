using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Excel = Microsoft.Office.Interop.Excel;

namespace Diplom_Bibl.Pages
{
    public partial class ReportsPage : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private bool _isLoading = false;

        public ReportsPage()
        {
            InitializeComponent();

            // Устанавливаем значения по умолчанию
            StartDatePicker.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            EndDatePicker.SelectedDate = DateTime.Now.Date;

            // Подписываемся на события изменения дат (если ещё не сделано)
            StartDatePicker.SelectedDateChanged += StartDatePicker_SelectedDateChanged;
            EndDatePicker.SelectedDateChanged += EndDatePicker_SelectedDateChanged;
        }

        // ========== ПРОВЕРКА ДАТ ==========

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

                // Опционально: автоматически корректируем, если пользователь ошибся
                EndDatePicker.SelectedDate = start;
                return false;
            }

            return true;
        }


        // ========== ОТЧЁТ 1: ВЫДАЧА ЛИТЕРАТУРЫ ==========

        private async void ViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadBookIssuanceReportAsync(
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value);

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ShowReportWindow(data, "Отчёт по выдаче литературы",
                    StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<ObservableCollection<BookIssuanceReport>> LoadBookIssuanceReportAsync(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<BookIssuanceReport>();

            string query = @"
        -- Индивидуальные выдачи
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
            END AS DaysOverdue,
            'Индивидуальная' AS IssuanceType
        FROM BookIssuance i
        INNER JOIN Books b ON i.BookId = b.IDBook
        INNER JOIN ReaderForm r ON i.ReaderId = r.ReaderID
        INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
        LEFT JOIN Groups g ON r.GroupID = g.GroupID
        LEFT JOIN Post p ON r.Post = p.ID_Post
        WHERE CAST(i.IssuanceDate AS DATE) BETWEEN @StartDate AND @EndDate
        
        UNION ALL
        
        -- Групповые выдачи (детализированные по книгам)
        SELECT 
            gi.IssuanceDate,
            b.TitleBook,
            b.InventaryNumber,
            CONCAT(t.LastName, ' ', t.FirstName, ' ', ISNULL(t.MiddleName, '')) + ' (преподаватель)' AS ReaderName,
            g.GroupCode AS GroupOrPost,
            gi.PlannedReturnDate,
            gi.ReturnDate,
            gi.Status,
            CASE 
                WHEN gi.ReturnDate IS NULL AND gi.PlannedReturnDate < GETDATE() 
                THEN DATEDIFF(DAY, gi.PlannedReturnDate, GETDATE())
                WHEN gi.ReturnDate > gi.PlannedReturnDate
                THEN DATEDIFF(DAY, gi.PlannedReturnDate, gi.ReturnDate)
                ELSE 0
            END AS DaysOverdue,
            'Групповая' AS IssuanceType
        FROM GroupBookIssuance gi
        INNER JOIN Groups g ON gi.GroupId = g.GroupID
        INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId
        INNER JOIN Books b ON gii.BookId = b.IDBook
        LEFT JOIN ReaderForm t ON gi.TeacherId = t.ReaderID
        WHERE CAST(gi.IssuanceDate AS DATE) BETWEEN @StartDate AND @EndDate
        
        ORDER BY IssuanceDate DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", start.Date);
                    cmd.Parameters.AddWithValue("@EndDate", end.Date);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                                DaysOverdue = reader.GetInt32(8),
                                IssuanceType = reader.GetString(9)  // Добавляем тип выдачи
                            };
                            data.Add(report);
                        }
                    }
                }
            }

            return data;
        }

        // ========== ОТЧЁТ 2: ЧИТАТЕЛЬСКАЯ АКТИВНОСТЬ ==========

        private async void ViewReaderActivity_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadReaderActivityReportAsync();

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ShowReportWindow(data, "Читательская активность", null, null);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<ObservableCollection<ReaderActivityReport>> LoadReaderActivityReportAsync()
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

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
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

            return data;
        }

        // ========== ОТЧЁТ 3: ФОНД БИБЛИОТЕКИ ==========

        private async void ViewFundReport_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadBookFundReportAsync();

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ShowReportWindow(data, "Состояние библиотечного фонда", null, null);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<ObservableCollection<BookFundGroupedReport>> LoadBookFundReportAsync()
        {
            var data = new ObservableCollection<BookFundGroupedReport>();

            string query = @"
        WITH IssuanceStats AS (
            -- Индивидуальные выдачи
            SELECT 
                b.TitleBook,
                COUNT(*) AS IssuedCount
            FROM BookIssuance bi
            INNER JOIN Books b ON bi.BookId = b.IDBook
            WHERE bi.ReturnDate IS NULL AND bi.Status = 'Выдана'
            GROUP BY b.TitleBook
            
            UNION ALL
            
            -- Групповые выдачи
            SELECT 
                b.TitleBook,
                SUM(gii.Quantity - gii.ReturnedQuantity) AS IssuedCount
            FROM GroupBookIssuance gbi
            INNER JOIN GroupIssuanceItems gii ON gbi.GroupIssuanceId = gii.GroupIssuanceId
            INNER JOIN Books b ON gii.BookId = b.IDBook
            WHERE gbi.ReturnDate IS NULL AND gbi.Status = 'Выдана'
            GROUP BY b.TitleBook
        ),
        TotalIssued AS (
            SELECT 
                TitleBook,
                SUM(IssuedCount) AS TotalIssued
            FROM IssuanceStats
            GROUP BY TitleBook
        ),
        DisposedStats AS (
            SELECT 
                TitleBook,
                COUNT(*) AS DisposedCount
            FROM Books
            WHERE StatusBook = 5
            GROUP BY TitleBook
        )
        SELECT 
            b.TitleBook,
            MAX(CONCAT(a.SurnameAuthor, ' ', a.NameAuthor, ' ', ISNULL(a.PatronymicAuthor, ''))) AS AuthorName,
            MAX(ISNULL(ph.TitlePublishingHouse, '')) AS Publisher,
            MAX(b.YearPublishing) AS Year,
            MAX(ISNULL(g.TitleGener, '')) AS Genre,
            COUNT(*) AS TotalCopies,
            COUNT(*) - ISNULL(MAX(ti.TotalIssued), 0) - ISNULL(MAX(ds.DisposedCount), 0) AS AvailableCopies,
            ISNULL(MAX(ti.TotalIssued), 0) AS CopiesInUse,
            ISNULL(MAX(ds.DisposedCount), 0) AS DisposedCopies
        FROM Books b
        INNER JOIN Authors a ON b.Author = a.ID_Autor
        LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
        LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
        LEFT JOIN TotalIssued ti ON b.TitleBook = ti.TitleBook
        LEFT JOIN DisposedStats ds ON b.TitleBook = ds.TitleBook
        GROUP BY b.TitleBook
        ORDER BY b.TitleBook";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
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
                            CopiesInUse = reader.GetInt32(7),
                            DisposedCopies = reader.GetInt32(8)
                        };
                        data.Add(report);
                    }
                }
            }

            return data;
        }

        // ========== ОТЧЁТ 4: ИНФОРМАЦИОННОЕ ОБСЛУЖИВАНИЕ ==========

        private async void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadInfoServiceReportAsync(
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value);

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ShowInfoServiceReportWindow(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<ObservableCollection<InfoServiceReport>> LoadInfoServiceReportAsync(DateTime start, DateTime end)
        {
            var data = new ObservableCollection<InfoServiceReport>();

            string query = @"
        -- Выдачи за период (индивидуальные)
        SELECT 
            CAST(IssuanceDate AS DATE) AS OperationDate,
            'Выдача' AS OperationType,
            COUNT(*) AS Count
        FROM BookIssuance
        WHERE Status = 'Выдана' 
            AND CAST(IssuanceDate AS DATE) BETWEEN @StartDate AND @EndDate
        GROUP BY CAST(IssuanceDate AS DATE)
        
        UNION ALL
        
        -- Выдачи за период (групповые) - каждая книга отдельно
        SELECT 
            CAST(gi.IssuanceDate AS DATE) AS OperationDate,
            'Выдача' AS OperationType,
            COUNT(*) AS Count
        FROM GroupBookIssuance gi
        INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId
        WHERE gi.Status = 'Выдана'
            AND CAST(gi.IssuanceDate AS DATE) BETWEEN @StartDate AND @EndDate
        GROUP BY CAST(gi.IssuanceDate AS DATE)
        
        UNION ALL
        
        -- Возвраты за период (индивидуальные)
        SELECT 
            CAST(ReturnDate AS DATE) AS OperationDate,
            'Возврат' AS OperationType,
            COUNT(*) AS Count
        FROM BookIssuance
        WHERE ReturnDate IS NOT NULL 
            AND Status = 'Возвращена'
            AND CAST(ReturnDate AS DATE) BETWEEN @StartDate AND @EndDate
        GROUP BY CAST(ReturnDate AS DATE)
        
        UNION ALL
        
        -- Возвраты за период (групповые) - каждая книга отдельно
        SELECT 
            CAST(gi.ReturnDate AS DATE) AS OperationDate,
            'Возврат' AS OperationType,
            COUNT(*) AS Count
        FROM GroupBookIssuance gi
        INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId
        WHERE gi.ReturnDate IS NOT NULL 
            AND gi.Status = 'Возвращена'
            AND CAST(gi.ReturnDate AS DATE) BETWEEN @StartDate AND @EndDate
        GROUP BY CAST(gi.ReturnDate AS DATE)
    ";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Получаем все даты в диапазоне
                var allDates = new List<DateTime>();
                for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
                {
                    allDates.Add(date);
                }

                // Группируем результаты по датам
                var resultDict = allDates.ToDictionary(d => d, d => new { Issued = 0, Returned = 0 });

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", start.Date);
                    cmd.Parameters.AddWithValue("@EndDate", end.Date);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            DateTime opDate = reader.GetDateTime(0);
                            string opType = reader.GetString(1);
                            int count = reader.GetInt32(2);

                            if (resultDict.ContainsKey(opDate))
                            {
                                if (opType == "Выдача")
                                {
                                    resultDict[opDate] = new { Issued = resultDict[opDate].Issued + count, Returned = resultDict[opDate].Returned };
                                }
                                else if (opType == "Возврат")
                                {
                                    resultDict[opDate] = new { Issued = resultDict[opDate].Issued, Returned = resultDict[opDate].Returned + count };
                                }
                            }
                        }
                    }
                }

                // Формируем результат
                foreach (var date in allDates)
                {
                    data.Add(new InfoServiceReport
                    {
                        OperationDate = date,
                        BooksIssued = resultDict[date].Issued,
                        BooksReturned = resultDict[date].Returned
                    });
                }
            }

            return data;
        }

        // ========== МЕТОДЫ ОТОБРАЖЕНИЯ ОКОН ==========

        private void ShowReportWindow(object data, string title, DateTime? startDate, DateTime? endDate)
        {
            var enumerableData = data as IEnumerable;
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
                    case "IssuanceDate":
                        e.Column.Header = "Дата выдачи";
                        if (e.Column is DataGridTextColumn col) col.Binding.StringFormat = "dd.MM.yyyy HH:mm";
                        break;
                    case "BookTitle": e.Column.Header = "Название книги"; break;
                    case "InventoryNumber": e.Column.Header = "Инв. номер"; break;
                    case "ReaderName": e.Column.Header = "Читатель"; break;
                    case "GroupOrPost": e.Column.Header = "Группа/Должность"; break;
                    case "PlannedReturnDate":
                        e.Column.Header = "План. возврат";
                        if (e.Column is DataGridTextColumn col2) col2.Binding.StringFormat = "dd.MM.yyyy";
                        break;
                    case "ReturnDate":
                        e.Column.Header = "Дата возврата";
                        if (e.Column is DataGridTextColumn col3) col3.Binding.StringFormat = "dd.MM.yyyy";
                        break;
                    case "Status": e.Column.Header = "Статус"; break;
                    case "DaysOverdue": e.Column.Header = "Дней просрочки"; break;
                    case "IssuanceType": e.Column.Header = "Тип выдачи"; break;
                    case "CardNumber": e.Column.Header = "№ билета"; break;
                    case "FullName": e.Column.Header = "ФИО"; break;
                    case "TypeName": e.Column.Header = "Тип"; break;
                    case "BooksIssued": e.Column.Header = "Всего выдано"; break;
                    case "BooksReturned": e.Column.Header = "Возвращено"; break;
                    case "CurrentlyIssued": e.Column.Header = "На руках"; break;
                    case "OverdueBooks": e.Column.Header = "Просрочено"; break;
                    case "LastIssuanceDate":
                        e.Column.Header = "Последняя выдача";
                        if (e.Column is DataGridTextColumn col4) col4.Binding.StringFormat = "dd.MM.yyyy";
                        break;
                    case "AuthorName": e.Column.Header = "Автор"; break;
                    case "Publisher": e.Column.Header = "Издательство"; break;
                    case "Year": e.Column.Header = "Год"; break;
                    case "Genre": e.Column.Header = "Жанр"; break;
                    case "TotalCopies": e.Column.Header = "Всего"; break;
                    case "AvailableCopies": e.Column.Header = "Доступно"; break;
                    case "CopiesInUse": e.Column.Header = "В использовании"; break;
                    case "DisposedCopies": e.Column.Header = "Списано"; break;  // 👈 ДОБАВЛЕННАЯ СТРОКА
                    case "Title": e.Column.Header = "Название книги"; break;
                    case "OperationDate":
                        e.Column.Header = "Дата";
                        if (e.Column is DataGridTextColumn col5) col5.Binding.StringFormat = "dd.MM.yyyy";
                        break;
                    default:
                        e.Column.Visibility = Visibility.Collapsed;
                        break;
                }
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            };

            mainPanel.Children.Add(dataGrid);

            // 👇 ДОБАВЛЕННАЯ ИТОГОВАЯ СТРОКА ДЛЯ ОТЧЁТА О ФОНДЕ
            if (title == "Состояние библиотечного фонда" && enumerableData is ObservableCollection<BookFundGroupedReport> fundData && fundData.Any())
            {
                var totalPanel = new Border
                {
                    Background = System.Windows.Media.Brushes.LightGray,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(10, 0, 10, 10)
                };

                int totalAll = fundData.Sum(x => x.TotalCopies);
                int totalAvailable = fundData.Sum(x => x.AvailableCopies);
                int totalInUse = fundData.Sum(x => x.CopiesInUse);
                int totalDisposed = fundData.Sum(x => x.DisposedCopies);

                var totalStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                totalStack.Children.Add(new TextBlock
                {
                    Text = $"ИТОГО: ",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 20, 0)
                });
                totalStack.Children.Add(new TextBlock
                {
                    Text = $"📚 Всего: {totalAll}",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 20, 0),
                    Foreground = System.Windows.Media.Brushes.Blue
                });
                totalStack.Children.Add(new TextBlock
                {
                    Text = $"✅ Доступно: {totalAvailable}",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 20, 0),
                    Foreground = System.Windows.Media.Brushes.Green
                });
                totalStack.Children.Add(new TextBlock
                {
                    Text = $"📤 В использовании: {totalInUse}",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 20, 0),
                    Foreground = System.Windows.Media.Brushes.Orange
                });
                totalStack.Children.Add(new TextBlock
                {
                    Text = $"❌ Списано: {totalDisposed}",
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.Red
                });

                totalPanel.Child = totalStack;
                mainPanel.Children.Add(totalPanel);
            }

            reportWindow.Content = mainPanel;
            reportWindow.ShowDialog();
        }
        private void ShowInfoServiceReportWindow(ObservableCollection<InfoServiceReport> data, DateTime startDate, DateTime endDate)
        {
            int totalIssued = data.Sum(x => x.BooksIssued);
            int totalReturned = data.Sum(x => x.BooksReturned);

            var reportWindow = new Window
            {
                Title = "Информационное обслуживание (Выдача и возврат книг)",
                Width = 900,
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
                Binding = new System.Windows.Data.Binding("BooksIssued"),
                Width = new DataGridLength(120)
            });

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Возвращено книг",
                Binding = new System.Windows.Data.Binding("BooksReturned"),
                Width = new DataGridLength(120)
            });

            mainPanel.Children.Add(dataGrid);

            var totalPanel = new Border
            {
                Background = System.Windows.Media.Brushes.LightGray,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(10, 0, 10, 10)
            };

            var totalStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            totalStack.Children.Add(new TextBlock
            {
                Text = $"ИТОГО ЗА ПЕРИОД: ",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 20, 0)
            });
            totalStack.Children.Add(new TextBlock
            {
                Text = $"📤 Выдано: {totalIssued}",
                FontSize = 13,
                Margin = new Thickness(0, 0, 20, 0),
                Foreground = System.Windows.Media.Brushes.Green
            });
            totalStack.Children.Add(new TextBlock
            {
                Text = $"📥 Возвращено: {totalReturned}",
                FontSize = 13,
                Foreground = System.Windows.Media.Brushes.Orange
            });

            totalPanel.Child = totalStack;
            mainPanel.Children.Add(totalPanel);

            reportWindow.Content = mainPanel;
            reportWindow.ShowDialog();
        }
        // Дополнительный класс для отображения с накопленными итогами
        public class InfoServiceReportWithCumulative : InfoServiceReport
        {
            public int CumulativeIssued { get; set; }
            public int CumulativeReturned { get; set; }
            public int CurrentlyOnHand { get; set; }
        }

        // ========== ЭКСПОРТ В EXCEL ==========

        private async void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadInfoServiceReportAsync(
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value);

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ExportInfoServiceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        private void ExportInfoServiceToExcel(ObservableCollection<InfoServiceReport> data, DateTime startDate, DateTime endDate)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Информационное_обслуживание_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                int totalIssued = data.Sum(x => x.BooksIssued);
                int totalReturned = data.Sum(x => x.BooksReturned);

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

                    worksheet.Cells[currentRow, 1] = "Информационное обслуживание (Выдача и возврат книг)";
                    var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                    titleRange.Font.Bold = true;
                    titleRange.Font.Size = 16;
                    titleRange.Merge(worksheet.Cells[currentRow, 3]);
                    currentRow += 2;

                    worksheet.Cells[currentRow, 1] = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                    currentRow++;

                    worksheet.Cells[currentRow, 1] = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                    currentRow += 2;

                    worksheet.Cells[currentRow, 1] = "Дата";
                    worksheet.Cells[currentRow, 2] = "Выдано книг";
                    worksheet.Cells[currentRow, 3] = "Возвращено книг";
                    var headerRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                    headerRange.Font.Bold = true;
                    headerRange.Interior.Color = System.Drawing.Color.LightGray.ToArgb();
                    currentRow++;

                    foreach (var item in data)
                    {
                        worksheet.Cells[currentRow, 1] = item.DisplayDate;
                        worksheet.Cells[currentRow, 2] = item.BooksIssued;
                        worksheet.Cells[currentRow, 3] = item.BooksReturned;
                        currentRow++;
                    }

                    currentRow++;
                    worksheet.Cells[currentRow, 1] = "ИТОГО:";
                    var totalCell = (Excel.Range)worksheet.Cells[currentRow, 1];
                    totalCell.Font.Bold = true;
                    worksheet.Cells[currentRow, 2] = totalIssued;
                    worksheet.Cells[currentRow, 3] = totalReturned;
                    ((Excel.Range)worksheet.Cells[currentRow, 2]).Font.Bold = true;
                    ((Excel.Range)worksheet.Cells[currentRow, 3]).Font.Bold = true;

                    worksheet.Columns.AutoFit();
                    workbook.SaveAs(saveFileDialog.FileName, Excel.XlFileFormat.xlOpenXMLWorkbook);

                    MessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Освобождаем COM-объекты в правильном порядке
                    if (worksheet != null)
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                    }
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
        // ========== ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ ЭКСПОРТА ДЛЯ ОТЧЁТА 1 ==========

        /// <summary>
        /// Экспорт отчета "Выдача литературы" в Excel
        /// </summary>
        private async void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadBookIssuanceReportAsync(
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value);

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ExportBookIssuanceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Экспорт отчета "Выдача литературы" в PDF (печать)
        /// </summary>
        /// <summary>
        /// Экспорт отчета "Выдача литературы" в PDF (печать)
        /// </summary>
        private async void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates()) return;
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadBookIssuanceReportAsync(
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value);

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных за выбранный период.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                // Создаём копию данных для фонового потока
                var dataCopy = data.ToList();

                await Task.Run(() => CreateAndShowPDFFromCopy(
                    dataCopy,
                    "Отчёт_Выдача_литературы",
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value,
                    new[]
                    {
                "Дата выдачи", "Название книги", "Инв. номер", "Читатель",
                "Группа/Должность", "План. возврат", "Дата возврата", "Статус", "Дней просрочки"
                    },
                    item => new string[]
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
                    }
                ));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex.InnerException is InvalidOperationException)
            {
                CustomMessageBox.Show(
                    "Ошибка при создании PDF: невозможно работать с UI‑элементами в фоновом потоке. Попробуйте ещё раз.",
                    "Ошибка печати",
                    CustomMessageBoxButton.OK,
                    CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка печати: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }
        private void CreateAndShowPDFFromCopy<T>(List<T> data, string reportName, DateTime? startDate, DateTime? endDate, string[] headers, Func<T, string[]> getValues)
        {
            string tempFile = Path.GetTempFileName() + ".pdf";

            try
            {
                using (PdfDocument document = new PdfDocument())
                {
                    document.Info.Title = reportName;
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

                        double[] columnWidths = new double[headers.Length];
                        for (int i = 0; i < headers.Length; i++)
                        {
                            columnWidths[i] = 80;
                        }

                        gfx.DrawString(reportName, titleFont, XBrushes.Black, new XRect(0, yPoint, page.Width.Point, 30), XStringFormats.TopCenter);
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
                                string displayText = values[i];
                                if (displayText.Length > 20) displayText = displayText.Substring(0, 17) + "...";
                                gfx.DrawString(displayText, dataFont, XBrushes.Black,
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

                    document.Save(tempFile);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания PDF: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Экспорт отчета "Выдача литературы" в Excel (вспомогательный метод)
        /// </summary>
        private void ExportBookIssuanceToExcel(ObservableCollection<BookIssuanceReport> data, DateTime startDate, DateTime endDate)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Выдача_литературы_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
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
                    worksheet.Name = "Выдача литературы";

                    int currentRow = 1;

                    worksheet.Cells[currentRow, 1] = "Отчёт по выдаче литературы";
                    var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                    titleRange.Font.Bold = true;
                    titleRange.Font.Size = 16;
                    titleRange.Merge(worksheet.Cells[currentRow, 9]);
                    currentRow += 2;

                    worksheet.Cells[currentRow, 1] = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                    currentRow++;

                    worksheet.Cells[currentRow, 1] = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                    currentRow += 2;

                    string[] headers = { "Дата выдачи", "Название книги", "Инв. номер", "Читатель",
                "Группа/Должность", "План. возврат", "Дата возврата", "Статус", "Дней просрочки" };

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
                        worksheet.Cells[currentRow, 1] = item.IssuanceDate.ToString("dd.MM.yyyy HH:mm");
                        worksheet.Cells[currentRow, 2] = item.BookTitle;
                        worksheet.Cells[currentRow, 3] = item.InventoryNumber;
                        worksheet.Cells[currentRow, 4] = item.ReaderName;
                        worksheet.Cells[currentRow, 5] = item.GroupOrPost;
                        worksheet.Cells[currentRow, 6] = item.PlannedReturnDate.ToString("dd.MM.yyyy");
                        worksheet.Cells[currentRow, 7] = item.ReturnDate?.ToString("dd.MM.yyyy") ?? "Не возвращена";
                        worksheet.Cells[currentRow, 8] = item.Status;
                        worksheet.Cells[currentRow, 9] = item.DaysOverdue;
                        currentRow++;
                    }

                    worksheet.Columns.AutoFit();
                    workbook.SaveAs(saveFileDialog.FileName, Excel.XlFileFormat.xlOpenXMLWorkbook);

                    MessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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

        // ========== ЭКСПОРТ ДЛЯ ОТЧЁТА 2 (ЧИТАТЕЛЬСКАЯ АКТИВНОСТЬ) ==========

        /// <summary>
        /// Экспорт отчета "Читательская активность" в Excel
        /// </summary>
        private async void ExportReaderActivityExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadReaderActivityReportAsync();

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                ExportReaderActivityToExcel(data);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Экспорт отчета "Читательская активность" в PDF
        /// </summary>
        private async void ExportReaderActivityPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadReaderActivityReportAsync();

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных о читателях.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                await Task.Run(() => CreateAndShowPDF(data, "Отчёт_Читательская_активность", null, null, new[]
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
        }));
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка экспорта PDF: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Экспорт отчета "Читательская активность" в Excel (вспомогательный метод)
        /// </summary>
        private void ExportReaderActivityToExcel(ObservableCollection<ReaderActivityReport> data)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчёт_Читательская_активность_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
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
                    worksheet.Name = "Читательская активность";

                    int currentRow = 1;

                    worksheet.Cells[currentRow, 1] = "Отчёт по читательской активности";
                    var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                    titleRange.Font.Bold = true;
                    titleRange.Font.Size = 16;
                    titleRange.Merge(worksheet.Cells[currentRow, 9]);
                    currentRow += 2;

                    worksheet.Cells[currentRow, 1] = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                    currentRow += 2;

                    string[] headers = { "№ билета", "ФИО", "Тип", "Группа/Должность", "Всего выдано",
                "Возвращено", "На руках", "Просрочено", "Последняя выдача" };

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
                        worksheet.Cells[currentRow, 1] = item.CardNumber;
                        worksheet.Cells[currentRow, 2] = item.FullName;
                        worksheet.Cells[currentRow, 3] = item.TypeName;
                        worksheet.Cells[currentRow, 4] = item.GroupOrPost;
                        worksheet.Cells[currentRow, 5] = item.BooksIssued;
                        worksheet.Cells[currentRow, 6] = item.BooksReturned;
                        worksheet.Cells[currentRow, 7] = item.CurrentlyIssued;
                        worksheet.Cells[currentRow, 8] = item.OverdueBooks;
                        worksheet.Cells[currentRow, 9] = item.LastIssuanceDate?.ToString("dd.MM.yyyy") ?? "Нет выдач";
                        currentRow++;
                    }

                    worksheet.Columns.AutoFit();
                    workbook.SaveAs(saveFileDialog.FileName, Excel.XlFileFormat.xlOpenXMLWorkbook);

                    MessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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

        // ========== ЭКСПОРТ ДЛЯ ОТЧЁТА 3 (ФОНД БИБЛИОТЕКИ) ==========

        /// <summary>
        /// Экспорт отчета "Фонд библиотеки" в PDF
        /// </summary>
        private async void ExportFundReportPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                Mouse.OverrideCursor = Cursors.Wait;

                var data = await LoadBookFundReportAsync();

                if (data.Count == 0)
                {
                    CustomMessageBox.Show("Нет данных о книгах.", "Информация",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    return;
                }

                await Task.Run(() => CreateAndShowPDF(data, "Отчёт_Фонд_библиотеки", null, null, new[]
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
        }));
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка экспорта PDF: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                Mouse.OverrideCursor = null;
            }
        }

        // ========== МЕТОДЫ СОЗДАНИЯ PDF ==========

        private void CreateAndShowPDF<T>(ObservableCollection<T> data, string reportName, DateTime? startDate, DateTime? endDate, string[] headers, Func<T, string[]> getValues)
        {
            string tempFile = Path.GetTempFileName() + ".pdf";

            try
            {
                using (PdfDocument document = new PdfDocument())
                {
                    document.Info.Title = reportName;
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

                        double[] columnWidths = new double[headers.Length];
                        for (int i = 0; i < headers.Length; i++)
                        {
                            columnWidths[i] = 80;
                        }

                        gfx.DrawString(reportName, titleFont, XBrushes.Black, new XRect(0, yPoint, page.Width.Point, 30), XStringFormats.TopCenter);
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
                                string displayText = values[i];
                                if (displayText.Length > 20) displayText = displayText.Substring(0, 17) + "...";
                                gfx.DrawString(displayText, dataFont, XBrushes.Black,
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

                    document.Save(tempFile);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания PDF: {ex.Message}", ex);
            }
        }
        private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                // Автоматически проверяем корректность дат при изменении
                ValidateDates();
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                ValidateDates();
            }
        }

    }

    // ========== МОДЕЛИ ДАННЫХ ==========

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
        public string IssuanceType { get; set; } = ""; 
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
        public int DisposedCopies { get; set; }

    }

    public class InfoServiceReport
    {
        public DateTime OperationDate { get; set; }
        public int BooksIssued { get; set; }
        public int BooksReturned { get; set; }
        public string DisplayDate => OperationDate.ToString("dd.MM.yyyy");
    }
}