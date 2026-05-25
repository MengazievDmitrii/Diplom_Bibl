using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Diplom_Bibl.Services
{
    public class NotificationService : IDisposable
    {
        private readonly string _connectionString;
        private readonly Dispatcher _dispatcher;
        private Timer _pollingTimer;
        private ObservableCollection<NotificationItem> _notifications;
        private int _lastNotificationId;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        private int _currentUserId;
        private int _currentUserTypeId;
        private List<string> _userRoles;

        public class NotificationItem
        {
            public int ID_Notification { get; set; }
            public int RequestID { get; set; }
            public string Message { get; set; } = "";
            public string NotificationType { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public bool IsRead { get; set; }
            public string RequestNumber { get; set; } = "";
            public DateTime? ExpirationDate { get; set; }
            public string UserName { get; set; } = "";
            public string GroupName { get; set; } = "";
            public string LessonName { get; set; } = "";
            public string LessonDate { get; set; } = "";
            public string LessonTime { get; set; } = "";
            public int BooksCount { get; set; }
            public string BookTitles { get; set; } = "";
        }

        public event EventHandler<NotificationItem> NewNotificationReceived;
        public event EventHandler<int> UnreadCountChanged;
        public event EventHandler<ObservableCollection<NotificationItem>> NotificationsUpdated;

        public NotificationService(string connectionString, Dispatcher dispatcher, int userId, int userTypeId, List<string> userRoles)
        {
            _connectionString = connectionString;
            _dispatcher = dispatcher;
            _currentUserId = userId;
            _currentUserTypeId = userTypeId;
            _userRoles = userRoles;
            _notifications = new ObservableCollection<NotificationItem>();
        }

        public void StartPolling(int intervalSeconds = 5)
        {
            LoadInitialNotifications();
            _pollingTimer = new Timer(async _ => await PollNotificationsAsync(), null, 0, intervalSeconds * 1000);
        }

        public void StopPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        public void RefreshNotifications()
        {
            LoadInitialNotifications();
        }

        private bool IsLibrarianOrAdmin()
        {
            return _userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN");
        }

        private async void LoadInitialNotifications()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query;

                    if (IsLibrarianOrAdmin())
                    {
                        // Загружаем только НЕПРОЧИТАННЫЕ уведомления для библиотекаря
                        query = @"
                            SELECT 
                                n.ID_Notification, 
                                n.RequestID, 
                                n.Message, 
                                nt.TypeName AS NotificationType,
                                n.CreatedDate, 
                                n.IsRead,
                                br.RequestNumber,
                                br.ExpirationDate,
                                ISNULL(rf.LastName + ' ' + rf.FirstName + ' ' + rf.MiddleName, 'Не указано') AS UserName,
                                ISNULL(g.GroupCode, 'Не указана') AS GroupName,
                                ISNULL(br.LessonName, 'Не указано') AS LessonName,
                                FORMAT(br.LessonDate, 'dd.MM.yyyy') AS LessonDate,
                                ISNULL(LEFT(br.LessonTime, 5), 'Не указано') AS LessonTime,
                                (SELECT COUNT(*) FROM RequestItems WHERE RequestID = br.ID_Request) AS BooksCount,
                                STUFF((
                                    SELECT ', ' + b.TitleBook 
                                    FROM RequestItems ri
                                    INNER JOIN Books b ON ri.BookID = b.IDBook
                                    WHERE ri.RequestID = br.ID_Request
                                    FOR XML PATH('')
                                ), 1, 2, '') AS BookTitles
                            FROM RequestNotifications n
                            INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                            INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                            LEFT JOIN ReaderForm rf ON br.UserID = rf.ReaderID
                            LEFT JOIN Groups g ON rf.GroupID = g.GroupID
                            WHERE n.IsRead = 0
                            ORDER BY n.CreatedDate DESC";
                    }
                    else
                    {
                        // Для читателей - только свои НЕПРОЧИТАННЫЕ уведомления
                        query = @"
                            SELECT 
                                n.ID_Notification, 
                                n.RequestID, 
                                n.Message, 
                                nt.TypeName AS NotificationType,
                                n.CreatedDate, 
                                n.IsRead,
                                br.RequestNumber,
                                br.ExpirationDate,
                                '' AS UserName,
                                '' AS GroupName,
                                '' AS LessonName,
                                '' AS LessonDate,
                                '' AS LessonTime,
                                0 AS BooksCount,
                                '' AS BookTitles
                            FROM RequestNotifications n
                            INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                            INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                            WHERE br.UserID = @UserId AND n.IsRead = 0
                            ORDER BY n.CreatedDate DESC";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!IsLibrarianOrAdmin())
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            _notifications.Clear();
                            int maxId = 0;

                            while (await reader.ReadAsync())
                            {
                                var notification = new NotificationItem
                                {
                                    ID_Notification = reader.GetInt32(0),
                                    RequestID = reader.GetInt32(1),
                                    Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NotificationType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CreatedDate = reader.GetDateTime(4),
                                    IsRead = reader.GetBoolean(5),
                                    RequestNumber = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    ExpirationDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                    UserName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    GroupName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    LessonName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    LessonDate = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    LessonTime = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    BooksCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                                    BookTitles = reader.IsDBNull(14) ? "" : reader.GetString(14)
                                };

                                _notifications.Add(notification);

                                if (notification.ID_Notification > maxId)
                                    maxId = notification.ID_Notification;
                            }

                            _lastNotificationId = maxId;
                            System.Diagnostics.Debug.WriteLine($"Загружено НЕПРОЧИТАННЫХ уведомлений: {_notifications.Count}");

                            _dispatcher.Invoke(() => NotificationsUpdated?.Invoke(this, _notifications));
                            _dispatcher.Invoke(() => UnreadCountChanged?.Invoke(this, GetUnreadCount()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки уведомлений: {ex.Message}");
            }
        }

        public ObservableCollection<NotificationItem> GetNotifications()
        {
            return _notifications;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "UPDATE RequestNotifications SET IsRead = 1 WHERE ID_Notification = @Id";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", notificationId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                var notification = _notifications.FirstOrDefault(n => n.ID_Notification == notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    _notifications.Remove(notification); // Удаляем из списка непрочитанных
                    _dispatcher.Invoke(() =>
                    {
                        NotificationsUpdated?.Invoke(this, _notifications);
                        UnreadCountChanged?.Invoke(this, GetUnreadCount());
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отметки прочитанного: {ex.Message}");
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query;
                    if (IsLibrarianOrAdmin())
                    {
                        query = "UPDATE RequestNotifications SET IsRead = 1 WHERE IsRead = 0";
                    }
                    else
                    {
                        query = @"
                            UPDATE n 
                            SET n.IsRead = 1
                            FROM RequestNotifications n
                            INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                            WHERE br.UserID = @UserId AND n.IsRead = 0";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!IsLibrarianOrAdmin())
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        }
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                _notifications.Clear();
                _dispatcher.Invoke(() =>
                {
                    NotificationsUpdated?.Invoke(this, _notifications);
                    UnreadCountChanged?.Invoke(this, GetUnreadCount());
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        public int GetUnreadCount()
        {
            return _notifications.Count;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopPolling();
            GC.SuppressFinalize(this);
        }

        private async Task PollNotificationsAsync()
        {
            if (_isDisposed) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query;

                    if (IsLibrarianOrAdmin())
                    {
                        // Проверяем только НОВЫЕ уведомления (с ID больше последнего)
                        query = @"
                            SELECT 
                                n.ID_Notification, 
                                n.RequestID, 
                                n.Message, 
                                nt.TypeName AS NotificationType,
                                n.CreatedDate, 
                                n.IsRead,
                                br.RequestNumber,
                                br.ExpirationDate,
                                ISNULL(rf.LastName + ' ' + rf.FirstName + ' ' + rf.MiddleName, 'Не указано') AS UserName,
                                ISNULL(g.GroupCode, 'Не указана') AS GroupName,
                                ISNULL(br.LessonName, 'Не указано') AS LessonName,
                                FORMAT(br.LessonDate, 'dd.MM.yyyy') AS LessonDate,
                                ISNULL(LEFT(br.LessonTime, 5), 'Не указано') AS LessonTime,
                                (SELECT COUNT(*) FROM RequestItems WHERE RequestID = br.ID_Request) AS BooksCount,
                                STUFF((
                                    SELECT ', ' + b.TitleBook 
                                    FROM RequestItems ri
                                    INNER JOIN Books b ON ri.BookID = b.IDBook
                                    WHERE ri.RequestID = br.ID_Request
                                    FOR XML PATH('')
                                ), 1, 2, '') AS BookTitles
                            FROM RequestNotifications n
                            INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                            INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                            LEFT JOIN ReaderForm rf ON br.UserID = rf.ReaderID
                            LEFT JOIN Groups g ON rf.GroupID = g.GroupID
                            WHERE n.ID_Notification > @LastId AND n.IsRead = 0
                            ORDER BY n.CreatedDate DESC";
                    }
                    else
                    {
                        query = @"
                            SELECT 
                                n.ID_Notification, 
                                n.RequestID, 
                                n.Message, 
                                nt.TypeName AS NotificationType,
                                n.CreatedDate, 
                                n.IsRead,
                                br.RequestNumber,
                                br.ExpirationDate,
                                '' AS UserName,
                                '' AS GroupName,
                                '' AS LessonName,
                                '' AS LessonDate,
                                '' AS LessonTime,
                                0 AS BooksCount,
                                '' AS BookTitles
                            FROM RequestNotifications n
                            INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                            INNER JOIN BookRequests br ON n.RequestID = br.ID_Request
                            WHERE br.UserID = @UserId AND n.ID_Notification > @LastId AND n.IsRead = 0
                            ORDER BY n.CreatedDate DESC";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!IsLibrarianOrAdmin())
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        }
                        cmd.Parameters.AddWithValue("@LastId", _lastNotificationId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var notification = new NotificationItem
                                {
                                    ID_Notification = reader.GetInt32(0),
                                    RequestID = reader.GetInt32(1),
                                    Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NotificationType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CreatedDate = reader.GetDateTime(4),
                                    IsRead = reader.GetBoolean(5),
                                    RequestNumber = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    ExpirationDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                    UserName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    GroupName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    LessonName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    LessonDate = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    LessonTime = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    BooksCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                                    BookTitles = reader.IsDBNull(14) ? "" : reader.GetString(14)
                                };

                                lock (_lockObject)
                                {
                                    if (notification.ID_Notification > _lastNotificationId)
                                        _lastNotificationId = notification.ID_Notification;
                                }

                                _dispatcher.Invoke(() =>
                                {
                                    _notifications.Insert(0, notification);
                                    NotificationsUpdated?.Invoke(this, _notifications);
                                    UnreadCountChanged?.Invoke(this, GetUnreadCount());
                                    NewNotificationReceived?.Invoke(this, notification);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка опроса: {ex.Message}");
            }
        }
    }
}