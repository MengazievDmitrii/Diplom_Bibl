using System;
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

        public class NotificationItem
        {
            public int ID_Notification { get; set; }
            public int RequestID { get; set; }
            public string Message { get; set; } = "";
            public string NotificationType { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public bool IsRead { get; set; }
        }

        // Событие при получении нового уведомления
        public event EventHandler<NotificationItem> NewNotificationReceived;

        // Событие при обновлении списка уведомлений
        public event EventHandler<ObservableCollection<NotificationItem>> NotificationsUpdated;

        public NotificationService(string connectionString, Dispatcher dispatcher)
        {
            _connectionString = connectionString;
            _dispatcher = dispatcher;
            _notifications = new ObservableCollection<NotificationItem>();
        }

        public void StartPolling(int intervalSeconds = 5)
        {
            _pollingTimer = new Timer(async _ => await PollNotificationsAsync(), null, 0, intervalSeconds * 1000);
        }

        public void StopPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        private async Task PollNotificationsAsync()
        {
            if (_isDisposed) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT TOP 10 
                            ID_Notification, 
                            RequestID, 
                            Message, 
                            NotificationTypeID,
                            (SELECT TypeName FROM NotificationTypes WHERE ID_Type = NotificationTypeID) AS NotificationType,
                            CreatedDate, 
                            IsRead
                        FROM RequestNotifications
                        WHERE ID_Notification > @LastId
                        ORDER BY CreatedDate DESC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@LastId", _lastNotificationId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            var newNotifications = new ObservableCollection<NotificationItem>();
                            while (await reader.ReadAsync())
                            {
                                var notification = new NotificationItem
                                {
                                    ID_Notification = Convert.ToInt32(reader["ID_Notification"]),
                                    RequestID = Convert.ToInt32(reader["RequestID"]),
                                    Message = reader["Message"].ToString(),
                                    NotificationType = reader["NotificationType"].ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                    IsRead = Convert.ToBoolean(reader["IsRead"])
                                };
                                newNotifications.Add(notification);

                                lock (_lockObject)
                                {
                                    if (notification.ID_Notification > _lastNotificationId)
                                        _lastNotificationId = notification.ID_Notification;
                                }

                                // Вызываем событие для каждого нового уведомления
                                _dispatcher.Invoke(() =>
                                {
                                    NewNotificationReceived?.Invoke(this, notification);
                                });
                            }

                            if (newNotifications.Count > 0)
                            {
                                // Обновляем общий список
                                _dispatcher.Invoke(() =>
                                {
                                    foreach (var n in newNotifications)
                                    {
                                        _notifications.Insert(0, n);
                                    }
                                    NotificationsUpdated?.Invoke(this, _notifications);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка опроса уведомлений: {ex.Message}");
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
                    _dispatcher.Invoke(() => NotificationsUpdated?.Invoke(this, _notifications));
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
                    string query = "UPDATE RequestNotifications SET IsRead = 1 WHERE IsRead = 0";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                foreach (var n in _notifications)
                {
                    n.IsRead = true;
                }
                _dispatcher.Invoke(() => NotificationsUpdated?.Invoke(this, _notifications));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        public int GetUnreadCount()
        {
            return _notifications.Count(n => !n.IsRead);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopPolling();
            GC.SuppressFinalize(this);
        }
    }
}