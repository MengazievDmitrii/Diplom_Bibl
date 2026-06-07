using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Diplom_Bibl.Services
{
    /// <summary>
    /// Сервис для работы с уведомлениями в библиотечной системе
    /// Отвечает за получение, хранение и отображение уведомлений для пользователей
    /// </summary>
    public class NotificationService : IDisposable
    {
        #region Поля

        private readonly string _connectionString;              // Строка подключения к БД
        private readonly Dispatcher _dispatcher;               // Dispatcher для UI потока
        private Timer _pollingTimer;                           // Таймер для периодической проверки
        private ObservableCollection<NotificationItem> _notifications;  // Коллекция уведомлений
        private int _lastNotificationId;                       // ID последнего полученного уведомления
        private bool _isDisposed;                              // Флаг освобождения ресурсов
        private readonly object _lockObject = new object();    // Объект синхронизации
        private int _currentUserId;                            // ID текущего пользователя
        private int _currentUserTypeId;                        // Тип пользователя
        private List<string> _userRoles;                       // Роли пользователя

        #endregion

        #region Вложенный класс NotificationItem

        /// <summary>
        /// Модель уведомления для отображения в интерфейсе
        /// </summary>
        public class NotificationItem
        {
            public int ID_Notification { get; set; }           // Уникальный ID уведомления
            public int RequestID { get; set; }                 // ID связанной заявки
            public string Message { get; set; } = "";          // Текст сообщения
            public string NotificationType { get; set; } = ""; // Тип уведомления (Новая заявка, Готова к выдаче и т.д.)
            public DateTime CreatedDate { get; set; }          // Дата создания
            public bool IsRead { get; set; }                   // Прочитано/не прочитано
            public string RequestNumber { get; set; } = "";    // Номер заявки
            public DateTime? ExpirationDate { get; set; }      // Дата истечения срока
            public string UserName { get; set; } = "";         // Имя пользователя
            public string GroupName { get; set; } = "";        // Название группы
            public string LessonName { get; set; } = "";       // Название занятия
            public string LessonDate { get; set; } = "";       // Дата занятия
            public string LessonTime { get; set; } = "";       // Время занятия
            public int BooksCount { get; set; }                // Количество книг в заявке
            public string BookTitles { get; set; } = "";       // Названия книг через запятую
        }

        #endregion

        #region События

        /// <summary>
        /// Событие возникает при получении нового уведомления
        /// </summary>
        public event EventHandler<NotificationItem> NewNotificationReceived;

        /// <summary>
        /// Событие возникает при изменении количества непрочитанных уведомлений
        /// </summary>
        public event EventHandler<int> UnreadCountChanged;

        /// <summary>
        /// Событие возникает при обновлении коллекции уведомлений
        /// </summary>
        public event EventHandler<ObservableCollection<NotificationItem>> NotificationsUpdated;

        #endregion

        #region Конструктор

        /// <summary>
        /// Конструктор сервиса уведомлений
        /// </summary>
        /// <param name="connectionString">Строка подключения к БД</param>
        /// <param name="dispatcher">Dispatcher для работы с UI потоком</param>
        /// <param name="userId">ID текущего пользователя</param>
        /// <param name="userTypeId">Тип пользователя (1-студент, 3-преподаватель)</param>
        /// <param name="userRoles">Список ролей пользователя</param>
        public NotificationService(string connectionString, Dispatcher dispatcher, int userId, int userTypeId, List<string> userRoles)
        {
            _connectionString = connectionString;
            _dispatcher = dispatcher;
            _currentUserId = userId;
            _currentUserTypeId = userTypeId;
            _userRoles = userRoles;
            _notifications = new ObservableCollection<NotificationItem>();
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Запуск периодической проверки новых уведомлений
        /// </summary>
        /// <param name="intervalSeconds">Интервал проверки в секундах (по умолчанию 5)</param>
        public void StartPolling(int intervalSeconds = 5)
        {
            LoadInitialNotifications();
            _pollingTimer = new Timer(async _ => await PollNotificationsAsync(), null, 0, intervalSeconds * 1000);
        }

        /// <summary>
        /// Остановка периодической проверки уведомлений
        /// </summary>
        public void StopPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        /// <summary>
        /// Принудительное обновление списка уведомлений
        /// </summary>
        public void RefreshNotifications()
        {
            LoadInitialNotifications();
        }

        /// <summary>
        /// Принудительная проверка и загрузка новых уведомлений
        /// Используется после выполнения операций, которые могут создать новые уведомления
        /// </summary>
        public async Task CheckNewNotifications()
        {
            await PollNotificationsAsync();
        }

        /// <summary>
        /// Получение текущей коллекции уведомлений
        /// </summary>
        /// <returns>Коллекция уведомлений</returns>
        public ObservableCollection<NotificationItem> GetNotifications()
        {
            return _notifications;
        }

        /// <summary>
        /// Отметка уведомления как прочитанного
        /// </summary>
        /// <param name="notificationId">ID уведомления</param>
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
                    _notifications.Remove(notification);
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

        /// <summary>
        /// Отметка всех уведомлений как прочитанных
        /// </summary>
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
                        // Для библиотекаря и админа - все уведомления
                        query = "UPDATE RequestNotifications SET IsRead = 1 WHERE IsRead = 0";
                    }
                    else
                    {
                        // Для читателя - только свои уведомления
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

        /// <summary>
        /// Получение количества непрочитанных уведомлений
        /// </summary>
        /// <returns>Количество непрочитанных уведомлений</returns>
        public int GetUnreadCount()
        {
            return _notifications.Count;
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopPolling();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Приватные методы

        /// <summary>
        /// Проверка, является ли пользователь библиотекарем или администратором
        /// </summary>
        /// <returns>True если пользователь библиотекарь или админ</returns>
        private bool IsLibrarianOrAdmin()
        {
            return _userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN");
        }

        private async void LoadInitialNotifications()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📥 ЗАГРУЗКА УВЕДОМЛЕНИЙ (Initial)");

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query;
                    bool isLibrarian = IsLibrarianOrAdmin();

                    if (isLibrarian)
                    {
                        query = @"
                    SELECT TOP 20 
                        n.ID_Notification, 
                        n.RequestID, 
                        n.Message, 
                        nt.TypeName AS NotificationType,
                        n.CreatedDate, 
                        n.IsRead,
                        ISNULL(br.RequestNumber, '') AS RequestNumber,
                        br.ExpirationDate,
                        ISNULL(rf.LastName + ' ' + rf.FirstName + ' ' + rf.MiddleName, 'Системное уведомление') AS UserName,
                        ISNULL(g.GroupCode, '') AS GroupName,
                        ISNULL(br.LessonName, '') AS LessonName,
                        CONVERT(NVARCHAR(10), br.LessonDate, 104) AS LessonDate,
                        ISNULL(LEFT(CONVERT(NVARCHAR(8), br.LessonTime, 8), 5), '') AS LessonTime,
                        (SELECT ISNULL(SUM(Quantity), 0) FROM RequestItems WHERE RequestID = br.ID_Request) AS BooksCount,
                        STUFF((
                            SELECT ', ' + b.TitleBook 
                            FROM RequestItems ri
                            INNER JOIN Books b ON ri.BookID = b.IDBook
                            WHERE ri.RequestID = br.ID_Request
                            FOR XML PATH('')
                        ), 1, 2, '') AS BookTitles
                    FROM RequestNotifications n
                    INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                    LEFT JOIN BookRequests br ON n.RequestID = br.ID_Request
                    LEFT JOIN ReaderForm rf ON br.UserID = rf.ReaderID
                    LEFT JOIN Groups g ON rf.GroupID = g.GroupID
                    WHERE n.IsRead = 0
                    ORDER BY n.CreatedDate DESC";
                    }
                    else
                    {
                        query = @"
                    SELECT TOP 20 
                        n.ID_Notification, 
                        n.RequestID, 
                        n.Message, 
                        nt.TypeName AS NotificationType,
                        n.CreatedDate, 
                        n.IsRead,
                        ISNULL(br.RequestNumber, '') AS RequestNumber,
                        br.ExpirationDate
                    FROM RequestNotifications n
                    INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
                    LEFT JOIN BookRequests br ON n.RequestID = br.ID_Request
                    WHERE (br.UserID = @UserId OR n.RequestID IS NULL)
                      AND n.IsRead = 0 
                      AND nt.TypeName <> 'Новая заявка'
                    ORDER BY n.CreatedDate DESC";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!isLibrarian)
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            _notifications.Clear();
                            int maxId = 0;
                            int count = 0;

                            while (await reader.ReadAsync())
                            {
                                count++;

                                int? requestId = null;
                                if (!reader.IsDBNull(1))
                                    requestId = reader.GetInt32(1);

                                var notification = new NotificationItem
                                {
                                    ID_Notification = reader.GetInt32(0),
                                    RequestID = requestId ?? 0,
                                    Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NotificationType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CreatedDate = reader.GetDateTime(4),
                                    IsRead = reader.GetBoolean(5),
                                    RequestNumber = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    ExpirationDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7)
                                };

                                if (isLibrarian)
                                {
                                    notification.UserName = reader.IsDBNull(8) ? "" : reader.GetString(8);
                                    notification.GroupName = reader.IsDBNull(9) ? "" : reader.GetString(9);
                                    notification.LessonName = reader.IsDBNull(10) ? "" : reader.GetString(10);
                                    notification.LessonDate = reader.IsDBNull(11) ? "" : reader.GetString(11);
                                    notification.LessonTime = reader.IsDBNull(12) ? "" : reader.GetString(12);
                                    notification.BooksCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13);
                                    notification.BookTitles = reader.IsDBNull(14) ? "" : reader.GetString(14);
                                }

                                _notifications.Add(notification);

                                if (notification.ID_Notification > maxId)
                                    maxId = notification.ID_Notification;
                            }

                            _lastNotificationId = maxId;
                            System.Diagnostics.Debug.WriteLine($"✅ Загружено уведомлений: {count}, последний ID: {_lastNotificationId}");
                            _dispatcher.Invoke(() =>
                            {
                                NotificationsUpdated?.Invoke(this, _notifications);
                                UnreadCountChanged?.Invoke(this, GetUnreadCount());
                            });
                            
                           
                            ShowAllUnreadNotifications();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки уведомлений: {ex.Message}\n{ex.StackTrace}");
            }
        }
        /// Периодическая проверка новых уведомлений (выполняется в фоновом потоке)
        /// </summary>
        private async Task PollNotificationsAsync()
        {
            if (_isDisposed) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query;
                    bool isLibrarian = IsLibrarianOrAdmin();

                    if (isLibrarian)
                    {
                        // =========================================
                        // ИСПРАВЛЕНИЕ ДЛЯ БИБЛИОТЕКАРЯ:
                        // Используем LEFT JOIN вместо INNER JOIN,
                        // чтобы уведомления с RequestID = NULL тоже выбирались
                        // =========================================
                        query = @"
            SELECT 
                n.ID_Notification, 
                n.RequestID, 
                n.Message, 
                nt.TypeName AS NotificationType,
                n.CreatedDate, 
                n.IsRead,
                ISNULL(br.RequestNumber, '') AS RequestNumber,
                br.ExpirationDate,
                ISNULL(rf.LastName + ' ' + rf.FirstName + ' ' + rf.MiddleName, 'Системное уведомление') AS UserName,
                ISNULL(g.GroupCode, '') AS GroupName,
                ISNULL(br.LessonName, '') AS LessonName,
                FORMAT(br.LessonDate, 'dd.MM.yyyy') AS LessonDate,
                ISNULL(LEFT(CONVERT(NVARCHAR(8), br.LessonTime, 8), 5), '') AS LessonTime,
                ISNULL((SELECT ISNULL(SUM(Quantity), 0) FROM RequestItems WHERE RequestID = br.ID_Request), 0) AS BooksCount,
                ISNULL(STUFF((
                    SELECT ', ' + b.TitleBook 
                    FROM RequestItems ri
                    INNER JOIN Books b ON ri.BookID = b.IDBook
                    WHERE ri.RequestID = br.ID_Request
                    FOR XML PATH('')
                ), 1, 2, ''), '') AS BookTitles
            FROM RequestNotifications n
            INNER JOIN NotificationTypes nt ON n.NotificationTypeID = nt.ID_Type
            LEFT JOIN BookRequests br ON n.RequestID = br.ID_Request  -- ← LEFT JOIN вместо INNER JOIN!
            LEFT JOIN ReaderForm rf ON br.UserID = rf.ReaderID
            LEFT JOIN Groups g ON rf.GroupID = g.GroupID
            WHERE n.ID_Notification > @LastId AND n.IsRead = 0
            ORDER BY n.CreatedDate DESC";
                    }
                    else
                    {
                        // =========================================
                        // ДЛЯ ЧИТАТЕЛЯ - тоже LEFT JOIN
                        // =========================================
                        query = @"
            SELECT 
                n.ID_Notification, 
                n.RequestID, 
                n.Message, 
                nt.TypeName AS NotificationType,
                n.CreatedDate, 
                n.IsRead,
                ISNULL(br.RequestNumber, '') AS RequestNumber,
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
            LEFT JOIN BookRequests br ON n.RequestID = br.ID_Request  -- ← LEFT JOIN вместо INNER JOIN!
            WHERE (br.UserID = @UserId OR n.RequestID IS NULL)  -- ← Добавляем условие для уведомлений без заявки
              AND n.ID_Notification > @LastId 
              AND n.IsRead = 0 
              AND nt.TypeName <> 'Новая заявка'
            ORDER BY n.CreatedDate DESC";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!isLibrarian)
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        }
                        cmd.Parameters.AddWithValue("@LastId", _lastNotificationId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int newCount = 0;
                            while (await reader.ReadAsync())
                            {
                                newCount++;

                                // Безопасное чтение RequestID (может быть NULL)
                                int? requestId = null;
                                if (!reader.IsDBNull(1))
                                    requestId = reader.GetInt32(1);

                                var notification = new NotificationItem
                                {
                                    ID_Notification = reader.GetInt32(0),
                                    RequestID = requestId ?? 0,
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
                                    NewNotificationReceived?.Invoke(this, notification);  // ← ЭТО ДЛЯ НОВЫХ

                                    System.Diagnostics.Debug.WriteLine($"🔔 НОВОЕ УВЕДОМЛЕНИЕ: {notification.NotificationType} - {notification.Message}");
                                });
                            }

                            if (newCount > 0)
                                System.Diagnostics.Debug.WriteLine($"✅ Получено новых уведомлений: {newCount}");
                            else
                                System.Diagnostics.Debug.WriteLine($"🔍 Новых уведомлений нет, lastId={_lastNotificationId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка опроса: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Показывает все непрочитанные уведомления при инициализации
        /// </summary>
        /// <summary>
        /// Показывает все непрочитанные уведомления при инициализации
        /// </summary>
        private void ShowAllUnreadNotifications()
        {
            System.Diagnostics.Debug.WriteLine($"📢 ПОКАЗ ВСЕХ НЕПРОЧИТАННЫХ УВЕДОМЛЕНИЙ: {_notifications.Count} шт.");

            foreach (var notification in _notifications)
            {
                if (!notification.IsRead)
                {
                    System.Diagnostics.Debug.WriteLine($"🔔 ТРИГГЕР ДЛЯ ID={notification.ID_Notification}, Тип={notification.NotificationType}");
                    _dispatcher.Invoke(() =>
                    {
                        // ВАЖНО: Вызываем событие для каждого уведомления
                        NewNotificationReceived?.Invoke(this, notification);
                    });
                }
            }
        }

        #endregion
    }
}