using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class BookRequestsManagement : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<RequestItem> _allRequests;
        private int _librarianId;
        private System.Timers.Timer _notificationTimer;

        // Словари статусов из БД
        private Dictionary<int, string> _requestStatuses = new Dictionary<int, string>();
        private Dictionary<int, string> _itemStatuses = new Dictionary<int, string>();

        // Храним ID уже показанных уведомлений
        private HashSet<int> _shownNotificationIds = new HashSet<int>();

        #region Вспомогательные классы

        public class NotificationDetail
        {
            public int ID_Notification { get; set; }
            public int RequestID { get; set; }
            public string Message { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public bool IsRead { get; set; }
            public string UserName { get; set; } = "";
            public string RequestNumber { get; set; } = "";
            public List<BookDetail> Books { get; set; } = new List<BookDetail>();
        }

        public class BookDetail
        {
            public string Title { get; set; } = "";
            public string Author { get; set; } = "";
            public int Quantity { get; set; }
        }

        public class RequestItem
        {
            public int ID_Request { get; set; }
            public string RequestNumber { get; set; } = "";
            public DateTime RequestDate { get; set; }
            public int UserID { get; set; }
            public string UserName { get; set; } = "";
            public int? GroupID { get; set; }
            public string GroupCode { get; set; } = "";
            public string LessonName { get; set; } = "";
            public DateTime LessonDate { get; set; }
            public int StatusID { get; set; }
            public string StatusName { get; set; } = "";
            public int TotalBooks { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public int UserTypeID { get; set; }

            public SolidColorBrush StatusColor
            {
                get
                {
                    switch (StatusName)
                    {
                        case "Новая": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
                        case "В обработке": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
                        case "Выдана": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C"));
                        case "Отменена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                        case "Просрочена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                        default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                    }
                }
            }

            public Visibility ConfirmVisible => (StatusName == "Новая") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility IssueVisible => (StatusName == "В обработке") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility CancelVisible => (StatusName != "Выдана" && StatusName != "Отменена" && StatusName != "Просрочена") ? Visibility.Visible : Visibility.Collapsed;
        }

        public class RequestBookItem
        {
            public int ID_RequestItem { get; set; }
            public int BookID { get; set; }
            public string BookTitle { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public int StatusID { get; set; }
            public string StatusName { get; set; } = "";
            public Visibility CancelVisible => (StatusName == "Зарезервирована" || StatusName == "Ожидает") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility IssueVisible => (StatusName == "Зарезервирована" || StatusName == "Ожидает") ? Visibility.Visible : Visibility.Collapsed;
        }
        #region Конструктор и загрузка

        public BookRequestsManagement(int librarianId)
        {
            InitializeComponent();
            _librarianId = librarianId;
            _notificationTimer = new System.Timers.Timer(30000);
            _notificationTimer.Elapsed += async (sender, e) => await CheckNewRequests();
            _notificationTimer.Start();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatuses();
            LoadStatusFilters();
            await LoadDataAsync();
        }

        /// <summary>
        /// Загрузка статусов из БД в словари
        /// </summary>
        private void LoadStatuses()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("SELECT ID_Status, StatusName FROM RequestStatuses ORDER BY ID_Status", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            _requestStatuses[reader.GetInt32(0)] = reader.GetString(1);
                    }

                    using (var cmd = new SqlCommand("SELECT ID_Status, StatusName FROM RequestItemStatuses ORDER BY ID_Status", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            _itemStatuses[reader.GetInt32(0)] = reader.GetString(1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статусов: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка статусов в выпадающий список фильтра
        /// </summary>
        private void LoadStatusFilters()
        {
            CbStatusFilter.Items.Clear();
            CbStatusFilter.Items.Add(new ComboBoxItem { Content = "Все заявки", IsSelected = true });

            foreach (var status in _requestStatuses.Values)
            {
                CbStatusFilter.Items.Add(new ComboBoxItem { Content = status });
            }
        }

        private int GetRequestStatusId(string statusName)
        {
            var pair = _requestStatuses.FirstOrDefault(s => s.Value == statusName);
            return pair.Key != 0 ? pair.Key : -1;
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                await LoadRequests();
                await LoadUnreadNotificationsCount();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        #endregion

        #region Уведомления

        private async System.Threading.Tasks.Task LoadUnreadNotificationsCount()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM RequestNotifications WHERE IsRead = 0", conn))
                    {
                        int count = (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки счетчика уведомлений: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task CheckNewRequests()
        {
            try
            {
                List<NotificationDetail> newNotifications = new List<NotificationDetail>();

                string query = @"
                    SELECT 
                        n.ID_Notification, n.RequestID, n.Message, n.CreatedDate, n.IsRead,
                        CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                        r.RequestNumber
                    FROM RequestNotifications n
                    INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                    LEFT JOIN ReaderForm rd ON br.UserID = rd.ReaderID
                    LEFT JOIN BookRequests r ON n.RequestID = r.ID_Request
                    WHERE n.IsRead = 0 
                      AND n.CreatedDate > DATEADD(minute, -1, GETDATE())
                    ORDER BY n.CreatedDate DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = Convert.ToInt32(reader["ID_Notification"]);
                            if (!_shownNotificationIds.Contains(id))
                            {
                                var detail = new NotificationDetail
                                {
                                    ID_Notification = id,
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    Message = reader["Message"].ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                    IsRead = Convert.ToBoolean(reader["IsRead"]),
                                    UserName = reader["UserName"]?.ToString() ?? "Неизвестный пользователь",
                                    RequestNumber = reader["RequestNumber"]?.ToString() ?? ""
                                };
                                await LoadNotificationBooks(detail.RequestID, detail);
                                newNotifications.Add(detail);
                                _shownNotificationIds.Add(id);
                            }
                        }
                    }
                }

                if (newNotifications.Any())
                {
                    Dispatcher.Invoke(() =>
                    {
                        SystemSounds.Beep.Play();
                        string message = "";
                        foreach (var notification in newNotifications)
                        {
                            string cleanMessage = notification.Message;
                            int userIndex = cleanMessage.IndexOf("от пользователя ID");
                            if (userIndex > 0)
                                cleanMessage = cleanMessage.Substring(0, userIndex).Trim();

                            message += $"📋 {cleanMessage}\n";
                            message += $"👤 От: {notification.UserName}\n";
                            message += $"📚 Книги:\n";
                            if (notification.Books.Any())
                                foreach (var book in notification.Books)
                                    message += $"   • {book.Title} - {book.Author} ({book.Quantity} шт.)\n";
                            else
                                message += $"   • Книги не указаны\n";
                            message += $"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
                        }
                        CustomMessageBox.Show(message, "📢 Новые заявки на книги",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        _ = LoadRequests();
                        _ = LoadUnreadNotificationsCount();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки новых заявок: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadNotificationBooks(int requestId, NotificationDetail detail)
        {
            try
            {
                string query = @"
                    SELECT b.TitleBook,
                           ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                           ri.Quantity
                    FROM RequestItems ri
                    JOIN Books b ON ri.BookID = b.IDBook
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE ri.RequestID = @RequestId";

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
                                detail.Books.Add(new BookDetail
                                {
                                    Title = reader["TitleBook"]?.ToString() ?? "",
                                    Author = reader["AuthorName"]?.ToString() ?? "",
                                    Quantity = Convert.ToInt32(reader["Quantity"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг для уведомления: {ex.Message}");
            }
        }

        #endregion

        #region Загрузка заявок

        private async System.Threading.Tasks.Task LoadRequests()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                string query = @"
            SELECT 
                r.ID_Request, 
                r.RequestNumber, 
                r.RequestDate,
                r.UserID,
                CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                r.GroupID, 
                g.GroupCode, 
                r.LessonName, 
                r.LessonDate,
                r.StatusID, 
                rs.StatusName, 
                r.ExpirationDate, 
                r.UserTypeID,
                (SELECT ISNULL(SUM(Quantity), 0) FROM RequestItems WHERE RequestID = r.ID_Request) AS TotalBooks
            FROM BookRequests r
            LEFT JOIN ReaderForm rd ON r.UserID = rd.ReaderID
            LEFT JOIN Groups g ON r.GroupID = g.GroupID
            LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
            ORDER BY r.RequestDate DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<RequestItem>();

                        while (reader.Read())
                        {
                            var item = new RequestItem
                            {
                                ID_Request = Convert.ToInt32(reader["ID_Request"]),
                                RequestNumber = reader["RequestNumber"]?.ToString() ?? "",
                                RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                UserID = Convert.ToInt32(reader["UserID"]),
                                UserName = reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "Неизвестный пользователь",
                                GroupID = reader["GroupID"] != DBNull.Value ? Convert.ToInt32(reader["GroupID"]) : (int?)null,
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                LessonName = reader["LessonName"]?.ToString() ?? "",
                                LessonDate = Convert.ToDateTime(reader["LessonDate"]),
                                StatusID = Convert.ToInt32(reader["StatusID"]),
                                StatusName = reader["StatusName"]?.ToString() ?? "",
                                ExpirationDate = reader["ExpirationDate"] != DBNull.Value ? Convert.ToDateTime(reader["ExpirationDate"]) : (DateTime?)null,
                                TotalBooks = Convert.ToInt32(reader["TotalBooks"]),
                                UserTypeID = reader["UserTypeID"] != DBNull.Value ? Convert.ToInt32(reader["UserTypeID"]) : 1
                            };
                            items.Add(item);
                        }

                        Dispatcher.Invoke(() =>
                        {
                            _allRequests = items;
                            ApplyFilters();
                        });
                    }
                }
            });
        }
        /// <summary>
        /// Выдача отдельной книги из заявки
        /// </summary>
        /// <summary>
        /// Выдача отдельной книги из заявки (открывает форму выдачи)
        /// </summary>
        private async void IssueSingleBook_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;
            int requestItemId = (int)btn.Tag;

            // Находим книгу в текущем списке
            var bookItem = _currentRequestBooks.FirstOrDefault(b => b.ID_RequestItem == requestItemId);
            if (bookItem == null) return;

            var request = _allRequests?.FirstOrDefault(r => r.ID_Request == _currentRequestId);
            if (request == null) return;

            // Проверяем статус заявки
            int processingStatusId = GetRequestStatusId("В обработке");
            if (processingStatusId > 0 && request.StatusID != processingStatusId)
            {
                CustomMessageBox.Show($"Книги по заявке еще не готовы к выдаче. Сначала подтвердите заявку.\nТекущий статус: {request.StatusName}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var confirmResult = CustomMessageBox.Show(
                $"Выдать книгу \"{bookItem.BookTitle}\" (кол-во: {bookItem.Quantity} шт.) по заявке №{request.RequestNumber}?",
                "Подтверждение выдачи",
                CustomMessageBoxButton.YesNo,
                CustomMessageBoxImage.Question);

            if (confirmResult != CustomMessageBoxResult.Yes) return;

            // Создаем список книг для передачи (только выбранная книга)
            var booksForPage = new List<InformationServicePage.RequestBookItem>
    {
        new InformationServicePage.RequestBookItem
        {
            ID_RequestItem = bookItem.ID_RequestItem,
            BookID = bookItem.BookID,
            BookTitle = bookItem.BookTitle,
            AuthorName = bookItem.AuthorName,
            Quantity = bookItem.Quantity,
            StatusID = bookItem.StatusID,
            StatusName = bookItem.StatusName
        }
    };

            // Переходим на страницу информационного обслуживания для выдачи
            var infoServicePage = new InformationServicePage(
                requestId: _currentRequestId,
                userId: request.UserID,
                groupId: request.GroupID,
                userName: request.UserName,
                groupCode: request.GroupCode,
                lessonName: request.LessonName,
                lessonDate: request.LessonDate,
                userTypeId: request.UserTypeID,
                quantity: bookItem.Quantity,
                requestBooks: booksForPage
            );

            NavigationService?.Navigate(infoServicePage);
        }

        /// <summary>
        /// Получение доступных ID книг для выдачи
        /// </summary>
        private async Task<List<int>> GetAvailableBookIdsForIssuance(int bookId, int quantity, int requestId)
        {
            var bookIds = new List<int>();
            try
            {
                string sql = @"
            SELECT TOP (@Qty) b.IDBook
            FROM Books b
            WHERE b.TitleBook = (SELECT TitleBook FROM Books WHERE IDBook = @BookId)
              AND (
                  b.StatusBook = (SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%')
                  OR (
                      b.StatusBook = (SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%')
                      AND EXISTS (
                          SELECT 1 FROM RequestItems ri 
                          WHERE ri.BookID = b.IDBook 
                            AND ri.RequestID = @RequestId 
                            AND ri.StatusID = 2
                      )
                  )
              )
              AND b.AvailableCopies > 0
            ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BookId", bookId);
                        cmd.Parameters.AddWithValue("@Qty", quantity);
                        cmd.Parameters.AddWithValue("@RequestId", requestId);

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

        #endregion

        #region Фильтры

        private void ApplyFilters()
        {
            if (_allRequests == null) return;

            string searchTerm = TbSearch.Text?.Trim().ToLower() ?? "";
            string statusFilter = (CbStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все заявки";

            var filtered = _allRequests.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(r =>
                    r.RequestNumber.ToLower().Contains(searchTerm) ||
                    r.UserName.ToLower().Contains(searchTerm) ||
                    r.GroupCode.ToLower().Contains(searchTerm) ||
                    r.LessonName.ToLower().Contains(searchTerm));
            }

            if (statusFilter != "Все заявки")
                filtered = filtered.Where(r => r.StatusName == statusFilter);

            DgRequests.ItemsSource = filtered.ToList();
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        #endregion

        #region Детали заявки

        private async void ViewRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
                await ShowRequestDetails((int)btn.Tag);
        }

        private void CloseDetailsPanel_Click(object sender, RoutedEventArgs e)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            DgRequestItems.ItemsSource = null;
        }

        private async System.Threading.Tasks.Task ShowRequestDetails(int requestId)
        {
            _currentRequestId = requestId;
            _currentRequestBooks.Clear();

            try
            {
                string query = @"
            SELECT 
                r.RequestNumber,
                CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                rd.TypeID AS UserTypeID,
                r.LessonName, r.LessonDate, r.RequestDate,
                g.GroupCode, rs.StatusName, r.ExpirationDate,
                ri.ID_RequestItem, ri.BookID,
                b.TitleBook,
                ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                ri.Quantity,
                ris.StatusName AS ItemStatusName,
                ri.StatusID
            FROM BookRequests r
            LEFT JOIN ReaderForm rd ON r.UserID = rd.ReaderID
            LEFT JOIN Groups g ON r.GroupID = g.GroupID
            LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
            LEFT JOIN RequestItems ri ON r.ID_Request = ri.RequestID
            LEFT JOIN Books b ON ri.BookID = b.IDBook
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            LEFT JOIN RequestItemStatuses ris ON ri.StatusID = ris.ID_Status
            WHERE r.ID_Request = @RequestId";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            bool hasData = false;
                            int userTypeID = 0;
                            string statusName = "";

                            while (await reader.ReadAsync())
                            {
                                if (!hasData)
                                {
                                    userTypeID = reader["UserTypeID"] != DBNull.Value ? Convert.ToInt32(reader["UserTypeID"]) : 0;
                                    string userTypeName = userTypeID == 1 ? "Студент" : (userTypeID == 3 ? "Сотрудник" : "Неизвестно");
                                    statusName = reader["StatusName"]?.ToString() ?? "";

                                    Dispatcher.Invoke(() =>
                                    {
                                        TbRequestNumber.Text = $"Заявка №: {reader["RequestNumber"]}";
                                        TbUserName.Text = $"Заявитель: {reader["UserName"]}";
                                        TbUserType.Text = $"Тип: {userTypeName}";
                                        TbLessonInfo.Text = $"Занятие: {reader["LessonName"]}";
                                        TbRequestDate.Text = $"Дата создания: {Convert.ToDateTime(reader["RequestDate"]):dd.MM.yyyy HH:mm}";
                                        TbGroupInfo.Text = $"Группа: {reader["GroupCode"]?.ToString() ?? "Не указана"}";
                                        TbStatus.Text = $"Статус: {statusName}";
                                        TbExpirationDate.Text = reader["ExpirationDate"] != DBNull.Value
                                            ? $"Срок брони: {Convert.ToDateTime(reader["ExpirationDate"]):dd.MM.yyyy}"
                                            : "";
                                    });
                                    hasData = true;
                                }

                                if (reader["ID_RequestItem"] != DBNull.Value)
                                {
                                    var bookItem = new RequestBookItem
                                    {
                                        ID_RequestItem = Convert.ToInt32(reader["ID_RequestItem"]),
                                        BookID = Convert.ToInt32(reader["BookID"]),
                                        BookTitle = reader["TitleBook"]?.ToString() ?? "Не указано",
                                        AuthorName = reader["AuthorName"]?.ToString() ?? "Не указан",
                                        Quantity = Convert.ToInt32(reader["Quantity"]),
                                        StatusName = reader["ItemStatusName"]?.ToString() ?? "Неизвестен",
                                        StatusID = Convert.ToInt32(reader["StatusID"])
                                    };
                                    _currentRequestBooks.Add(bookItem);
                                }
                            }

                            Dispatcher.Invoke(() =>
                            {
                                // Используем непосредственно список _currentRequestBooks
                                DgRequestItems.ItemsSource = _currentRequestBooks;
                                DetailsPanel.Visibility = Visibility.Visible;

                                if (_currentRequestBooks.Count == 0)
                                {
                                    var emptyList = new ObservableCollection<RequestBookItem>
                            {
                                new RequestBookItem { BookTitle = "Нет книг в заявке", AuthorName = "", Quantity = 0, StatusName = "" }
                            };
                                    DgRequestItems.ItemsSource = emptyList;
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки деталей: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        #endregion


        #region Действия с заявками
        private int _currentRequestId = 0;
        private ObservableCollection<RequestBookItem> _currentRequestBooks = new ObservableCollection<RequestBookItem>();

        private async void ConfirmRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;
            int requestId = (int)btn.Tag;

            var request = _allRequests.FirstOrDefault(r => r.ID_Request == requestId);
            if (request == null)
            {
                CustomMessageBox.Show("Заявка не найдена.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                return;
            }

            var result = CustomMessageBox.Show("Подтвердить заявку? Книги будут зарезервированы.", "Подтверждение",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("sp_ReserveBooksAtomic", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@RequestID", requestId);
                        cmd.Parameters.AddWithValue("@UserID", request.UserID);
                        cmd.Parameters.AddWithValue("@ReserveDays", 3);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                CustomMessageBox.Show("Заявка подтверждена! Книги зарезервированы.", "Успешно",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                await LoadRequests();
                await LoadUnreadNotificationsCount();
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                CustomMessageBox.Show("Система временно перегружена. Повторите попытку.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
            catch (SqlException ex) when (ex.Message.Contains("недостаточно") || ex.Message.Contains("Недостаточно"))
            {
                CustomMessageBox.Show("Недостаточно экземпляров книг для резервирования.\n\nВозможно, другие заявки уже забрали эти книги.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка подтверждения: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
      

        private void IssueBooks_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;
            int requestId = (int)btn.Tag;

            var request = _allRequests.FirstOrDefault(r => r.ID_Request == requestId);
            if (request == null) return;

            int cancelledId = GetRequestStatusId("Отменена");
            if (cancelledId > 0 && request.StatusID == cancelledId)
            {
                CustomMessageBox.Show("Нельзя выдать книги по отмененной заявке.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            int processingStatusId = GetRequestStatusId("В обработке");
            if (processingStatusId > 0 && request.StatusID != processingStatusId)
            {
                CustomMessageBox.Show($"Книги по заявке еще не готовы к выдаче. Сначала подтвердите заявку. Текущий статус: {request.StatusName}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var confirmResult = CustomMessageBox.Show(
                $"Выдать книги по заявке №{request.RequestNumber}?\n\nПользователь: {request.UserName}\nКниг: {request.TotalBooks} шт.",
                "Подтверждение выдачи", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (confirmResult != CustomMessageBoxResult.Yes) return;

            // ПЕРЕДАЁМ ВСЕ ДАННЫЕ ЧЕРЕЗ КОНСТРУКТОР!
            var infoServicePage = new InformationServicePage(
                requestId: requestId,
                userId: request.UserID,
                groupId: request.GroupID,
                userName: request.UserName,
                groupCode: request.GroupCode,
                lessonName: request.LessonName,
                lessonDate: request.LessonDate,
                userTypeId: request.UserTypeID,
                quantity: request.TotalBooks
            );

            NavigationService?.Navigate(infoServicePage);
        }

        private async void CancelRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;
            int requestId = (int)btn.Tag;

            var result = CustomMessageBox.Show("Отменить заявку? Зарезервированные книги будут возвращены в доступные.",
                "Подтверждение отмены", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                int cancelledRequestStatusId = GetRequestStatusId("Отменена");
                if (cancelledRequestStatusId <= 0) cancelledRequestStatusId = 5;

                // Ищем ID статуса "Отменена" для позиций заявки
                int cancelledItemStatusId = _itemStatuses
                    .FirstOrDefault(s => s.Value == "Отменена").Key;
                if (cancelledItemStatusId <= 0) cancelledItemStatusId = 4;

                // Получаем ID статуса книги "В наличии"
                int availableBookStatusId;
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(
                        "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%'", conn))
                    {
                        availableBookStatusId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Возвращаем зарезервированные книги в статус "В наличии"
                            using (var cmd = new SqlCommand(@"
                        UPDATE b
                        SET b.StatusBook = @AvailableStatus
                        FROM Books b
                        JOIN RequestItems ri ON b.IDBook = ri.BookID
                        WHERE ri.RequestID = @RequestId 
                          AND ri.StatusID = 2 
                          AND b.StatusBook = (
                              SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                          )", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@AvailableStatus", availableBookStatusId);
                                cmd.Parameters.AddWithValue("@RequestId", requestId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. Снимаем резерв с позиций заявки
                            using (var cmd = new SqlCommand(@"
                        UPDATE RequestItems 
                        SET StatusID = @CancelledItemStatus 
                        WHERE RequestID = @RequestId AND StatusID = 2", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@CancelledItemStatus", cancelledItemStatusId);
                                cmd.Parameters.AddWithValue("@RequestId", requestId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 3. Меняем статус заявки на "Отменена"
                            using (var cmd = new SqlCommand(
                                "UPDATE BookRequests SET StatusID = @StatusId WHERE ID_Request = @RequestId",
                                conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@StatusId", cancelledRequestStatusId);
                                cmd.Parameters.AddWithValue("@RequestId", requestId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                CustomMessageBox.Show("Заявка отменена. Книги возвращены в доступные.", "Успешно",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                await LoadRequests();
                await LoadUnreadNotificationsCount();
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка отмены: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Отмена отдельной позиции книги в заявке
        /// </summary>
        private async void CancelBookItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;
            int requestItemId = (int)btn.Tag;

            // Находим книгу в текущем списке
            var bookItem = _currentRequestBooks.FirstOrDefault(b => b.ID_RequestItem == requestItemId);
            if (bookItem == null) return;

            var result = CustomMessageBox.Show(
                $"Отменить книгу \"{bookItem.BookTitle}\" (кол-во: {bookItem.Quantity} шт.) из заявки?",
                "Подтверждение отмены",
                CustomMessageBoxButton.YesNo,
                CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                // Получаем ID статуса "Отменена" для позиции заявки
                int cancelledItemStatusId = _itemStatuses
                    .FirstOrDefault(s => s.Value == "Отменена").Key;
                if (cancelledItemStatusId <= 0) cancelledItemStatusId = 4;

                // Получаем ID статуса книги "В наличии"
                int availableBookStatusId;
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(
                        "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%'", conn))
                    {
                        availableBookStatusId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Снимаем резерв с книг
                            using (var cmd = new SqlCommand(@"
                        UPDATE b
                        SET b.StatusBook = @AvailableStatus
                        FROM Books b
                        WHERE b.IDBook IN (
                            SELECT BookID FROM RequestItems WHERE ID_RequestItem = @RequestItemId
                        )
                        AND b.StatusBook = (
                            SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%'
                        )", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@AvailableStatus", availableBookStatusId);
                                cmd.Parameters.AddWithValue("@RequestItemId", requestItemId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. Отменяем позицию заявки
                            using (var cmd = new SqlCommand(
                                "UPDATE RequestItems SET StatusID = @StatusId WHERE ID_RequestItem = @RequestItemId",
                                conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@StatusId", cancelledItemStatusId);
                                cmd.Parameters.AddWithValue("@RequestItemId", requestItemId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                CustomMessageBox.Show($"Книга \"{bookItem.BookTitle}\" отменена.", "Успешно",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                // Обновляем детали заявки
                await ShowRequestDetails(_currentRequestId);
                await LoadRequests();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка отмены: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        #endregion

        #region События DataGrid

        private void DgRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRequests.SelectedItem is RequestItem selected)
                _ = ShowRequestDetails(selected.ID_Request);
        }

        #endregion
    }
}
#endregion
