using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class InformationServicePage : Page
    {
        #region Поля и свойства

        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private readonly BookIssuanceService _issuanceService;


        private ObservableCollection<BookIssuanceService.ReaderInfo> _readers;
        private ObservableCollection<BookIssuanceService.ReaderInfo> _students;
        private ObservableCollection<BookIssuanceService.GroupInfo> _groups;
        private ObservableCollection<BookIssuanceService.TeacherInfo> _teachers;
        private ObservableCollection<BookIssuanceService.BookInfo> _books;
        private ObservableCollection<BookIssuanceService.IssuanceItem> _todayIssues;
        private ObservableCollection<RequestBookItem> _pendingRequestBooks;
        private BookIssuanceService.BookInfo _selectedBook;
        private BookIssuanceService.ReaderInfo _selectedReader;
        private BookIssuanceService.GroupInfo _selectedGroup;
        private BookIssuanceService.TeacherInfo _selectedTeacher;
        private BookIssuanceService.ReaderInfo _selectedStudent;
        

        private int _statusAvailableId = 1;
        private int _statusReservedId = 6;
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
        private int _pendingQuantity = 1;

        private DateTime _selectedReturnDate;
        private bool _isInitialDataLoaded = false;

        #endregion

        #region Вспомогательные классы
        public class RequestBookItem
        {
            public int ID_RequestItem { get; set; }
            public int BookID { get; set; }
            public string BookTitle { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public int StatusID { get; set; }
            public string StatusName { get; set; } = "";
            // Добавьте эти поля для совместимости
            public int IDBook { get; set; }
            public string TitleBook { get; set; } = "";
            public int AvailableCopies { get; set; }
        }

        #endregion

        #region Конструкторы

        /// <summary>
        /// Конструктор для открытия страницы "с нуля" (без предварительной заявки)
        /// </summary>
        public InformationServicePage()
        {
            InitializeComponent();
            _issuanceService = new BookIssuanceService(_connectionString);

            InitializeCollections();
            InitializePlaceholders();
            InitializeReturnDatePicker();

            this.Loaded += Page_Loaded;
        }

        public InformationServicePage(
      int? requestId = null,
      int? userId = null,
      int? groupId = null,
      string userName = "",
      string groupCode = "",
      string lessonName = "",
      DateTime? lessonDate = null,
      int? userTypeId = null,
      int quantity = 1,
      List<RequestBookItem> requestBooks = null)
        {
            InitializeComponent();
            _issuanceService = new BookIssuanceService(_connectionString);

            InitializeCollections();
            InitializePlaceholders();
            InitializeReturnDatePicker();

            // Сохраняем данные из заявки
            _pendingRequestId = requestId;
            _pendingUserId = userId;
            _pendingGroupId = groupId;
            _pendingUserName = userName;
            _pendingGroupCode = groupCode;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = userTypeId;
            _pendingQuantity = quantity;

            // Преобразуем список книг в ObservableCollection (если нужно)
            if (requestBooks != null)
            {
                _pendingRequestBooks = new ObservableCollection<RequestBookItem>();
                foreach (var book in requestBooks)
                {
                    _pendingRequestBooks.Add(new RequestBookItem
                    {
                        ID_RequestItem = book.ID_RequestItem,
                        BookID = book.BookID,
                        IDBook = book.BookID,  // Для совместимости
                        BookTitle = book.BookTitle,
                        TitleBook = book.BookTitle,  // Для совместимости
                        AuthorName = book.AuthorName,
                        Quantity = book.Quantity,
                        StatusID = book.StatusID,
                        StatusName = book.StatusName,
                        AvailableCopies = book.Quantity  // Для совместимости
                    });
                }
            }

            this.Loaded += Page_Loaded;
        }
        #endregion

        #region Инициализация

        private void InitializeCollections()
        {
            _readers = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _students = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _groups = new ObservableCollection<BookIssuanceService.GroupInfo>();
            _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            _books = new ObservableCollection<BookIssuanceService.BookInfo>();
            _todayIssues = new ObservableCollection<BookIssuanceService.IssuanceItem>();
            DatePicker.SelectedDate = DateTime.Today;
        }

        private void InitializePlaceholders()
        {
            ReaderSearchBox.Text = "Введите фамилию или номер билета...";
            ReaderSearchBox.Foreground = Brushes.Gray;

            GroupSearchBox.Text = "Введите код группы...";
            GroupSearchBox.Foreground = Brushes.Gray;

            TeacherSearchBox.Text = "Введите фамилию преподавателя...";
            TeacherSearchBox.Foreground = Brushes.Gray;

            StudentSearchBox.Text = "Введите фамилию или номер билета студента...";
            StudentSearchBox.Foreground = Brushes.Gray;

            BookSearchBox.Text = "Введите название или автора...";
            BookSearchBox.Foreground = Brushes.Gray;
        }

        private void InitializeReturnDatePicker()
        {
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
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= Page_Loaded;
            if (_isInitialDataLoaded) return;
            _isInitialDataLoaded = true;

            try
            {
                await LoadInitialDataAsync();

                if (_pendingRequestId.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"Запуск автозаполнения для заявки {_pendingRequestId}");
                    AutoFillFromRequest();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в Page_Loaded: {ex.Message}");
                ShowError("Ошибка загрузки страницы", ex);
            }
        }

        private async Task LoadInitialDataAsync()
        {
            await Task.Yield();
            await LoadStatusIdsAsync();
            await LoadReadersAsync();
            await LoadGroupsAsync();
            await LoadTeachersAsync();
            await LoadBooksAsync();
            await LoadTodayIssuesAsync();
            await UpdateStatisticsAsync();
            UpdateInventoryPreview();
        }

        #endregion

        #region Загрузка справочных данных

        private async Task LoadStatusIdsAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var statuses = new Dictionary<string, int>();
                    string query = @"
                        SELECT Title_Status, ID_Status 
                        FROM StatusBook 
                        WHERE Title_Status IN ('В наличии', 'Зарезервирована', 'Выдан')";
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            statuses[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }

                    _statusAvailableId = statuses.ContainsKey("В наличии") ? statuses["В наличии"] : 1;
                    _statusReservedId = statuses.ContainsKey("Зарезервирована") ? statuses["Зарезервирована"] : 6;
                    _statusIssuedId = statuses.ContainsKey("Выдан") ? statuses["Выдан"] : 3;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статусов: {ex.Message}");
                _statusAvailableId = 1;
                _statusReservedId = 6;
                _statusIssuedId = 3;
            }
        }

        private async Task LoadReadersAsync()
        {
            try
            {
                _readers = await Task.Run(() => _issuanceService.GetReaders());
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки читателей", ex);
            }
        }

        private async Task LoadGroupsAsync()
        {
            try
            {
                _groups = await Task.Run(() => _issuanceService.GetGroups());
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки групп", ex);
            }
        }

        private async Task LoadTeachersAsync()
        {
            try
            {
                _teachers = await Task.Run(() => _issuanceService.GetTeachers());
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки преподавателей", ex);
                _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            }
        }

        private async Task LoadBooksAsync()
        {
            try
            {
                string query = @"
                    SELECT 
                        MIN(b.IDBook) AS IDBook,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        ISNULL(SUM(b.AvailableCopies), 0) AS AvailableCopies
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE b.StatusBook NOT IN (
                        SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'
                    )
                    AND b.StatusBook NOT IN (
                        SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Утрачен%'
                    )
                    GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
                    HAVING ISNULL(SUM(b.AvailableCopies), 0) > 0
                    ORDER BY b.TitleBook";

                var books = new ObservableCollection<BookIssuanceService.BookInfo>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            books.Add(new BookIssuanceService.BookInfo
                            {
                                IDBook = reader.GetInt32(0),
                                TitleBook = reader.GetString(1),
                                AuthorName = reader.GetString(2),
                                AvailableCopies = reader.GetInt32(3)
                            });
                        }
                    }
                }

                _books = books;
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки книг", ex);
            }
        }

        private async Task LoadTodayIssuesAsync()
        {
            try
            {
                var issues = await Task.Run(() => _issuanceService.GetIssuancesByDate(DateTime.Today));
                foreach (var item in issues)
                {
                    item.OverdueColor = item.IsOverdue
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }

                _todayIssues = issues;
                IssuesDataGrid.ItemsSource = _todayIssues;
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки выдач", ex);
            }
        }

        #endregion

        #region Автоматическая статистика

        private async Task UpdateStatisticsAsync()
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                int booksIssued = 0;
                int booksReturned = 0;

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();

                        // Выдано книг за день (индивидуальные + групповые)
                        string issuedQuery = @"
                    SELECT 
                        ISNULL((SELECT COUNT(*) FROM BookIssuance WHERE CAST(IssuanceDate AS DATE) = @Date), 0)
                        + ISNULL((SELECT SUM(Quantity) FROM GroupIssuanceItems git 
                                   INNER JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId
                                   WHERE CAST(gi.IssuanceDate AS DATE) = @Date), 0) AS IssuedCount";
                        using (var cmd = new SqlCommand(issuedQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Date", selectedDate);
                            var result = cmd.ExecuteScalar();
                            booksIssued = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        }

                        // Возвращено книг за день
                        string returnedQuery = @"
                    SELECT 
                        ISNULL((SELECT COUNT(*) FROM BookIssuance WHERE ReturnDate IS NOT NULL AND CAST(ReturnDate AS DATE) = @Date), 0)
                        + ISNULL((SELECT SUM(git.Quantity) FROM GroupIssuanceItems git 
                                   INNER JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId
                                   WHERE gi.ReturnDate IS NOT NULL AND CAST(gi.ReturnDate AS DATE) = @Date), 0) AS ReturnedCount";
                        using (var cmd = new SqlCommand(returnedQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Date", selectedDate);
                            var result = cmd.ExecuteScalar();
                            booksReturned = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        }
                    }
                });

                await Dispatcher.InvokeAsync(() =>
                {
                    BooksCountStat.Text = booksIssued.ToString();
                    ReturnedBooksCountStat.Text = booksReturned.ToString();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статистики: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    BooksCountStat.Text = "0";
                    ReturnedBooksCountStat.Text = "0";
                });
            }
        }

        #endregion

        #region Автозаполнение из заявки

        public void SetDataFromRequest(int requestId, int userId, int? groupId, string userName, string groupCode, string lessonName, DateTime lessonDate, int quantity)
        {
            _pendingRequestId = requestId;
            _pendingUserId = userId;
            _pendingGroupId = groupId;
            _pendingUserName = userName;
            _pendingGroupCode = groupCode;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = null;
            _pendingQuantity = quantity;

            System.Diagnostics.Debug.WriteLine($"SetDataFromRequest: groupCode={groupCode}, quantity={quantity}");
        }

        public void SetDataFromRequestIndividual(int requestId, int userId, string userName, string lessonName, DateTime lessonDate, int userTypeId, int quantity)
        {
            _pendingRequestId = requestId;
            _pendingUserId = userId;
            _pendingUserName = userName;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = userTypeId;
            _pendingGroupId = null;
            _pendingGroupCode = "";
            _pendingQuantity = quantity;

            System.Diagnostics.Debug.WriteLine($"SetDataFromRequestIndividual: userId={userId}, quantity={quantity}");
        }

        private async void AutoFillFromRequest()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== AutoFillFromRequest START ===");

                if (_pendingGroupId.HasValue && _pendingGroupId > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Тип: ГРУППОВАЯ выдача");
                    GroupTypeRadio.IsChecked = true;

                    await LoadAndSelectGroup(_pendingGroupCode);
                    await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                    // Студент выбирается вручную
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Тип: ИНДИВИДУАЛЬНАЯ выдача");
                    ReaderTypeRadio.IsChecked = true;

                    if (_pendingUserTypeId == 3)
                        await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                    else
                        await LoadAndSelectReader(_pendingUserId.Value, _pendingUserName);
                }

                await LoadRequestBooks(_pendingRequestId.Value);

                if (_selectedBook != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Устанавливаем количество: {_pendingQuantity}");
                    QuantityBox.Text = _pendingQuantity.ToString();
                    QuantityBox.IsEnabled = true;
                    AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();
                }

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

                if (!booksFromRequest.Any())
                {
                    ShowWarning("В заявке нет книг для выдачи.");
                    return;
                }

                var first = booksFromRequest.First();

                // ИСПРАВЛЕНО: Доступно = Всего - Выдано - Списано (не вычитаем зарезервированные!)
                first.AvailableCopies = await GetRealAvailableCopiesForRequest(first.TitleBook, requestId);

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

                System.Diagnostics.Debug.WriteLine($"Книга загружена: {selected.TitleBook}, Доступно (Всего-Выдано-Списано): {selected.AvailableCopies}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadRequestBooks: {ex.Message}");
                ShowError("Ошибка загрузки книг из заявки", ex);
            }
        }
        /// <summary>
        /// Получение реального количества доступных экземпляров (НЕ вычитаем зарезервированные!)
        /// </summary>
        private async Task<int> GetRealAvailableCopiesForRequest(string title, int requestId)
        {
            try
            {
                // Используем CopiesInUse, а не подзапросы!
                string sql = @"
            SELECT 
                ISNULL(SUM(b.TotalCopies), 0) - 
                ISNULL(SUM(b.CopiesInUse), 0) AS AvailableCopies
            FROM Books b
            WHERE b.TitleBook = @Title
              AND b.StatusBook NOT IN (5) 
              AND b.ID_Disposal IS NULL"; 
        
        using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        var result = await cmd.ExecuteScalarAsync();
                        int available = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        return available;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка подсчета копий: {ex.Message}");
                return 0;
            }
        }

        private async Task<int> GetRealAvailableCopies(string title, int? excludeRequestId = null)
        {
            try
            {
                string sql;
                if (excludeRequestId.HasValue)
                {
                    sql = @"
                        SELECT ISNULL(SUM(b.AvailableCopies), 0)
                        FROM Books b
                        WHERE b.TitleBook = @Title 
                          AND (b.StatusBook IN (@StatusAvailableId, @StatusReservedId)
                               OR b.IDBook IN (SELECT ri.BookID FROM RequestItems ri WHERE ri.RequestID = @ExcludeRequestId))";
                }
                else
                {
                    sql = @"
                        SELECT ISNULL(SUM(b.AvailableCopies), 0)
                        FROM Books b
                        WHERE b.TitleBook = @Title 
                          AND b.StatusBook IN (@StatusAvailableId, @StatusReservedId)";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@StatusAvailableId", _statusAvailableId);
                        cmd.Parameters.AddWithValue("@StatusReservedId", _statusReservedId);
                        if (excludeRequestId.HasValue)
                            cmd.Parameters.AddWithValue("@ExcludeRequestId", excludeRequestId.Value);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка подсчета копий: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки преподавателя: {ex.Message}");
            }
        }

        private async Task LoadAndSelectGroup(string groupCode)
        {
            if (string.IsNullOrEmpty(groupCode)) return;

            var group = _groups.FirstOrDefault(g => g.GroupCode.Equals(groupCode, StringComparison.OrdinalIgnoreCase));

            if (group == null)
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT GroupID, GroupCode FROM Groups WHERE GroupCode = @GroupCode";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GroupCode", groupCode);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                group = new BookIssuanceService.GroupInfo
                                {
                                    GroupID = reader.GetInt32(0),
                                    GroupCode = reader.GetString(1)
                                };
                                _groups.Add(group);
                            }
                        }
                    }
                }
            }

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

            if (_pendingRequestId.HasValue)
            {
                if (_selectedBook == null)
                {
                    ShowWarning("Выберите книгу для выдачи.");
                    return false;
                }
                if (!int.TryParse(QuantityBox?.Text, out quantity) || quantity <= 0)
                {
                    ShowWarning("Укажите корректное количество.");
                    return false;
                }
            }
            else
            {
                if (_selectedBook == null)
                {
                    ShowWarning("Выберите книгу для выдачи.");
                    return false;
                }
                if (!int.TryParse(QuantityBox?.Text, out quantity) || quantity <= 0)
                {
                    ShowWarning("Укажите корректное количество.");
                    return false;
                }
                if (quantity > _selectedBook.AvailableCopies)
                {
                    ShowWarning($"Недостаточно экземпляров. Доступно: {_selectedBook.AvailableCopies}");
                    return false;
                }
            }

            if (ReaderTypeRadio.IsChecked == true)
            {
                if (_selectedReader == null)
                {
                    ShowWarning("Выберите читателя.");
                    return false;
                }
            }
            else
            {
                if (_selectedGroup == null)
                {
                    ShowWarning("Выберите группу.");
                    return false;
                }
                if (_selectedTeacher == null)
                {
                    ShowWarning("Выберите преподавателя.");
                    return false;
                }
                if (_selectedStudent == null)
                {
                    ShowWarning("Выберите студента.");
                    return false;
                }
            }

            return true;
        }

        private async void CreateIndividualIssuance(int quantity)
        {
            try
            {
                List<int> bookIds;

                if (_pendingRequestId.HasValue)
                {
                    bookIds = await GetAvailableBookIdsForIssue(_selectedBook.IDBook, quantity);
                }
                else
                {
                    bookIds = GetAvailableBookIds(_selectedBook.TitleBook, _selectedBook.AuthorName, quantity);
                }

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

                _selectedBook.AvailableCopies -= quantity;

                // ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ СТАТИСТИКИ И СПИСКА ВЫДАЧ
                await Task.Delay(100); // Небольшая задержка для синхронизации БД
                await UpdateStatisticsAsync();           // Обновляем статистику
                await LoadIssuancesByDateAsync(DatePicker.SelectedDate ?? DateTime.Today); // Обновляем список выдач
                await LoadBooksAsync();                  // Перезагружаем список книг

                ShowSuccess($"Индивидуальная выдача успешно создана!\nВыдано книг: {quantity}");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при создании выдачи", ex);
            }
        }

        private async Task<List<int>> GetAvailableBookIdsForIssue(int bookId, int requiredQuantity)
        {
            var bookIds = new List<int>();
            try
            {
                // ИСПРАВЛЕНО: Выбираем ТОЛЬКО зарезервированные книги для этой заявки
                string sql = @"
            SELECT TOP (@Qty) b.IDBook
            FROM Books b
            INNER JOIN RequestItems ri ON b.IDBook = ri.BookID
            WHERE ri.RequestID = @RequestId
              AND b.StatusBook = @StatusReservedId
              AND b.AvailableCopies > 0
            ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BookId", bookId);
                        cmd.Parameters.AddWithValue("@Qty", requiredQuantity);
                        cmd.Parameters.AddWithValue("@StatusReservedId", _statusReservedId);
                        cmd.Parameters.AddWithValue("@RequestId", _pendingRequestId.Value);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
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
                            await UpdateStatisticsAsync();
                            await LoadIssuancesByDateAsync(DatePicker.SelectedDate ?? DateTime.Today);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                _selectedBook.AvailableCopies -= quantity;
                ShowSuccess($"Групповая выдача успешно создана!\nВыдано книг: {quantity}");
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
                      AND b.StatusBook = @StatusAvailableId
                    ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@Qty", requiredQuantity);
                        cmd.Parameters.AddWithValue("@StatusAvailableId", _statusAvailableId);
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

        private List<int> GetAvailableBookIdsForGroupIssue(string title, string author, int requiredQuantity, int? requestId = null)
        {
            var bookIds = new List<int>();
            try
            {
                string sql = @"
            SELECT TOP (@Qty) b.IDBook
            FROM Books b
            INNER JOIN RequestItems ri ON b.IDBook = ri.BookID
            WHERE ri.RequestID = @RequestId
              AND b.StatusBook = @StatusReservedId
              AND b.AvailableCopies > 0
            ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@Qty", requiredQuantity);
                        cmd.Parameters.AddWithValue("@StatusReservedId", _statusReservedId);
                        cmd.Parameters.AddWithValue("@RequestId", requestId.HasValue ? (object)requestId.Value : DBNull.Value);

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
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книг для группы: {ex.Message}");
            }
            return bookIds;
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
            if (ReaderSearchSection != null)
                ReaderSearchSection.Visibility = (ReaderTypeRadio.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (GroupSearchSection != null)
                GroupSearchSection.Visibility = (ReaderTypeRadio.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;

            if (ReturnDatePicker != null)
            {
                if (ReaderTypeRadio.IsChecked == true)
                    ReturnDatePicker.SelectedDate = DateTime.Now.AddDays(14);
                else
                    ReturnDatePicker.SelectedDate = DateTime.Now.AddDays(30);
            }

            ResetQuantity();
        }

        private async void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateStatisticsAsync();
            await LoadIssuancesByDateAsync(DatePicker.SelectedDate ?? DateTime.Today);
        }

        private async Task LoadIssuancesByDateAsync(DateTime date)
        {
            try
            {
                var issues = await Task.Run(() => _issuanceService.GetIssuancesByDate(date));
                foreach (var item in issues)
                {
                    item.OverdueColor = item.IsOverdue
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }
                _todayIssues = issues;
                IssuesDataGrid.ItemsSource = _todayIssues;
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки выдач", ex);
            }
        }

        private void ReturnBooksButton_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new ReturnBooksPage());

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) => e.Handled = !char.IsDigit(e.Text, 0);

        private void UpdateInventoryPreview()
        {
            if (_selectedBook == null) return;
            AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();
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

        private void ClearPlaceholder(TextBox box, string placeholder)
        {
            if (box.Text == placeholder)
            {
                box.Text = "";
                box.Foreground = Brushes.Black;
            }
        }

        private void RestorePlaceholder(TextBox box, string placeholder, ListBox list)
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = placeholder;
                box.Foreground = Brushes.Gray;
                list.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsPlaceholder(string text, string placeholder) => string.IsNullOrWhiteSpace(text) || text == placeholder;

        private bool MatchReader(BookIssuanceService.ReaderInfo r, string search)
        {
            string s = search.ToLower();
            return r.LastName.ToLower().Contains(s) || r.FirstName.ToLower().Contains(s) || r.CardNumber.ToLower().Contains(s);
        }

        private void ShowFilteredList(ListBox listBox, System.Collections.IList items)
        {
            if (items.Count > 0)
            {
                listBox.ItemsSource = items;
                listBox.Visibility = Visibility.Visible;
            }
            else
            {
                listBox.Visibility = Visibility.Collapsed;
            }
        }

        // Читатели
        private void ReaderSearchBox_GotFocus(object sender, RoutedEventArgs e) => ClearPlaceholder(ReaderSearchBox, "Введите фамилию или номер билета...");
        private void ReaderSearchBox_LostFocus(object sender, RoutedEventArgs e) => RestorePlaceholder(ReaderSearchBox, "Введите фамилию или номер билета...", ReadersListBox);
        private void ReaderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPlaceholder(ReaderSearchBox.Text, "Введите фамилию или номер билета...")) { ReadersListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _readers.Where(r => MatchReader(r, ReaderSearchBox.Text)).Take(10).ToList();
            ShowFilteredList(ReadersListBox, filtered);
        }
        private void ReadersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReadersListBox.SelectedItem is BookIssuanceService.ReaderInfo selected)
            {
                _selectedReader = selected;
                ReaderSearchBox.Text = selected.DisplayInfo;
                ReaderSearchBox.Foreground = Brushes.Black;
                ReadersListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Группы
        private void GroupSearchBox_GotFocus(object sender, RoutedEventArgs e) => ClearPlaceholder(GroupSearchBox, "Введите код группы...");
        private void GroupSearchBox_LostFocus(object sender, RoutedEventArgs e) => RestorePlaceholder(GroupSearchBox, "Введите код группы...", GroupsListBox);
        private void GroupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPlaceholder(GroupSearchBox.Text, "Введите код группы...")) { GroupsListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _groups.Where(g => g.GroupCode.ToLower().Contains(GroupSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(GroupsListBox, filtered);
        }
        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsListBox.SelectedItem is BookIssuanceService.GroupInfo selected)
            {
                _selectedGroup = selected;
                GroupSearchBox.Text = selected.GroupCode;
                GroupSearchBox.Foreground = Brushes.Black;
                GroupsListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Преподаватели
        private void TeacherSearchBox_GotFocus(object sender, RoutedEventArgs e) => ClearPlaceholder(TeacherSearchBox, "Введите фамилию преподавателя...");
        private void TeacherSearchBox_LostFocus(object sender, RoutedEventArgs e) => RestorePlaceholder(TeacherSearchBox, "Введите фамилию преподавателя...", TeachersListBox);
        private void TeacherSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPlaceholder(TeacherSearchBox.Text, "Введите фамилию преподавателя...")) { TeachersListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _teachers.Where(t => t.LastName.ToLower().Contains(TeacherSearchBox.Text.ToLower()) || t.FirstName.ToLower().Contains(TeacherSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(TeachersListBox, filtered);
        }
        private void TeachersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeachersListBox.SelectedItem is BookIssuanceService.TeacherInfo selected)
            {
                _selectedTeacher = selected;
                TeacherSearchBox.Text = selected.FullName;
                TeacherSearchBox.Foreground = Brushes.Black;
                TeachersListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Студенты
        private void StudentSearchBox_GotFocus(object sender, RoutedEventArgs e) => ClearPlaceholder(StudentSearchBox, "Введите фамилию или номер билета студента...");
        private void StudentSearchBox_LostFocus(object sender, RoutedEventArgs e) => RestorePlaceholder(StudentSearchBox, "Введите фамилию или номер билета студента...", StudentsListBox);
        private void StudentSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPlaceholder(StudentSearchBox.Text, "Введите фамилию или номер билета студента...")) { StudentsListBox.Visibility = Visibility.Collapsed; return; }
            _students = _issuanceService.GetReadersStudent(StudentSearchBox.Text);
            ShowFilteredList(StudentsListBox, _students.ToList());
        }
        private void StudentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentsListBox.SelectedItem is BookIssuanceService.ReaderInfo selected)
            {
                _selectedStudent = selected;
                StudentSearchBox.Text = selected.DisplayInfo;
                StudentSearchBox.Foreground = Brushes.Black;
                StudentsListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Книги
        private void BookSearchBox_GotFocus(object sender, RoutedEventArgs e) => ClearPlaceholder(BookSearchBox, "Введите название или автора...");
        private void BookSearchBox_LostFocus(object sender, RoutedEventArgs e) => RestorePlaceholder(BookSearchBox, "Введите название или автора...", BooksListBox);
        private void BookSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPlaceholder(BookSearchBox.Text, "Введите название или автора...")) { BooksListBox.Visibility = Visibility.Collapsed; return; }
            var filtered = _books.Where(b => b.TitleBook.ToLower().Contains(BookSearchBox.Text.ToLower()) || b.AuthorName.ToLower().Contains(BookSearchBox.Text.ToLower())).Take(10).ToList();
            ShowFilteredList(BooksListBox, filtered);
        }
        private void BooksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BooksListBox.SelectedItem is BookIssuanceService.BookInfo selected)
            {
                _selectedBook = selected;
                SelectedBookTitle.Text = selected.TitleBook;
                SelectedBookAuthor.Text = selected.AuthorName;
                SelectedBookPanel.Visibility = Visibility.Visible;
                BookSearchBox.Text = selected.TitleBook;
                BookSearchBox.Foreground = Brushes.Black;
                BooksListBox.Visibility = Visibility.Collapsed;
                AvailableQuantityText.Text = selected.AvailableCopies.ToString();
                UpdateInventoryPreview();
            }
        }

        private void ClearSelectedBook_Click(object sender, RoutedEventArgs e)
        {
            _selectedBook = null;
            SelectedBookPanel.Visibility = Visibility.Collapsed;
            BookSearchBox.Text = "Введите название или автора...";
            BookSearchBox.Foreground = Brushes.Gray;
            AvailableQuantityText.Text = "0";
        }

        #endregion

        #region Вспомогательные методы

        private void ExecuteNonQuery(SqlConnection conn, string sql, params (string, object)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var p in parameters)
                    cmd.Parameters.AddWithValue(p.Item1, p.Item2);
                cmd.ExecuteNonQuery();
            }
        }

        private void ExecuteNonQuery(SqlConnection conn, string sql, SqlTransaction transaction, params (string, object)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, conn, transaction))
            {
                foreach (var p in parameters)
                    cmd.Parameters.AddWithValue(p.Item1, p.Item2);
                cmd.ExecuteNonQuery();
            }
        }

        private void ShowWarning(string message) => CustomMessageBox.Show(message, "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
        private void ShowSuccess(string message) => CustomMessageBox.Show(message, "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        private void ShowError(string title, Exception ex) => CustomMessageBox.Show($"{title}: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);

        #endregion
    }
}