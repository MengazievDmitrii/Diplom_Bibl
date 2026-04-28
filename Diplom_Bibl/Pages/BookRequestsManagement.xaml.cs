using Diplom_Bibl.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class BookRequestsManagement : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<RequestItem> _allRequests;
        private int _librarianId;
        private System.Timers.Timer _notificationTimer;

        // Храним ID уже показанных уведомлений
        private HashSet<int> _shownNotificationIds = new HashSet<int>();

        // Класс для деталей уведомления
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

        // Класс для отображения заявки
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
                        case "Готова": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                        case "Выдана": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C"));
                        case "Отменена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                        case "Просрочена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                        default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                    }
                }
            }

            public Visibility ConfirmVisible => (StatusName == "Новая") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility IssueVisible => (StatusName == "В обработке" || StatusName == "Готова") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility CancelVisible => (StatusName != "Выдана" && StatusName != "Отменена" && StatusName != "Просрочена") ? Visibility.Visible : Visibility.Collapsed;
        }

        // Класс для элемента заявки (книги)
        public class RequestBookItem
        {
            public int ID_RequestItem { get; set; }
            public int BookID { get; set; }
            public string BookTitle { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public int StatusID { get; set; }
            public string StatusName { get; set; } = "";
        }

        public BookRequestsManagement(int librarianId)
        {
            InitializeComponent();
            _librarianId = librarianId;

            // Запускаем таймер для проверки новых заявок каждые 30 секунд
            _notificationTimer = new System.Timers.Timer(30000);
            _notificationTimer.Elapsed += async (sender, e) => await CheckNewRequests();
            _notificationTimer.Start();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
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

        private async System.Threading.Tasks.Task LoadUnreadNotificationsCount()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT COUNT(*) FROM RequestNotifications WHERE IsRead = 0";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        int count = (int)await cmd.ExecuteScalarAsync();
                        // Обновляем счетчик уведомлений (если есть такой элемент в XAML)
                        //NotificationBadge.Text = count.ToString();
                        //NotificationBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
                // Получаем только НОВЫЕ непрочитанные уведомления (не старше 1 минуты)
                List<NotificationDetail> newNotifications = new List<NotificationDetail>();

                string query = @"
                    SELECT 
                        n.ID_Notification,
                        n.RequestID,
                        n.Message,
                        n.CreatedDate,
                        n.IsRead,
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

                                // Получаем книги из заявки
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

                        // Формируем подробное сообщение для каждой заявки
                        string message = "";
                        foreach (var notification in newNotifications)
                        {
                            // Убираем "от пользователя ID X" из сообщения
                            string cleanMessage = notification.Message;
                            int userIndex = cleanMessage.IndexOf("от пользователя ID");
                            if (userIndex > 0)
                            {
                                cleanMessage = cleanMessage.Substring(0, userIndex).Trim();
                            }

                            message += $"📋 {cleanMessage}\n";
                            message += $"👤 От: {notification.UserName}\n";
                            message += $"📚 Книги:\n";

                            if (notification.Books.Any())
                            {
                                foreach (var book in notification.Books)
                                {
                                    message += $"   • {book.Title} - {book.Author} ({book.Quantity} шт.)\n";
                                }
                            }
                            else
                            {
                                message += $"   • Книги не указаны\n";
                            }
                            message += $"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
                        }

                        CustomMessageBox.Show(message, "📢 Новые заявки на книги",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                        // Обновляем список заявок и счетчик
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
                    SELECT 
                        b.TitleBook,
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
                        (SELECT SUM(Quantity) FROM RequestItems WHERE RequestID = r.ID_Request) AS TotalBooks
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
                            items.Add(new RequestItem
                            {
                                ID_Request = Convert.ToInt32(reader["ID_Request"]),
                                RequestNumber = reader["RequestNumber"].ToString(),
                                RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                UserID = Convert.ToInt32(reader["UserID"]),
                                UserName = reader["UserName"].ToString(),
                                GroupID = reader["GroupID"] != DBNull.Value ? Convert.ToInt32(reader["GroupID"]) : (int?)null,
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                LessonName = reader["LessonName"]?.ToString() ?? "",
                                LessonDate = Convert.ToDateTime(reader["LessonDate"]),
                                StatusID = Convert.ToInt32(reader["StatusID"]),
                                StatusName = reader["StatusName"].ToString(),
                                ExpirationDate = reader["ExpirationDate"] != DBNull.Value ? Convert.ToDateTime(reader["ExpirationDate"]) : (DateTime?)null,
                                TotalBooks = Convert.ToInt32(reader["TotalBooks"]),
                                UserTypeID = Convert.ToInt32(reader["UserTypeID"])
                            });
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
            {
                filtered = filtered.Where(r => r.StatusName == statusFilter);
            }

            DgRequests.ItemsSource = filtered.ToList();
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private async void ViewRequest_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;
                await ShowRequestDetails(requestId);
            }
        }

        private void CloseDetailsPanel_Click(object sender, RoutedEventArgs e)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            DgRequestItems.ItemsSource = null;
        }

        private async System.Threading.Tasks.Task ShowRequestDetails(int requestId)
        {
            try
            {
                string query = @"
                    SELECT 
                        r.RequestNumber,
                        CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                        rd.TypeID AS UserTypeID,
                        r.LessonName,
                        r.LessonDate,
                        r.RequestDate,
                        g.GroupCode,
                        rs.StatusName,
                        r.ExpirationDate,
                        ri.ID_RequestItem,
                        ri.BookID,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        ri.Quantity,
                        ris.StatusName AS ItemStatusName
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
                            var items = new ObservableCollection<RequestBookItem>();

                            while (await reader.ReadAsync())
                            {
                                if (!hasData)
                                {
                                    userTypeID = reader["UserTypeID"] != DBNull.Value ? Convert.ToInt32(reader["UserTypeID"]) : 0;
                                    string userTypeName = userTypeID == 1 ? "Студент" : (userTypeID == 3 ? "Сотрудник" : "Неизвестно");

                                    Dispatcher.Invoke(() =>
                                    {
                                        TbRequestNumber.Text = $"Заявка №: {reader["RequestNumber"]}";
                                        TbUserName.Text = $"Заявитель: {reader["UserName"]}";
                                        TbUserType.Text = $"Тип: {userTypeName}";
                                        TbLessonInfo.Text = $"Занятие: {reader["LessonName"]}";
                                        TbRequestDate.Text = $"Дата создания: {Convert.ToDateTime(reader["RequestDate"]):dd.MM.yyyy HH:mm}";
                                        TbGroupInfo.Text = $"Группа: {reader["GroupCode"]?.ToString() ?? "Не указана"}";
                                        TbStatus.Text = $"Статус: {reader["StatusName"]}";
                                        if (reader["ExpirationDate"] != DBNull.Value)
                                            TbExpirationDate.Text = $"Срок брони: {Convert.ToDateTime(reader["ExpirationDate"]):dd.MM.yyyy}";
                                        else
                                            TbExpirationDate.Text = "";
                                    });
                                    hasData = true;
                                }

                                if (reader["ID_RequestItem"] != DBNull.Value)
                                {
                                    items.Add(new RequestBookItem
                                    {
                                        ID_RequestItem = Convert.ToInt32(reader["ID_RequestItem"]),
                                        BookID = Convert.ToInt32(reader["BookID"]),
                                        BookTitle = reader["TitleBook"]?.ToString() ?? "Не указано",
                                        AuthorName = reader["AuthorName"]?.ToString() ?? "Не указан",
                                        Quantity = Convert.ToInt32(reader["Quantity"]),
                                        StatusName = reader["ItemStatusName"]?.ToString() ?? "Неизвестен"
                                    });
                                }
                            }

                            Dispatcher.Invoke(() =>
                            {
                                DgRequestItems.ItemsSource = items;
                                DetailsPanel.Visibility = Visibility.Visible;

                                if (items.Count == 0)
                                {
                                    var emptyList = new ObservableCollection<RequestBookItem>();
                                    emptyList.Add(new RequestBookItem
                                    {
                                        BookTitle = "Нет книг в заявке",
                                        AuthorName = "",
                                        Quantity = 0,
                                        StatusName = ""
                                    });
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

        private async void ConfirmRequest_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;

                // Находим заявку, чтобы получить UserID (ReaderID)
                var request = _allRequests.FirstOrDefault(r => r.ID_Request == requestId);
                if (request == null)
                {
                    CustomMessageBox.Show("Заявка не найдена.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    return;
                }

                var result = CustomMessageBox.Show("Подтвердить заявку? Книги будут зарезервированы.", "Подтверждение",
                    CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

                if (result == CustomMessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            await conn.OpenAsync();
                            using (var cmd = new SqlCommand("sp_ReserveBooksAtomic", conn))
                            {
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@RequestID", requestId);
                                cmd.Parameters.AddWithValue("@UserID", request.UserID);  // ← ДОБАВЛЯЕМ ЭТОТ ПАРАМЕТР!
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
                        CustomMessageBox.Show("Система временно перегружена. Пожалуйста, повторите попытку.", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                    catch (SqlException ex) when (ex.Message.Contains("недостаточно") || ex.Message.Contains("Недостаточно"))
                    {
                        CustomMessageBox.Show("Недостаточно экземпляров книг для резервирования.\n\n" +
                            "Возможно, другие заявки уже забрали эти книги.", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Ошибка подтверждения: {ex.Message}", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                }
            }
        }
        private void IssueBooks_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;

                var request = _allRequests.FirstOrDefault(r => r.ID_Request == requestId);
                if (request != null)
                {
                    // Проверка: если заявка отменена, запрещаем выдачу
                    if (request.StatusName == "Отменена")
                    {
                        CustomMessageBox.Show("Нельзя выдать книги по отмененной заявке.", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        return;
                    }

                    // Проверка: если заявка не в статусе "В обработке" или "Готова", разрешаем выдачу
                    if (request.StatusName != "В обработке" && request.StatusName != "Готова")
                    {
                        CustomMessageBox.Show($"Книги по заявке еще не готовы к выдаче. Сначала подтвердите заявку. Текущий статус: {request.StatusName}", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        return;
                    }

                    // Запрашиваем подтверждение выдачи
                    var confirmResult = CustomMessageBox.Show(
                        $"Выдать книги по заявке №{request.RequestNumber}?\n\nПользователь: {request.UserName}\nКниг: {request.TotalBooks} шт.",
                        "Подтверждение выдачи",
                        CustomMessageBoxButton.YesNo,
                        CustomMessageBoxImage.Question);

                    if (confirmResult != CustomMessageBoxResult.Yes) return;

                    var infoServicePage = new InformationServicePage();

                    if (request.GroupID.HasValue && request.GroupID > 0)
                    {
                        infoServicePage.SetDataFromRequest(
                            requestId,
                            request.UserID,
                            request.GroupID,
                            request.UserName,
                            request.GroupCode,
                            request.LessonName,
                            request.LessonDate
                        );
                    }
                    else
                    {
                        infoServicePage.SetDataFromRequestIndividual(
                            requestId,
                            request.UserID,
                            request.UserName,
                            request.LessonName,
                            request.LessonDate,
                            request.UserTypeID
                        );
                    }

                    NavigationService?.Navigate(infoServicePage);
                }
            }
        }

        private async void CancelRequest_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;

                var result = CustomMessageBox.Show(
                    "Отменить заявку?",
                    "Подтверждение отмены",
                    CustomMessageBoxButton.YesNo,
                    CustomMessageBoxImage.Question);

                if (result == CustomMessageBoxResult.Yes)
                {
                    try
                    {
                        int cancelledStatusId = 5;

                        using (var conn = new SqlConnection(_connectionString))
                        {
                            await conn.OpenAsync();
                            string query = "UPDATE BookRequests SET StatusID = @StatusId WHERE ID_Request = @RequestId";
                            using (var cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@StatusId", cancelledStatusId);
                                cmd.Parameters.AddWithValue("@RequestId", requestId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        CustomMessageBox.Show("Заявка отменена.", "Успешно",
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
            }
        }

        private void DgRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRequests.SelectedItem is RequestItem selected)
            {
                _ = ShowRequestDetails(selected.ID_Request);
            }
        }

        private void NavigateToRequest_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;
                _ = ShowRequestDetails(requestId);
            }
        }
    }
}