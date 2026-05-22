using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Страница информационного обслуживания библиотеки.
    /// Обеспечивает выдачу книг читателям и группам, учёт ежедневной статистики.
    /// </summary>
    public partial class InformationServicePage : Page
    {
        #region Поля и свойства

        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private readonly BookIssuanceService _issuanceService;

        // Коллекции данных для UI
        private ObservableCollection<BookIssuanceService.ReaderInfo> _readers;
        private ObservableCollection<BookIssuanceService.ReaderInfo> _students;
        private ObservableCollection<BookIssuanceService.GroupInfo> _groups;
        private ObservableCollection<BookIssuanceService.TeacherInfo> _teachers;
        private ObservableCollection<BookIssuanceService.BookInfo> _books;
        private ObservableCollection<BookIssuanceService.IssuanceItem> _todayIssues;

        // Выбранные пользователем элементы
        private BookIssuanceService.BookInfo _selectedBook;
        private BookIssuanceService.ReaderInfo _selectedReader;
        private BookIssuanceService.GroupInfo _selectedGroup;
        private BookIssuanceService.TeacherInfo _selectedTeacher;
        private BookIssuanceService.ReaderInfo _selectedStudent;

        // ID статусов книг (загружаются из БД)
        private int _statusAvailableId = 1;
        private int _statusIssuedId = 3;

        // Данные из заявки для автозаполнения
        private int? _pendingRequestId = null;
        private int? _pendingUserId = null;
        private int? _pendingGroupId = null;
        private int? _pendingUserTypeId = null;
        private string _pendingUserName = "";
        private string _pendingGroupCode = "";
        private string _pendingLessonName = "";
        private DateTime? _pendingLessonDate = null;

        // Текущая запись ежедневной статистики
        private int? _currentRecordId = null;
        private DateTime _selectedReturnDate;

        #endregion

        #region Вспомогательные классы

        /// <summary>
        /// Модель книги из заявки для отображения доступного количества
        /// </summary>
        public class RequestBookItem
        {
            public int IDBook { get; set; }
            public string TitleBook { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public int AvailableCopies { get; set; }
        }

        #endregion

        #region Конструктор и инициализация

        public InformationServicePage()
        {
            InitializeComponent();
            _issuanceService = new BookIssuanceService(_connectionString);

            _readers = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _students = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _groups = new ObservableCollection<BookIssuanceService.GroupInfo>();
            _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            _books = new ObservableCollection<BookIssuanceService.BookInfo>();
            _todayIssues = new ObservableCollection<BookIssuanceService.IssuanceItem>();

            DatePicker.SelectedDate = DateTime.Today;
            if (ReturnDatePicker != null)
            {
                ReturnDatePicker.SelectedDate = DateTime.Now.AddDays(14);
                _selectedReturnDate = ReturnDatePicker.SelectedDate.Value;
                ReturnDatePicker.SelectedDateChanged += ReturnDatePicker_SelectedDateChanged;
            }
            else
            {
                _selectedReturnDate = DateTime.Now.AddDays(14);
            }

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            LoadStatusIds();
            LoadTodayIssues();
            LoadReaders();
            LoadGroups();
            LoadTeachers();
            LoadBooks();
            InitializeStats();
            this.Loaded += (s, e) => UpdateInventoryPreview();
        }

        #endregion

        #region Загрузка справочных данных

        private void LoadStatusIds()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    _statusAvailableId = GetStatusId(conn, "В наличии");
                    _statusIssuedId = GetStatusId(conn, "Выдана");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статусов: {ex.Message}");
            }
        }

        private int GetStatusId(SqlConnection conn, string statusPattern)
        {
            string query = $"SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%{statusPattern}%'";
            using (var cmd = new SqlCommand(query, conn))
            {
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 1;
            }
        }

        private void LoadReaders()
        {
            try { _readers = _issuanceService.GetReaders(); }
            catch (Exception ex) { ShowError("Ошибка загрузки читателей", ex); }
        }

        private void LoadGroups()
        {
            try { _groups = _issuanceService.GetGroups(); }
            catch (Exception ex) { ShowError("Ошибка загрузки групп", ex); }
        }

        private void LoadTeachers()
        {
            try
            {
                _teachers = _issuanceService.GetTeachers();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки преподавателей", ex);
                _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            }
        }

        private void LoadBooks()
        {
            try { _books = _issuanceService.GetAvailableBooks(); }
            catch (Exception ex) { ShowError("Ошибка загрузки книг", ex); }
        }

        private void LoadTodayIssues()
        {
            try
            {
                _todayIssues = _issuanceService.GetIssuancesByDate(DateTime.Today);
                foreach (var item in _todayIssues)
                {
                    item.OverdueColor = item.IsOverdue
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }
                IssuesDataGrid.ItemsSource = _todayIssues;
            }
            catch (Exception ex) { ShowError("Ошибка загрузки выдач", ex); }
        }

        #endregion

        #region Ежедневная статистика

        private void InitializeStats()
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    LoadOrCreateDailyStats(conn, selectedDate);
                }
            }
            catch (Exception ex) { ShowError("Ошибка загрузки статистики", ex); }
        }

        private void LoadOrCreateDailyStats(SqlConnection conn, DateTime date)
        {
            string query = @"SELECT id_record, delivery_book, delivery_certificate, events_count 
                             FROM InformationServiseTb WHERE date_record = @Date";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Date", date.Date);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _currentRecordId = reader.GetInt32(0);
                        BooksCountBox.Text = reader.GetInt32(1).ToString();
                        ReferencesCountBox.Text = reader.GetInt32(2).ToString();
                        EventsCountBox.Text = reader.GetInt32(3).ToString();
                        SetEditMode(true);
                    }
                    else
                    {
                        _currentRecordId = null;
                        BooksCountBox.Text = "0";
                        ReferencesCountBox.Text = "0";
                        EventsCountBox.Text = "0";
                        SetEditMode(false);
                    }
                }
            }
            UpdateStatsDisplay();
        }

        private void SetEditMode(bool isExisting)
        {
            SaveNewRecordButton.Visibility = isExisting ? Visibility.Collapsed : Visibility.Visible;
            SaveChangesButton.Visibility = isExisting ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = isExisting ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStatsDisplay()
        {
            BooksCountStat.Text = BooksCountBox.Text;
            ReferencesCountStat.Text = ReferencesCountBox.Text;
            EventsCountStat.Text = EventsCountBox.Text;
            IdRecordLabel.Content = _currentRecordId.HasValue ? $"ID записи: {_currentRecordId}" : "Новая запись";
        }

        private void UpdateDailyStats(int additionalBooks)
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string checkQuery = "SELECT id_record, delivery_book FROM InformationServiseTb WHERE date_record = @Date";
                    using (var cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int currentId = reader.GetInt32(0);
                                int currentBooks = reader.GetInt32(1);
                                reader.Close();
                                ExecuteNonQuery(conn,
                                    "UPDATE InformationServiseTb SET delivery_book = @Books WHERE id_record = @ID",
                                    ("@Books", currentBooks + additionalBooks), ("@ID", currentId));
                            }
                            else
                            {
                                reader.Close();
                                ExecuteNonQuery(conn,
                                    "INSERT INTO InformationServiseTb (date_record, delivery_book, delivery_certificate, events_count) VALUES (@Date, @Books, 0, 0)",
                                    ("@Date", selectedDate.Date), ("@Books", additionalBooks));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статистики: {ex.Message}");
            }
        }

        #endregion

        #region Выдача книг

        private void CreateIssueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateIssueForm(out int requestedQuantity)) return;

            if (ReaderTypeRadio.IsChecked == true)
                CreateIndividualIssuance(requestedQuantity);
            else
                CreateGroupIssuance(requestedQuantity);
        }

        private bool ValidateIssueForm(out int quantity)
        {
            quantity = 0;
            if (_selectedBook == null) { ShowWarning("Выберите книгу для выдачи."); return false; }
            if (!int.TryParse(QuantityBox?.Text, out quantity) || quantity <= 0) { ShowWarning("Укажите корректное количество."); return false; }
            if (quantity > _selectedBook.AvailableCopies) { ShowWarning($"Недостаточно экземпляров. Доступно: {_selectedBook.AvailableCopies}"); return false; }

            if (ReaderTypeRadio.IsChecked == true)
            {
                if (_selectedReader == null) { ShowWarning("Выберите читателя."); return false; }
            }
            else
            {
                if (_selectedGroup == null) { ShowWarning("Выберите группу."); return false; }
                if (_selectedTeacher == null) { ShowWarning("Выберите преподавателя."); return false; }
                if (_selectedStudent == null) { ShowWarning("Выберите студента."); return false; }
            }
            return true;
        }

        /// <summary>
        /// Создание индивидуальной выдачи книг.
        /// Использует только книги со статусом "В наличии" (НЕ зарезервированные).
        /// </summary>
        private async void CreateIndividualIssuance(int quantity)
        {
            try
            {
                List<int> bookIds = GetAvailableBookIds(_selectedBook.TitleBook, _selectedBook.AuthorName, quantity);

                if (bookIds.Count < quantity)
                {
                    ShowWarning($"Недостаточно экземпляров книги.\nТребуется: {quantity}, доступно: {bookIds.Count}");
                    return;
                }

                DateTime plannedReturnDate = _selectedReturnDate;

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (int bookId in bookIds)
                            {
                                using (var cmd = new SqlCommand("sp_CreateIndividualIssuance", conn, transaction))
                                {
                                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                    cmd.Parameters.AddWithValue("@BookId", bookId);
                                    cmd.Parameters.AddWithValue("@ReaderId", _selectedReader.ReaderID);
                                    cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                                    cmd.Parameters.AddWithValue("@Notes", "Выдача через информационное обслуживание");
                                    cmd.Parameters.Add("@IssuanceId", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.Output;
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            UpdateRequestStatusIfNeeded(conn, transaction);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                UpdateDailyStats(quantity);
                _selectedBook.AvailableCopies -= quantity;
                ShowSuccess($"Индивидуальная выдача успешно создана!\nВыдано книг: {quantity}\nДата возврата: {plannedReturnDate:dd.MM.yyyy}");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при создании выдачи", ex);
            }
        }

        /// <summary>
        /// Создание групповой выдачи книг.
        /// Использует книги со статусом "В наличии" или "Зарезервирована",
        /// при выдаче статус меняется на "Выдана".
        /// </summary>
        private async void CreateGroupIssuance(int quantity)
        {
            try
            {
                List<int> bookIds = GetAvailableBookIdsForGroupIssue(_selectedBook.TitleBook, _selectedBook.AuthorName, quantity, _pendingRequestId);

                if (bookIds.Count < quantity)
                {
                    ShowWarning($"Недостаточно экземпляров книги.\nТребуется: {quantity}, доступно: {bookIds.Count}");
                    return;
                }

                DateTime plannedReturnDate = _selectedReturnDate;
                string bookIdsString = string.Join(",", bookIds);

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("sp_CreateGroupIssuance", conn, transaction))
                            {
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@GroupId", _selectedGroup.GroupID);
                                cmd.Parameters.AddWithValue("@TeacherId", _selectedTeacher.TeacherID);
                                cmd.Parameters.AddWithValue("@StudentId", _selectedStudent.ReaderID);
                                cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                                cmd.Parameters.AddWithValue("@SubjectName", _pendingLessonName ?? "Урок");
                                cmd.Parameters.AddWithValue("@Notes", "Групповая выдача");
                                cmd.Parameters.AddWithValue("@BookIds", bookIdsString);
                                cmd.Parameters.Add("@GroupIssuanceId", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.Output;
                                await cmd.ExecuteNonQueryAsync();
                            }

                            UpdateRequestStatusIfNeeded(conn, transaction);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                UpdateDailyStats(quantity);
                _selectedBook.AvailableCopies -= quantity;
                ShowSuccess($"Групповая выдача успешно создана!\nВыдано книг: {quantity}\nДата возврата: {plannedReturnDate:dd.MM.yyyy}");
                NavigationService?.GoBack();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при создании групповой выдачи", ex);
            }
        }

        private void UpdateRequestStatusIfNeeded(SqlConnection conn, SqlTransaction transaction)
        {
            if (!_pendingRequestId.HasValue) return;

            ExecuteNonQuery(conn,
                @"UPDATE BookRequests 
                  SET StatusID = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Выдана'), 
                      IssuedDate = GETDATE() 
                  WHERE ID_Request = @RequestId",
                transaction, ("@RequestId", _pendingRequestId.Value));

            ExecuteNonQuery(conn,
                @"UPDATE RequestItems 
                  SET StatusID = (SELECT ID_Status FROM RequestItemStatuses WHERE StatusName = 'Выдана'),
                      IssuedQuantity = Quantity
                  WHERE RequestID = @RequestId",
                transaction, ("@RequestId", _pendingRequestId.Value));
        }

        /// <summary>
        /// Получение ID книг со статусом "В наличии" (без зарезервированных).
        /// Используется для индивидуальной выдачи.
        /// </summary>
        private List<int> GetAvailableBookIds(string title, string author, int requiredQuantity)
        {
            var bookIds = new List<int>();
            try
            {
                string sql = @"
            SELECT TOP (@Qty) b.IDBook
            FROM Books b
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE b.TitleBook = @Title 
              AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @Author
              AND b.AvailableCopies > 0
              AND b.StatusBook NOT IN (
                  SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
              )
              AND b.StatusBook NOT IN (
                  SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
              )
              AND b.StatusBook NOT IN (
                  SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
              )
            ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@Qty", requiredQuantity);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                bookIds.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книг: {ex.Message}");
            }
            return bookIds;
        }

        /// <summary>
        /// Получение ID книг со статусом "В наличии" или "Зарезервирована".
        /// Используется для групповой выдачи.
        /// </summary>
        private List<int> GetAvailableBookIdsForGroupIssue(string title, string author, int requiredQuantity, int? requestId = null)
        {
            var bookIds = new List<int>();
            try
            {
                string sql;

                if (requestId.HasValue)
                {
                    sql = @"
                SELECT TOP (@Qty) b.IDBook
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                WHERE b.TitleBook = @Title 
                  AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @Author
                  AND b.AvailableCopies > 0
                  AND b.StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
                  )
                  AND b.StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
                  )
                  AND (
                      b.StatusBook NOT IN (
                          SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                      )
                      OR b.IDBook IN (
                          SELECT ri.BookID FROM RequestItems ri WHERE ri.RequestID = @RequestId
                      )
                  )
                ORDER BY b.IDBook";
                }
                else
                {
                    sql = @"
                SELECT TOP (@Qty) b.IDBook
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                WHERE b.TitleBook = @Title 
                  AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @Author
                  AND b.AvailableCopies > 0
                  AND b.StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                  )
                  AND b.StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
                  )
                  AND b.StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
                  )
                ORDER BY b.IDBook";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@Qty", requiredQuantity);
                        if (requestId.HasValue)
                            cmd.Parameters.AddWithValue("@RequestId", requestId.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                bookIds.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книг: {ex.Message}");
            }
            return bookIds;
        }

        #endregion

        #region Сохранение и удаление статистики

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e) => SaveRecord(false);
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e) => SaveRecord(true);

        private void SaveRecord(bool isUpdate)
        {
            ParseStatsInput(out int books, out int references, out int events);

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    if (isUpdate && _currentRecordId.HasValue)
                    {
                        ExecuteNonQuery(conn,
                            "UPDATE InformationServiseTb SET delivery_book = @Books, delivery_certificate = @Ref, events_count = @Events WHERE id_record = @ID",
                            ("@Books", books), ("@Ref", references), ("@Events", events), ("@ID", _currentRecordId.Value));
                        ShowSuccess("Запись обновлена!");
                    }
                    else
                    {
                        if (CheckRecordExists(conn)) { ShowWarning("Запись за эту дату уже существует!"); return; }
                        ExecuteNonQuery(conn,
                            "INSERT INTO InformationServiseTb (date_record, delivery_book, delivery_certificate, events_count) VALUES (@Date, @Books, @Ref, @Events)",
                            ("@Date", (DatePicker.SelectedDate ?? DateTime.Today).Date), ("@Books", books), ("@Ref", references), ("@Events", events));
                        ShowSuccess("Запись сохранена!");
                    }
                }
                InitializeStats();
            }
            catch (Exception ex) { ShowError("Ошибка сохранения", ex); }
        }

        private void ParseStatsInput(out int books, out int references, out int events)
        {
            int.TryParse(BooksCountBox.Text, out books);
            int.TryParse(ReferencesCountBox.Text, out references);
            int.TryParse(EventsCountBox.Text, out events);
        }

        private bool CheckRecordExists(SqlConnection conn)
        {
            string sql = "SELECT COUNT(*) FROM InformationServiseTb WHERE date_record = @Date";
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Date", (DatePicker.SelectedDate ?? DateTime.Today).Date);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentRecordId.HasValue) return;
            if (CustomMessageBox.Show("Удалить запись за этот день?", "Подтверждение",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question) != CustomMessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    ExecuteNonQuery(conn, "DELETE FROM InformationServiseTb WHERE id_record = @ID", ("@ID", _currentRecordId.Value));
                }
                InitializeStats();
                ShowSuccess("Запись удалена!");
            }
            catch (Exception ex) { ShowError("Ошибка удаления", ex); }
        }

        #endregion

        #region Автозаполнение из заявки

        public void SetDataFromRequest(int requestId, int userId, int? groupId, string userName, string groupCode, string lessonName, DateTime lessonDate)
        {
            _pendingRequestId = requestId;
            _pendingUserId = userId;
            _pendingGroupId = groupId;
            _pendingUserName = userName;
            _pendingGroupCode = groupCode;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = null;
        }

        public void SetDataFromRequestIndividual(int requestId, int userId, string userName, string lessonName, DateTime lessonDate, int userTypeId)
        {
            _pendingRequestId = requestId;
            _pendingUserId = userId;
            _pendingUserName = userName;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = userTypeId;
            _pendingGroupId = null;
            _pendingGroupCode = "";
        }

        private async void AutoFillFromRequest()
        {
            try
            {
                if (_pendingGroupId.HasValue && _pendingGroupId > 0)
                {
                    GroupTypeRadio.IsChecked = true;
                    await LoadAndSelectGroup(_pendingGroupCode);
                    await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                }
                else
                {
                    ReaderTypeRadio.IsChecked = true;
                    if (_pendingUserTypeId == 3)
                        await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                    else
                        await LoadAndSelectReader(_pendingUserId.Value, _pendingUserName);
                }

                await LoadRequestBooks(_pendingRequestId.Value);
                ShowSuccess($"Данные из заявки №{_pendingRequestId} загружены.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка автозаполнения: {ex.Message}");
                ShowError("Ошибка загрузки данных из заявки", ex);
            }
        }

        private async Task LoadRequestBooks(int requestId)
        {
            try
            {
                string query = @"
            SELECT 
                MIN(ri.BookID) AS BookID,
                b.TitleBook,
                ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                SUM(ri.Quantity) AS Quantity
            FROM RequestItems ri
            JOIN Books b ON ri.BookID = b.IDBook
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE ri.RequestID = @RequestId
            GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor";

                var booksFromRequest = new List<RequestBookItem>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                booksFromRequest.Add(new RequestBookItem
                                {
                                    IDBook = reader.GetInt32(0),
                                    TitleBook = reader.GetString(1),
                                    AuthorName = reader.GetString(2),
                                    Quantity = reader.GetInt32(3)
                                });
                            }
                        }
                    }
                }

                foreach (var book in booksFromRequest)
                   book.AvailableCopies = await GetRealAvailableCopies(book.TitleBook, requestId);

                if (booksFromRequest.Any())
                {
                    var first = booksFromRequest.First();
                    var selected = _books.FirstOrDefault(b => b.TitleBook == first.TitleBook && b.AuthorName == first.AuthorName);

                    if (selected == null)
                    {
                        selected = new BookIssuanceService.BookInfo
                        {
                            IDBook = first.IDBook,
                            TitleBook = first.TitleBook,
                            AuthorName = first.AuthorName,
                            AvailableCopies = first.AvailableCopies
                        };
                        _books.Add(selected);
                    }
                    else
                    {
                        selected.AvailableCopies = first.AvailableCopies;
                    }

                    _selectedBook = selected;
                    SelectedBookTitle.Text = selected.TitleBook;
                    SelectedBookAuthor.Text = selected.AuthorName;
                    SelectedBookPanel.Visibility = Visibility.Visible;
                    BookSearchBox.Text = selected.TitleBook;
                    BookSearchBox.Foreground = Brushes.Black;
                    AvailableQuantityText.Text = selected.AvailableCopies.ToString();
                    QuantityBox.Text = first.Quantity.ToString();
                    QuantityBox.IsEnabled = true;
                }
                else
                {
                    ShowWarning("В заявке нет книг для выдачи.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadRequestBooks: {ex.Message}");
                ShowError("Ошибка загрузки книг из заявки", ex);
            }
        }

        /// <summary>
        /// Получение реального количества доступных экземпляров книги по названию.
        /// Для выдачи по заявке: исключает зарезервированные другими заявками,
        /// но включает зарезервированные ЭТОЙ заявкой.
        /// </summary>
        private async Task<int> GetRealAvailableCopies(string title, int? excludeRequestId = null)
        {
            try
            {
                string query;

                if (excludeRequestId.HasValue)
                {
                    // Для выдачи по заявке: исключаем зарезервированные другими заявками
                    query = @"
                SELECT COUNT(*)
                FROM Books
                WHERE TitleBook = @Title
                  AND StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
                  )
                  AND StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
                  )
                  AND (
                     
                              StatusBook NOT IN(
                          SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                      )
                    
                              OR IDBook IN(
                          SELECT ri.BookID
                          FROM RequestItems ri
                          WHERE ri.RequestID = @ExcludeRequestId
                      )
                          )";
                }
                else
                {
                    // Для ручного поиска: исключаем все зарезервированные
                    query = @"
                SELECT COUNT(*)
                FROM Books
                WHERE TitleBook = @Title
                  AND StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                  )
                  AND StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
                  )
                  AND StatusBook NOT IN (
                      SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
                  )";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        if (excludeRequestId.HasValue)
                            cmd.Parameters.AddWithValue("@ExcludeRequestId", excludeRequestId.Value);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetRealAvailableCopies: {ex.Message}");
                return 0;
            }
        }

        private async Task LoadAndSelectTeacher(int userId, string userName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT ReaderID, LastName, FirstName, MiddleName FROM ReaderForm WHERE ReaderID = @UserId";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                _selectedTeacher = new BookIssuanceService.TeacherInfo
                                {
                                    TeacherID = reader.GetInt32(0),
                                    LastName = reader.GetString(1),
                                    FirstName = reader.GetString(2),
                                    MiddleName = reader["MiddleName"]?.ToString()
                                };
                                TeacherSearchBox.Text = _selectedTeacher.FullName;
                                TeacherSearchBox.Foreground = Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка загрузки преподавателя: {ex.Message}"); }
        }

        private async Task LoadAndSelectGroup(string groupCode)
        {
            if (string.IsNullOrEmpty(groupCode)) return;
            var group = _groups.FirstOrDefault(g => g.GroupCode == groupCode);
            if (group != null)
            {
                _selectedGroup = group;
                GroupSearchBox.Text = group.GroupCode;
                GroupSearchBox.Foreground = Brushes.Black;
                await LoadStudentsByGroup(group.GroupID);
            }
        }

        private async Task LoadStudentsByGroup(int groupId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"SELECT DISTINCT rf.ReaderID, rf.LastName, rf.FirstName, rf.MiddleName, rf.CardNumber
                                     FROM ReaderForm rf WHERE rf.GroupID = @GroupId AND rf.TypeID = 1
                                     ORDER BY rf.LastName, rf.FirstName";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            _students.Clear();
                            while (await reader.ReadAsync())
                            {
                                _students.Add(new BookIssuanceService.ReaderInfo
                                {
                                    ReaderID = reader.GetInt32(0),
                                    LastName = reader.GetString(1),
                                    FirstName = reader.GetString(2),
                                    MiddleName = reader["MiddleName"]?.ToString(),
                                    CardNumber = reader.GetString(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка загрузки студентов: {ex.Message}"); }
        }

        private async Task LoadAndSelectReader(int userId, string userName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT ReaderID, LastName, FirstName, MiddleName, CardNumber FROM ReaderForm WHERE ReaderID = @UserId";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                _selectedReader = new BookIssuanceService.ReaderInfo
                                {
                                    ReaderID = reader.GetInt32(0),
                                    LastName = reader.GetString(1),
                                    FirstName = reader.GetString(2),
                                    MiddleName = reader["MiddleName"]?.ToString(),
                                    CardNumber = reader.GetString(4)
                                };
                                ReaderSearchBox.Text = _selectedReader.DisplayInfo;
                                ReaderSearchBox.Foreground = Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка загрузки читателя: {ex.Message}"); }
        }

        #endregion

        #region Обработчики событий UI

        private void ReturnDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReturnDatePicker?.SelectedDate != null)
                _selectedReturnDate = ReturnDatePicker.SelectedDate.Value;
        }

        private void IssueType_Checked(object sender, RoutedEventArgs e)
        {
            if (ReaderTypeRadio.IsChecked == true)
            {
                if (ReaderSearchSection != null) ReaderSearchSection.Visibility = Visibility.Visible;
                if (GroupSearchSection != null) GroupSearchSection.Visibility = Visibility.Collapsed;
                if (ReturnDatePicker != null) ReturnDatePicker.SelectedDate = DateTime.Now.AddDays(14);
            }
            else
            {
                if (ReaderSearchSection != null) ReaderSearchSection.Visibility = Visibility.Collapsed;
                if (GroupSearchSection != null) GroupSearchSection.Visibility = Visibility.Visible;
                if (ReturnDatePicker != null) ReturnDatePicker.SelectedDate = DateTime.Now.AddDays(30);
            }
            ResetQuantity();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeStats();
            LoadTodayIssues();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeStats();
            LoadTeachers();
            LoadTodayIssues();
            if (_pendingRequestId.HasValue) AutoFillFromRequest();
        }

        private void ReturnBooksButton_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new ReturnBooksPage());

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) => e.Handled = !char.IsDigit(e.Text, 0);

        private void UpdateInventoryPreview()
        {
            if (_selectedBook == null) return;
            if (AvailableQuantityText != null) AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();
            if (QuantityBox != null)
            {
                QuantityBox.IsEnabled = true;
                if (string.IsNullOrWhiteSpace(QuantityBox.Text) || QuantityBox.Text == "0") QuantityBox.Text = "1";
                if (int.TryParse(QuantityBox.Text, out int qty) && qty > _selectedBook.AvailableCopies)
                    QuantityBox.Text = _selectedBook.AvailableCopies.ToString();
            }
        }

        private void ResetQuantity()
        {
            if (QuantityBox != null && _selectedBook != null) { QuantityBox.IsEnabled = true; QuantityBox.Text = "1"; }
        }

        #endregion

        #region Поиск и выбор

        // Читатели
        private void ReaderSearchBox_GotFocus(object sender, RoutedEventArgs e) { if (ReaderSearchBox != null) ClearPlaceholder(ReaderSearchBox, "Введите фамилию или номер билета..."); }
        private void ReaderSearchBox_LostFocus(object sender, RoutedEventArgs e) { if (ReaderSearchBox != null && ReadersListBox != null) RestorePlaceholder(ReaderSearchBox, "Введите фамилию или номер билета...", ReadersListBox); }
        private void ReaderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ReadersListBox == null) return;
            if (IsPlaceholder(ReaderSearchBox.Text, "Введите фамилию или номер билета...")) { ReadersListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _readers.Where(r => MatchReader(r, ReaderSearchBox.Text)).Take(10).ToList();
            ShowFilteredList(ReadersListBox, filtered);
        }
        private void ReadersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReadersListBox.SelectedItem is BookIssuanceService.ReaderInfo selected)
            { _selectedReader = selected; ReaderSearchBox.Text = selected.DisplayInfo; ReaderSearchBox.Foreground = Brushes.Black; ReadersListBox.Visibility = Visibility.Collapsed; }
        }

        // Группы
        private void GroupSearchBox_GotFocus(object sender, RoutedEventArgs e) { if (GroupSearchBox != null) ClearPlaceholder(GroupSearchBox, "Введите код группы..."); }
        private void GroupSearchBox_LostFocus(object sender, RoutedEventArgs e) { if (GroupSearchBox != null && GroupsListBox != null) RestorePlaceholder(GroupSearchBox, "Введите код группы...", GroupsListBox); }
        private void GroupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GroupsListBox == null) return;
            if (IsPlaceholder(GroupSearchBox.Text, "Введите код группы...")) { GroupsListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _groups.Where(g => g.GroupCode.ToLower().Contains(GroupSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(GroupsListBox, filtered);
        }
        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsListBox.SelectedItem is BookIssuanceService.GroupInfo selected)
            { _selectedGroup = selected; GroupSearchBox.Text = selected.GroupCode; GroupSearchBox.Foreground = Brushes.Black; GroupsListBox.Visibility = Visibility.Collapsed; }
        }

        // Преподаватели
        private void TeacherSearchBox_GotFocus(object sender, RoutedEventArgs e) { if (TeacherSearchBox != null) ClearPlaceholder(TeacherSearchBox, "Введите фамилию преподавателя..."); }
        private void TeacherSearchBox_LostFocus(object sender, RoutedEventArgs e) { if (TeacherSearchBox != null && TeachersListBox != null) RestorePlaceholder(TeacherSearchBox, "Введите фамилию преподавателя...", TeachersListBox); }
        private void TeacherSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TeachersListBox == null) return;
            if (IsPlaceholder(TeacherSearchBox.Text, "Введите фамилию преподавателя...")) { TeachersListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _teachers.Where(t => t.LastName.ToLower().Contains(TeacherSearchBox.Text.ToLower()) || t.FirstName.ToLower().Contains(TeacherSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(TeachersListBox, filtered);
        }
        private void TeachersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeachersListBox.SelectedItem is BookIssuanceService.TeacherInfo selected)
            { _selectedTeacher = selected; TeacherSearchBox.Text = selected.FullName; TeacherSearchBox.Foreground = Brushes.Black; TeachersListBox.Visibility = Visibility.Collapsed; }
        }

        // Студенты
        private void StudentSearchBox_GotFocus(object sender, RoutedEventArgs e) { if (StudentSearchBox != null) ClearPlaceholder(StudentSearchBox, "Введите фамилию или номер билета..."); }
        private void StudentSearchBox_LostFocus(object sender, RoutedEventArgs e) { if (StudentSearchBox != null && StudentsListBox != null) RestorePlaceholder(StudentSearchBox, "Введите фамилию или номер билета...", StudentsListBox); }
        private void StudentSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (StudentsListBox == null) return;
            if (IsPlaceholder(StudentSearchBox.Text, "Введите фамилию или номер билета...")) { StudentsListBox.Visibility = Visibility.Collapsed; return; }
            _students = _issuanceService.GetReadersStudent(StudentSearchBox.Text);
            ShowFilteredList(StudentsListBox, _students.ToList());
        }
        private void StudentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentsListBox.SelectedItem is BookIssuanceService.ReaderInfo selected)
            { _selectedStudent = selected; StudentSearchBox.Text = selected.DisplayInfo; StudentSearchBox.Foreground = Brushes.Black; StudentsListBox.Visibility = Visibility.Collapsed; }
        }

        // Книги
        private void BookSearchBox_GotFocus(object sender, RoutedEventArgs e) { if (BookSearchBox != null) ClearPlaceholder(BookSearchBox, "Введите название или автора..."); }
        private void BookSearchBox_LostFocus(object sender, RoutedEventArgs e) { if (BookSearchBox != null && BooksListBox != null) RestorePlaceholder(BookSearchBox, "Введите название или автора...", BooksListBox); }
        private void BookSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BooksListBox == null) return;
            if (IsPlaceholder(BookSearchBox.Text, "Введите название или автора...")) { BooksListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _books.Where(b => b.TitleBook.ToLower().Contains(BookSearchBox.Text.ToLower()) || b.AuthorName.ToLower().Contains(BookSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(BooksListBox, filtered);
        }
        private void BooksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BooksListBox.SelectedItem is BookIssuanceService.BookInfo selected)
            {
                _selectedBook = selected;
                SelectedBookTitle.Text = selected.TitleBook; SelectedBookAuthor.Text = selected.AuthorName;
                SelectedBookPanel.Visibility = Visibility.Visible; BookSearchBox.Text = selected.TitleBook;
                BookSearchBox.Foreground = Brushes.Black; BooksListBox.Visibility = Visibility.Collapsed;
                AvailableQuantityText.Text = selected.AvailableCopies.ToString(); UpdateInventoryPreview();
            }
        }
        private void ClearSelectedBook_Click(object sender, RoutedEventArgs e)
        {
            _selectedBook = null; SelectedBookPanel.Visibility = Visibility.Collapsed;
            BookSearchBox.Text = "Введите название или автора..."; BookSearchBox.Foreground = Brushes.Gray; AvailableQuantityText.Text = "0";
        }

        #endregion

        #region Вспомогательные методы UI

        private void ClearPlaceholder(TextBox box, string placeholder) { if (box.Text == placeholder) { box.Text = ""; box.Foreground = Brushes.Black; } }
        private void RestorePlaceholder(TextBox box, string placeholder, ListBox list) { if (string.IsNullOrWhiteSpace(box.Text)) { box.Text = placeholder; box.Foreground = Brushes.Gray; list.Visibility = Visibility.Collapsed; } }
        private bool IsPlaceholder(string text, string placeholder) => string.IsNullOrWhiteSpace(text) || text == placeholder;
        private bool MatchReader(BookIssuanceService.ReaderInfo r, string search) { string s = search.ToLower(); return r.LastName.ToLower().Contains(s) || r.FirstName.ToLower().Contains(s) || r.CardNumber.ToLower().Contains(s); }
        private void ShowFilteredList(ListBox listBox, System.Collections.IList items) { if (items.Count > 0) { listBox.ItemsSource = items; listBox.Visibility = Visibility.Visible; } else { listBox.Visibility = Visibility.Collapsed; } }

        #endregion

        #region Вспомогательные методы БД и сообщений

        private void ExecuteNonQuery(SqlConnection conn, string sql, params (string, object)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, conn)) { foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Item1, p.Item2); cmd.ExecuteNonQuery(); }
        }

        private void ExecuteNonQuery(SqlConnection conn, string sql, SqlTransaction transaction, params (string, object)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, conn, transaction)) { foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Item1, p.Item2); cmd.ExecuteNonQuery(); }
        }

        private void ShowWarning(string message) => CustomMessageBox.Show(message, "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
        private void ShowSuccess(string message) => CustomMessageBox.Show(message, "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        private void ShowError(string title, Exception ex) => CustomMessageBox.Show($"{title}: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);

        #endregion
    }
}