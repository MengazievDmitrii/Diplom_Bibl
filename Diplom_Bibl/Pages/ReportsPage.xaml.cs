using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для страницы формирования и экспорта отчётов (ReportsPage.xaml)
    /// </summary>
    public partial class ReportsPage : Page
    {
        // Строка подключения к базе данных
        private readonly string _connectionString = DatabaseHelper.ConnectionString;

        // Флаг для блокировки интерфейса во время загрузки данных
        private bool _isLoading = false;

        /// <summary>
        /// Конструктор страницы отчетов
        /// </summary>
        public ReportsPage()
        {
            InitializeComponent();

            // Устанавливаем начальную дату: 1-е число текущего месяца
            StartDatePicker.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            // Устанавливаем конечную дату: сегодняшний день
            EndDatePicker.SelectedDate = DateTime.Now.Date;

            // Подписываемся на события изменения дат после установки значений по умолчанию
            StartDatePicker.SelectedDateChanged += StartDatePicker_SelectedDateChanged;
            EndDatePicker.SelectedDateChanged += EndDatePicker_SelectedDateChanged;
        }

        #region ОБРАБОТЧИКИ ВЫБОРА ДАТ И ВАЛИДАЦИЯ

        private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                if (StartDatePicker.SelectedDate.Value.Date > EndDatePicker.SelectedDate.Value.Date)
                {
                    EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.Date;
                }
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                if (StartDatePicker.SelectedDate.Value.Date > EndDatePicker.SelectedDate.Value.Date)
                {
                    StartDatePicker.SelectedDate = EndDatePicker.SelectedDate.Value.Date;
                }
            }
        }

        private bool ValidateDates()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                CustomMessageBox.Show("Пожалуйста, выберите обе даты (с и по).", "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }
            if (StartDatePicker.SelectedDate.Value.Date > EndDatePicker.SelectedDate.Value.Date)
            {
                CustomMessageBox.Show("Дата 'с' не может быть позже даты 'по'.", "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void SetLoadingState(bool loading)
        {
            _isLoading = loading;
            Mouse.OverrideCursor = loading ? Cursors.Wait : null;
        }

        #endregion

        #region 1. ОТЧЕТ: ВЫДАЧА ЛИТЕРАТУРЫ (КНОПКИ)

        private async void ViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates() || _isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadBookIssuanceReportAsync(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных за выбранный период.", "Информация"); return; }
                ShowUniversalReportWindow(data, "Отчёт по выдаче литературы", StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        private async void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates() || _isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadBookIssuanceReportAsync(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                CreateBookIssuancePDF(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка PDF: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        private async void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates() || _isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadBookIssuanceReportAsync(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                ExportBookIssuanceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка Excel: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        #endregion

        #region 2. ОТЧЕТ: ЧИТАТЕЛЬСКАЯ АКТИВНОСТЬ (КНОПКИ)

        /// <summary>
        /// Кнопка «Просмотр» активности читателей
        /// </summary>
        private async void ViewReaderActivity_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadReaderActivityReportAsync();
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для отображения.", "Информация"); return; }
                ShowUniversalReportWindow(data, "Читательская активность", null, null);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        /// <summary>
        /// Кнопка «Экспорт в PDF» для Читательской активности
        /// </summary>
        private async void ExportReaderActivityPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadReaderActivityReportAsync();
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                CreateReaderActivityPDF(data);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка PDF: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        /// <summary>
        /// Кнопка «Экспорт в Excel» для Читательской активности
        /// </summary>
        private async void ExportReaderActivityExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadReaderActivityReportAsync();
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                ExportReaderActivityToExcel(data);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка Excel: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        #endregion

        #region 3. ОТЧЕТ: СОСТОЯНИЕ БИБЛИОТЕЧНОГО ФОНДА (КНОПКИ)

        /// <summary>
        /// Кнопка «Просмотр» состояния библиотечного фонда
        /// </summary>
        private async void ViewFundReport_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadBookFundReportAsync();
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для отображения.", "Информация"); return; }
                ShowUniversalReportWindow(data, "Состояние библиотечного фонда", null, null);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        /// <summary>
        /// Кнопка «Экспорт в PDF» для Состояния библиотечного фонда
        /// </summary>
        private async void ExportFundReportPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadBookFundReportAsync();
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                CreateBookFundPDF(data);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка PDF: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        #endregion

        #region 4. ОТЧЕТ: ИНФОРМАЦИОННОЕ ОБСЛУЖИВАНИЕ ПОЛЬЗОВАТЕЛЕЙ (КНОПКИ)

        /// <summary>
        /// Кнопка «Просмотр» информационного обслуживания
        /// </summary>
        private async void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates() || _isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadInfoServiceReportAsync(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных за выбранный период.", "Информация"); return; }
                ShowUniversalReportWindow(data, "Информационное обслуживание", StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        /// <summary>
        /// Кнопка «Экспорт в Excel» для Информационного обслуживания
        /// </summary>
        private async void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDates() || _isLoading) return;
            try
            {
                SetLoadingState(true);
                var data = await LoadInfoServiceReportAsync(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
                if (data.Count == 0) { CustomMessageBox.Show("Нет данных для экспорта.", "Информация"); return; }
                ExportInfoServiceToExcel(data, StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Ошибка Excel: {ex.Message}", "Ошибка"); }
            finally { SetLoadingState(false); }
        }

        #endregion

        #region АСИНХРОННАЯ ЗАГРУЗКА ДАННЫХ ИЗ БД

        private async Task<List<BookIssuanceReport>> LoadBookIssuanceReportAsync(DateTime start, DateTime end)
        {
            var data = new List<BookIssuanceReport>();
            string query = @"
                SELECT i.IssuanceDate, b.TitleBook, b.InventaryNumber, 
                       CONCAT(r.LastName, ' ', r.FirstName, ' ', ISNULL(r.MiddleName, '')) AS ReaderName,
                       CASE WHEN rt.TypeName = 'Студент' THEN ISNULL(g.GroupCode, '')
                            WHEN rt.TypeName = 'Сотрудник' THEN ISNULL(p.Post_Name, '') ELSE '' END AS GroupOrPost,
                       i.PlannedReturnDate, i.ReturnDate, i.Status,
                       CASE WHEN i.ReturnDate IS NULL AND i.PlannedReturnDate < GETDATE() THEN DATEDIFF(DAY, i.PlannedReturnDate, GETDATE())
                            WHEN i.ReturnDate > i.PlannedReturnDate THEN DATEDIFF(DAY, i.PlannedReturnDate, i.ReturnDate) ELSE 0 END AS DaysOverdue,
                       N'Индивидуальная' AS IssuanceType
                FROM BookIssuance i
                INNER JOIN Books b ON i.BookId = b.IDBook
                INNER JOIN ReaderForm r ON i.ReaderId = r.ReaderID
                INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID 
                LEFT JOIN Groups g ON r.GroupID = g.GroupID
                LEFT JOIN Post p ON r.Post = p.ID_Post
                WHERE i.IssuanceDate >= @StartDate AND i.IssuanceDate < @EndDatePlusOne
                
                UNION ALL
                
                SELECT gi.IssuanceDate, b.TitleBook, b.InventaryNumber,
                       CONCAT(t.LastName, ' ', t.FirstName, ' ', ISNULL(t.MiddleName, '')) + N' (преподаватель)' AS ReaderName,
                       g.GroupCode AS GroupOrPost, gi.PlannedReturnDate, gi.ReturnDate, gi.Status,
                       CASE WHEN gi.ReturnDate IS NULL AND gi.PlannedReturnDate < GETDATE() THEN DATEDIFF(DAY, gi.PlannedReturnDate, GETDATE())
                            WHEN gi.ReturnDate > gi.PlannedReturnDate THEN DATEDIFF(DAY, gi.PlannedReturnDate, gi.ReturnDate) ELSE 0 END AS DaysOverdue,
                       N'Групповая' AS IssuanceType
                FROM GroupBookIssuance gi
                INNER JOIN Groups g ON gi.GroupId = g.GroupID
                INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId
                INNER JOIN Books b ON gii.BookId = b.IDBook
                LEFT JOIN ReaderForm t ON gi.TeacherId = t.ReaderID
                WHERE gi.IssuanceDate >= @StartDate AND gi.IssuanceDate < @EndDatePlusOne
                ORDER BY IssuanceDate DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", start.Date);
                    cmd.Parameters.AddWithValue("@EndDatePlusOne", end.Date.AddDays(1));

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            data.Add(new BookIssuanceReport
                            {
                                IssuanceDate = reader.GetDateTime(0),
                                BookTitle = reader.GetString(1),
                                InventoryNumber = reader.GetString(2),
                                ReaderName = reader.GetString(3),
                                GroupOrPost = reader.GetString(4),
                                PlannedReturnDate = reader.GetDateTime(5),
                                ReturnDate = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                Status = reader.GetString(7),
                                DaysOverdue = reader.GetInt32(8),
                                IssuanceType = reader.GetString(9)
                            });
                        }
                    }
                }
            }
            return data;
        }

        private async Task<List<ReaderActivityReport>> LoadReaderActivityReportAsync()
        {
            var data = new List<ReaderActivityReport>();
            string query = @"
                SELECT r.ReaderID, r.CardNumber, CONCAT(r.LastName, ' ', r.FirstName, ' ', ISNULL(r.MiddleName, '')) AS FullName, 
                       rt.TypeName, CASE WHEN rt.TypeName = 'Студент' THEN ISNULL(g.GroupCode, '') WHEN rt.TypeName = 'Сотрудник' THEN ISNULL(p.Post_Name, '') ELSE '' END AS GroupOrPost,
                       COUNT(CASE WHEN i.Status = 'Возвращена' THEN 1 END) AS BooksReturned, COUNT(CASE WHEN i.Status = 'Выдана' THEN 1 END) AS CurrentlyIssued,
                       SUM(CASE WHEN i.ReturnDate IS NULL AND i.PlannedReturnDate < GETDATE() THEN 1 ELSE 0 END) AS OverdueBooks, MAX(i.IssuanceDate) AS LastIssuanceDate
                FROM ReaderForm r
                INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
                LEFT JOIN Groups g ON r.GroupID = g.GroupID
                LEFT JOIN Post p ON r.Post = p.ID_Post
                LEFT JOIN BookIssuance i ON r.ReaderID = i.ReaderId
                GROUP BY r.ReaderID, r.CardNumber, r.LastName, r.FirstName, r.MiddleName, rt.TypeName, g.GroupCode, p.Post_Name
                ORDER BY CurrentlyIssued DESC, BooksReturned DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int returned = reader.GetInt32(5);
                        int issued = reader.GetInt32(6);
                        data.Add(new ReaderActivityReport
                        {
                            ReaderID = reader.GetInt32(0),
                            CardNumber = reader.GetString(1),
                            FullName = reader.GetString(2),
                            TypeName = reader.GetString(3),
                            GroupOrPost = reader.GetString(4),
                            BooksReturned = returned,
                            CurrentlyIssued = issued,
                            OverdueBooks = reader.GetInt32(7),
                            LastIssuanceDate = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                            BooksIssued = returned + issued
                        });
                    }
                }
            }
            return data;
        }

        private async Task<List<BookFundGroupedReport>> LoadBookFundReportAsync()
        {
            var data = new List<BookFundGroupedReport>();
            string query = @"
                WITH IssuanceStats AS (
                    SELECT b.TitleBook, COUNT(*) AS IssuedCount FROM BookIssuance bi INNER JOIN Books b ON bi.BookId = b.IDBook WHERE bi.ReturnDate IS NULL AND bi.Status = 'Выдана' GROUP BY b.TitleBook
                    UNION ALL
                    SELECT b.TitleBook, SUM(gii.Quantity - gii.ReturnedQuantity) AS IssuedCount FROM GroupBookIssuance gbi INNER JOIN GroupIssuanceItems gii ON gbi.GroupIssuanceId = gii.GroupIssuanceId INNER JOIN Books b ON gii.BookId = b.IDBook WHERE gbi.ReturnDate IS NULL AND gbi.Status = 'Выдана' GROUP BY b.TitleBook
                ),
                TotalIssued AS ( SELECT TitleBook, SUM(IssuedCount) AS TotalIssued FROM IssuanceStats GROUP BY TitleBook ),
                DisposedStats AS ( SELECT TitleBook, COUNT(*) AS DisposedCount FROM Books WHERE StatusBook = 5 GROUP BY TitleBook )
                SELECT b.TitleBook, MAX(CONCAT(a.SurnameAuthor, ' ', a.NameAuthor, ' ', ISNULL(a.PatronymicAuthor, ''))) AS AuthorName, MAX(ISNULL(ph.TitlePublishingHouse, '')) AS Publisher, MAX(b.YearPublishing) AS Year, MAX(ISNULL(g.TitleGener, '')) AS Genre, COUNT(*) AS TotalCopies, COUNT(*) - ISNULL(MAX(ti.TotalIssued), 0) - ISNULL(MAX(ds.DisposedCount), 0) AS AvailableCopies, ISNULL(MAX(ti.TotalIssued), 0) AS CopiesInUse, ISNULL(MAX(ds.DisposedCount), 0) AS DisposedCopies
                FROM Books b
                INNER JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
                LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                LEFT JOIN TotalIssued ti ON b.TitleBook = ti.TitleBook
                LEFT JOIN DisposedStats ds ON b.TitleBook = ds.TitleBook
                GROUP BY b.TitleBook ORDER BY b.TitleBook";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        data.Add(new BookFundGroupedReport
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
                        });
                    }
                }
            }
            return data;
        }

        private async Task<List<InfoServiceReport>> LoadInfoServiceReportAsync(DateTime start, DateTime end)
        {
            var data = new List<InfoServiceReport>();
            string query = @"
                SELECT CAST(IssuanceDate AS DATE) AS OperationDate, 'Выдача' AS OperationType, COUNT(*) AS Count FROM BookIssuance WHERE Status = 'Выдана' AND IssuanceDate BETWEEN @StartDate AND @EndDatePlusOne GROUP BY CAST(IssuanceDate AS DATE)
                UNION ALL
                SELECT CAST(gi.IssuanceDate AS DATE) AS OperationDate, 'Выдача' AS OperationType, COUNT(*) AS Count FROM GroupBookIssuance gi INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId WHERE gi.Status = 'Выдана' AND gi.IssuanceDate BETWEEN @StartDate AND @EndDatePlusOne GROUP BY CAST(gi.IssuanceDate AS DATE)
                UNION ALL
                SELECT CAST(ReturnDate AS DATE) AS OperationDate, 'Возврат' AS OperationType, COUNT(*) AS Count FROM BookIssuance WHERE ReturnDate IS NOT NULL AND Status = 'Возвращена' AND ReturnDate BETWEEN @StartDate AND @EndDatePlusOne GROUP BY CAST(ReturnDate AS DATE)
                UNION ALL
                SELECT CAST(gi.ReturnDate AS DATE) AS OperationDate, 'Возврат' AS OperationType, COUNT(*) AS Count FROM GroupBookIssuance gi INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId WHERE gi.ReturnDate IS NOT NULL AND gi.Status = 'Возвращена' AND gi.ReturnDate BETWEEN @StartDate AND @EndDatePlusOne GROUP BY CAST(gi.ReturnDate AS DATE)";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var allDates = new List<DateTime>();
                for (var date = start.Date; date <= end.Date; date = date.AddDays(1)) allDates.Add(date);

                var resultDict = allDates.ToDictionary(d => d, d => new { Issued = 0, Returned = 0 });

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", start.Date);
                    cmd.Parameters.AddWithValue("@EndDatePlusOne", end.Date.AddDays(1));

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
                                    resultDict[opDate] = new { Issued = resultDict[opDate].Issued + count, Returned = resultDict[opDate].Returned };
                                else if (opType == "Возврат")
                                    resultDict[opDate] = new { Issued = resultDict[opDate].Issued, Returned = resultDict[opDate].Returned + count };
                            }
                        }
                    }
                }

                foreach (var date in allDates)
                {
                    data.Add(new InfoServiceReport { OperationDate = date, BooksIssued = resultDict[date].Issued, BooksReturned = resultDict[date].Returned });
                }
            }
            return data;
        }

        #endregion

        #region ИНТЕРФЕЙС УНИВЕРСАЛЬНОГО ОКНА ПРОСМОТРА

        private void ShowUniversalReportWindow(IEnumerable data, string title, DateTime? startDate, DateTime? endDate)
        {
            var reportWindow = new Window { Title = title, Width = 1100, Height = 700, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            var mainPanel = new Grid();
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerPanel = new StackPanel { Margin = new Thickness(15, 10, 15, 5) };
            headerPanel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.Bold });
            if (startDate.HasValue && endDate.HasValue)
            {
                headerPanel.Children.Add(new TextBlock { Text = $"Период: с {startDate.Value:dd.MM.yyyy} по {endDate.Value:dd.MM.yyyy}", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray });
            }
            Grid.SetRow(headerPanel, 0); mainPanel.Children.Add(headerPanel);

            var dataGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, Margin = new Thickness(10), ItemsSource = data };
            dataGrid.AutoGeneratingColumn += DataGrid_AutoGeneratingColumn;
            Grid.SetRow(dataGrid, 1); mainPanel.Children.Add(dataGrid);

            var totalPanel = CreateTotalPanel(data, title);
            if (totalPanel != null) { Grid.SetRow(totalPanel, 2); mainPanel.Children.Add(totalPanel); }

            reportWindow.Content = mainPanel;
            reportWindow.ShowDialog();
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var headers = new Dictionary<string, string> {
                { "IssuanceDate", "Дата выдачи" }, { "BookTitle", "Название книги" }, { "InventoryNumber", "Инв. номер" },
                { "ReaderName", "Читатель" }, { "GroupOrPost", "Группа/Должность" }, { "PlannedReturnDate", "План. возврат" },
                { "ReturnDate", "Дата возврата" }, { "Status", "Статус" }, { "DaysOverdue", "Дней просрочки" },
                { "IssuanceType", "Тип выдачи" }, { "CardNumber", "№ билета" }, { "FullName", "ФИО" },
                { "TypeName", "Тип читателя" }, { "BooksIssued", "Всего выдано" }, { "BooksReturned", "Возвращено" },
                { "CurrentlyIssued", "На руках" }, { "OverdueBooks", "Просрочено" }, { "LastIssuanceDate", "Последняя выдача" },
                { "AuthorName", "Автор" }, { "Publisher", "Издательство" }, { "Year", "Год" },
                { "Genre", "Жанр" }, { "TotalCopies", "Всего экземпляров" }, { "AvailableCopies", "В наличии" },
                { "CopiesInUse", "Выдано на руки" }, { "DisposedCopies", "Списано" }, { "Title", "Название книги" },
                { "OperationDate", "Дата" }
            };

            if (headers.TryGetValue(e.PropertyName, out string headerText))
            {
                e.Column.Header = headerText;
                if (e.PropertyType == typeof(DateTime) || e.PropertyType == typeof(DateTime?))
                {
                    if (e.Column is DataGridTextColumn col) col.Binding.StringFormat = "dd.MM.yyyy";
                }
            }
            else { e.Column.Visibility = Visibility.Collapsed; }
        }

        private UIElement CreateTotalPanel(IEnumerable data, string title)
        {
            var border = new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.LightGray, CornerRadius = new CornerRadius(6), Padding = new Thickness(15, 10, 15, 10), Margin = new Thickness(10) };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            if (title == "Состояние библиотечного фонда" && data is List<BookFundGroupedReport> fundData)
            {
                stack.Children.Add(new TextBlock { Text = $"📚 Всего экземпляров: {fundData.Sum(x => x.TotalCopies)} | ✅ В наличии: {fundData.Sum(x => x.AvailableCopies)} | 📤 На руках: {fundData.Sum(x => x.CopiesInUse)} | ❌ Списано: {fundData.Sum(x => x.DisposedCopies)}", FontSize = 13, FontWeight = FontWeights.Bold });
                border.Child = stack; return border;
            }
            if (title == "Информационное обслуживание" && data is List<InfoServiceReport> infoData)
            {
                stack.Children.Add(new TextBlock { Text = $"📤 Всего выдано: {infoData.Sum(x => x.BooksIssued)} | 📥 Всего возвращено: {infoData.Sum(x => x.BooksReturned)}", FontSize = 13, FontWeight = FontWeights.Bold });
                border.Child = stack; return border;
            }
            return null;
        }

        #endregion

        #region ГЕНЕРАЦИЯ PDF ДОКУМЕНТОВ (PDFsharp)

        /// <summary>
        /// Экспорт в PDF: Выдача литературы
        /// </summary>
        private void CreateBookIssuancePDF(List<BookIssuanceReport> data, DateTime start, DateTime end)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"Отчет_Выдача_литературы_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf",
                Title = "Сохранить отчет в PDF"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            PdfDocument document = new PdfDocument();
            XFont fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
            XFont fontPeriod = new XFont("Arial", 11, XFontStyle.Regular);
            XFont fontHeader = new XFont("Arial", 9, XFontStyle.Bold);
            XFont fontBody = new XFont("Arial", 8, XFontStyle.Regular);

            int itemsPerPage = 22;
            int totalPages = (int)Math.Ceiling((double)data.Count / itemsPerPage);

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                PdfPage page = document.AddPage();
                page.Size = PageSize.A4;
                page.Orientation = PageOrientation.Landscape; // Альбомная ориентация
                XGraphics gfx = XGraphics.FromPdfPage(page);
                double yPoint = 40;

                if (pageIndex == 0)
                {
                    gfx.DrawString("Отчёт по выдаче литературы", fontTitle, XBrushes.Black, new XRect(0, yPoint, page.Width, page.Height), XStringFormats.TopCenter);
                    yPoint += 25;
                    gfx.DrawString($"Период: с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}", fontPeriod, XBrushes.Black, new XRect(0, yPoint, page.Width, page.Height), XStringFormats.TopCenter);
                    yPoint += 30;
                }

                double[] columnWidths = { 60, 160, 60, 150, 80, 60, 60, 60, 50, 60 };
                string[] headers = { "Дата выдачи", "Название книги", "Инв. №", "Читатель", "Группа/Долж.", "План возвр.", "Возвращено", "Статус", "Просрочка", "Тип выдачи" };

                double xPoint = 20;
                XPen pen = new XPen(XColors.Black, 0.5);

                for (int i = 0; i < headers.Length; i++)
                {
                    XRect rect = new XRect(xPoint, yPoint, columnWidths[i], 20);
                    gfx.DrawRectangle(pen, XBrushes.LightGray, rect);
                    gfx.DrawString(headers[i], fontHeader, XBrushes.Black, rect, XStringFormats.Center);
                    xPoint += columnWidths[i];
                }
                yPoint += 20;

                int startItem = pageIndex * itemsPerPage;
                int endItem = Math.Min(startItem + itemsPerPage, data.Count);

                for (int i = startItem; i < endItem; i++)
                {
                    var item = data[i];
                    string[] rowData = { item.IssuanceDate.ToString("dd.MM.yyyy"), item.BookTitle, item.InventoryNumber, item.ReaderName, item.GroupOrPost, item.PlannedReturnDate.ToString("dd.MM.yyyy"), item.ReturnDate?.ToString("dd.MM.yyyy") ?? "-", item.Status, item.DaysOverdue.ToString(), item.IssuanceType };

                    xPoint = 20;
                    for (int j = 0; j < rowData.Length; j++)
                    {
                        XRect rect = new XRect(xPoint, yPoint, columnWidths[j], 20);
                        gfx.DrawRectangle(pen, rect);
                        string text = rowData[j] ?? "";
                        XStringFormat format = (j == 1 || j == 3) ? XStringFormats.CenterLeft : XStringFormats.Center;

                        if (gfx.MeasureString(text, fontBody).Width > columnWidths[j] - 4)
                        {
                            while (text.Length > 0 && gfx.MeasureString(text + "...", fontBody).Width > columnWidths[j] - 4)
                                text = text.Substring(0, text.Length - 1);
                            text += "...";
                        }

                        gfx.DrawString(text, fontBody, XBrushes.Black, new XRect(xPoint + 2, yPoint, columnWidths[j] - 4, 20), format);
                        xPoint += columnWidths[j];
                    }
                    yPoint += 20;
                }
                gfx.DrawString($"Страница {pageIndex + 1} из {totalPages}", fontBody, XBrushes.Black, new XRect(20, page.Height - 30, page.Width - 40, 20), XStringFormats.CenterRight);
            }
            document.Save(filePath);
            CustomMessageBox.Show($"Отчёт успешно сохранён в PDF:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Экспорт в PDF: Читательская активность
        /// </summary>
        private void CreateReaderActivityPDF(List<ReaderActivityReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"Отчет_Читательская_активность_{DateTime.Now:yyyyMMdd}.pdf",
                Title = "Сохранить отчет читательской активности в PDF"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            PdfDocument document = new PdfDocument();
            XFont fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
            XFont fontHeader = new XFont("Arial", 9, XFontStyle.Bold);
            XFont fontBody = new XFont("Arial", 8, XFontStyle.Regular);

            int itemsPerPage = 24; // Книжная ориентация помещает больше строк
            int totalPages = (int)Math.Ceiling((double)data.Count / itemsPerPage);

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                PdfPage page = document.AddPage();
                page.Size = PageSize.A4;
                page.Orientation = PageOrientation.Portrait; // Книжная ориентация
                XGraphics gfx = XGraphics.FromPdfPage(page);
                double yPoint = 40;

                if (pageIndex == 0)
                {
                    gfx.DrawString("Отчёт по читательской активности", fontTitle, XBrushes.Black, new XRect(0, yPoint, page.Width, page.Height), XStringFormats.TopCenter);
                    yPoint += 40;
                }

                double[] columnWidths = { 50, 160, 70, 80, 50, 50, 50, 50 };
                string[] headers = { "№ билета", "ФИО читателя", "Тип", "Группа/Долж.", "Выдано", "Возвр.", "На руках", "Просроч." };

                double xPoint = 20;
                XPen pen = new XPen(XColors.Black, 0.5);

                for (int i = 0; i < headers.Length; i++)
                {
                    XRect rect = new XRect(xPoint, yPoint, columnWidths[i], 20);
                    gfx.DrawRectangle(pen, XBrushes.LightGray, rect);
                    gfx.DrawString(headers[i], fontHeader, XBrushes.Black, rect, XStringFormats.Center);
                    xPoint += columnWidths[i];
                }
                yPoint += 20;

                int startItem = pageIndex * itemsPerPage;
                int endItem = Math.Min(startItem + itemsPerPage, data.Count);

                for (int i = startItem; i < endItem; i++)
                {
                    var item = data[i];
                    string[] rowData = { item.CardNumber, item.FullName, item.TypeName, item.GroupOrPost, item.BooksIssued.ToString(), item.BooksReturned.ToString(), item.CurrentlyIssued.ToString(), item.OverdueBooks.ToString() };

                    xPoint = 20;
                    for (int j = 0; j < rowData.Length; j++)
                    {
                        XRect rect = new XRect(xPoint, yPoint, columnWidths[j], 20);
                        gfx.DrawRectangle(pen, rect);
                        string text = rowData[j] ?? "";
                        XStringFormat format = (j == 1) ? XStringFormats.CenterLeft : XStringFormats.Center;

                        if (gfx.MeasureString(text, fontBody).Width > columnWidths[j] - 4)
                        {
                            while (text.Length > 0 && gfx.MeasureString(text + "...", fontBody).Width > columnWidths[j] - 4)
                                text = text.Substring(0, text.Length - 1);
                            text += "...";
                        }

                        gfx.DrawString(text, fontBody, XBrushes.Black, new XRect(xPoint + 2, yPoint, columnWidths[j] - 4, 20), format);
                        xPoint += columnWidths[j];
                    }
                    yPoint += 20;
                }
                gfx.DrawString($"Страница {pageIndex + 1} из {totalPages}", fontBody, XBrushes.Black, new XRect(20, page.Height - 30, page.Width - 40, 20), XStringFormats.CenterRight);
            }
            document.Save(filePath);
            CustomMessageBox.Show($"Отчёт успешно сохранён в PDF:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Экспорт в PDF: Состояние библиотечного фонда
        /// </summary>
        private void CreateBookFundPDF(List<BookFundGroupedReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"Отчет_Библиотечный_фонд_{DateTime.Now:yyyyMMdd}.pdf",
                Title = "Сохранить состояние фонда в PDF"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            PdfDocument document = new PdfDocument();
            XFont fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
            XFont fontHeader = new XFont("Arial", 9, XFontStyle.Bold);
            XFont fontBody = new XFont("Arial", 8, XFontStyle.Regular);

            int itemsPerPage = 22;
            int totalPages = (int)Math.Ceiling((double)data.Count / itemsPerPage);

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                PdfPage page = document.AddPage();
                page.Size = PageSize.A4;
                page.Orientation = PageOrientation.Landscape; // Много столбцов — альбомный режим
                XGraphics gfx = XGraphics.FromPdfPage(page);
                double yPoint = 40;

                if (pageIndex == 0)
                {
                    gfx.DrawString("Состояние библиотечного фонда (сводный)", fontTitle, XBrushes.Black, new XRect(0, yPoint, page.Width, page.Height), XStringFormats.TopCenter);
                    yPoint += 40;
                }

                double[] columnWidths = { 200, 150, 110, 40, 90, 40, 40, 40, 40 };
                string[] headers = { "Название книги", "Автор", "Издательство", "Год", "Жанр", "Всего", "Дост.", "В исп.", "Спис." };

                double xPoint = 20;
                XPen pen = new XPen(XColors.Black, 0.5);

                for (int i = 0; i < headers.Length; i++)
                {
                    XRect rect = new XRect(xPoint, yPoint, columnWidths[i], 20);
                    gfx.DrawRectangle(pen, XBrushes.LightGray, rect);
                    gfx.DrawString(headers[i], fontHeader, XBrushes.Black, rect, XStringFormats.Center);
                    xPoint += columnWidths[i];
                }
                yPoint += 20;

                int startItem = pageIndex * itemsPerPage;
                int endItem = Math.Min(startItem + itemsPerPage, data.Count);

                for (int i = startItem; i < endItem; i++)
                {
                    var item = data[i];
                    string[] rowData = { item.Title, item.AuthorName, item.Publisher, item.Year.ToString(), item.Genre, item.TotalCopies.ToString(), item.AvailableCopies.ToString(), item.CopiesInUse.ToString(), item.DisposedCopies.ToString() };

                    xPoint = 20;
                    for (int j = 0; j < rowData.Length; j++)
                    {
                        XRect rect = new XRect(xPoint, yPoint, columnWidths[j], 20);
                        gfx.DrawRectangle(pen, rect);
                        string text = rowData[j] ?? "";
                        XStringFormat format = (j == 0 || j == 1 || j == 2) ? XStringFormats.CenterLeft : XStringFormats.Center;

                        if (gfx.MeasureString(text, fontBody).Width > columnWidths[j] - 4)
                        {
                            while (text.Length > 0 && gfx.MeasureString(text + "...", fontBody).Width > columnWidths[j] - 4)
                                text = text.Substring(0, text.Length - 1);
                            text += "...";
                        }

                        gfx.DrawString(text, fontBody, XBrushes.Black, new XRect(xPoint + 2, yPoint, columnWidths[j] - 4, 20), format);
                        xPoint += columnWidths[j];
                    }
                    yPoint += 20;
                }

                // На последней странице выводим суммируемые итоги фонда
                if (pageIndex == totalPages - 1)
                {
                    yPoint += 10;
                    string totalSummary = $"Итого экземпляров фонда: {data.Sum(x => x.TotalCopies)} | Доступно: {data.Sum(x => x.AvailableCopies)} | В использовании: {data.Sum(x => x.CopiesInUse)} | Списано: {data.Sum(x => x.DisposedCopies)}";
                    gfx.DrawString(totalSummary, fontHeader, XBrushes.DarkSlateGray, new XRect(20, yPoint, page.Width - 40, 20), XStringFormats.CenterLeft);
                }

                gfx.DrawString($"Страница {pageIndex + 1} из {totalPages}", fontBody, XBrushes.Black, new XRect(20, page.Height - 30, page.Width - 40, 20), XStringFormats.CenterRight);
            }
            document.Save(filePath);
            CustomMessageBox.Show($"Отчёт успешно сохранён в PDF:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        #endregion

        #region ГЕНЕРАЦИЯ EXCEL ДОКУМЕНТОВ (EPPlus)

        /// <summary>
        /// Экспорт в Excel: Выдача литературы
        /// </summary>
        private void ExportBookIssuanceToExcel(List<BookIssuanceReport> data, DateTime start, DateTime end)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчет_Выдача_литературы_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx",
                Title = "Сохранить отчет в Excel"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Выдача литературы");
                worksheet.Cells["A1:J1"].Merge = true;
                worksheet.Cells["A1"].Value = "Отчёт по выдаче литературы";
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                worksheet.Cells["A2:J2"].Merge = true;
                worksheet.Cells["A2"].Value = $"Период: с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}";
                worksheet.Cells["A2"].Style.Font.Size = 11;
                worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                string[] headers = { "Дата выдачи", "Название книги", "Инв. №", "Читатель", "Группа/Должность", "План. возврат", "Дата возврата", "Статус", "Просрочка (дней)", "Тип выдачи" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[4, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                int row = 5;
                foreach (var item in data)
                {
                    worksheet.Cells[row, 1].Value = item.IssuanceDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 2].Value = item.BookTitle;
                    worksheet.Cells[row, 3].Value = item.InventoryNumber;
                    worksheet.Cells[row, 4].Value = item.ReaderName;
                    worksheet.Cells[row, 5].Value = item.GroupOrPost;
                    worksheet.Cells[row, 6].Value = item.PlannedReturnDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 7].Value = item.ReturnDate?.ToString("dd.MM.yyyy") ?? "-";
                    worksheet.Cells[row, 8].Value = item.Status;
                    worksheet.Cells[row, 9].Value = item.DaysOverdue;
                    worksheet.Cells[row, 10].Value = item.IssuanceType;

                    for (int j = 1; j <= 10; j++)
                    {
                        var cell = worksheet.Cells[row, j];
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        if (j != 2 && j != 4) cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    row++;
                }

                worksheet.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
            CustomMessageBox.Show($"Отчёт успешно сохранён в Excel:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Экспорт в Excel: Читательская активность
        /// </summary>
        private void ExportReaderActivityToExcel(List<ReaderActivityReport> data)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчет_Читательская_активность_{DateTime.Now:yyyyMMdd}.xlsx",
                Title = "Сохранить активность читателей в Excel"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Активность читателей");
                worksheet.Cells["A1:H1"].Merge = true;
                worksheet.Cells["A1"].Value = "Отчёт по читательской активности";
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                string[] headers = { "№ билета", "ФИО читателя", "Тип", "Группа/Должность", "Всего выдано", "Возвращено", "На руках", "Просрочено книг" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[3, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.Gainsboro);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                int row = 4;
                foreach (var item in data)
                {
                    worksheet.Cells[row, 1].Value = item.CardNumber;
                    worksheet.Cells[row, 2].Value = item.FullName;
                    worksheet.Cells[row, 3].Value = item.TypeName;
                    worksheet.Cells[row, 4].Value = item.GroupOrPost;
                    worksheet.Cells[row, 5].Value = item.BooksIssued;
                    worksheet.Cells[row, 6].Value = item.BooksReturned;
                    worksheet.Cells[row, 7].Value = item.CurrentlyIssued;
                    worksheet.Cells[row, 8].Value = item.OverdueBooks;

                    for (int j = 1; j <= 8; j++)
                    {
                        var cell = worksheet.Cells[row, j];
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        if (j != 2) cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    row++;
                }

                worksheet.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
            CustomMessageBox.Show($"Отчёт успешно сохранён в Excel:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Экспорт в Excel: Информационное обслуживание по дням
        /// </summary>
        private void ExportInfoServiceToExcel(List<InfoServiceReport> data, DateTime start, DateTime end)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Отчет_Информ_обслуживание_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx",
                Title = "Сохранить информационное обслуживание в Excel"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            string filePath = saveFileDialog.FileName;

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Информ. обслуживание");
                worksheet.Cells["A1:C1"].Merge = true;
                worksheet.Cells["A1"].Value = "Информационное обслуживание пользователей библиотеки";
                worksheet.Cells["A1"].Style.Font.Size = 14;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                worksheet.Cells["A2:C2"].Merge = true;
                worksheet.Cells["A2"].Value = $"Период: с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}";
                worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                string[] headers = { "Дата", "Выдано книг (операций)", "Возвращено книг (операций)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[4, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.DarkSeaGreen);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                int row = 5;
                foreach (var item in data)
                {
                    worksheet.Cells[row, 1].Value = item.OperationDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 2].Value = item.BooksIssued;
                    worksheet.Cells[row, 3].Value = item.BooksReturned;

                    for (int j = 1; j <= 3; j++)
                    {
                        var cell = worksheet.Cells[row, j];
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    row++;
                }

                // Добавление строки "ИТОГО" в Excel-таблицу
                worksheet.Cells[row, 1].Value = "ИТОГО:";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                worksheet.Cells[row, 2].Value = data.Sum(x => x.BooksIssued);
                worksheet.Cells[row, 2].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[row, 2].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                worksheet.Cells[row, 3].Value = data.Sum(x => x.BooksReturned);
                worksheet.Cells[row, 3].Style.Font.Bold = true;
                worksheet.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[row, 3].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                worksheet.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
            CustomMessageBox.Show($"Отчёт успешно сохранён в Excel:\n{filePath}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        #endregion
    }

    #region МОДЕЛИ ДАННЫХ

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
    }

    #endregion
}