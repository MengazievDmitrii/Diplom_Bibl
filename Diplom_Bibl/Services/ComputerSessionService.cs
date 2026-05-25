// STAGE 2: Сервис управления сессиями ПК
using Diplom_Bibl.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Diplom_Bibl.Services
{
    public class ComputerSessionService
    {

        private readonly string _connectionString;
        public ComputerSessionService(string connectionString)
        {
            _connectionString = connectionString;
        }
        public ComputerSessionService() : this(DatabaseHelper.ConnectionString)
        {
        }
        public class SessionItem
        {
            public int ID_Session { get; set; }
            public string ComputerNumber { get; set; } = "";
            public int UserID { get; set; }
            public string FullName { get; set; } = "";
            public string GroupCode { get; set; } = "";
            public string Purpose { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int? Duration { get; set; }
            public string Status { get; set; } = "";
            public bool IsActive => Status == "Active";
            public string StartTimeDisplay => StartTime.ToString("dd.MM.yyyy HH:mm");
            public string EndTimeDisplay => EndTime?.ToString("dd.MM.yyyy HH:mm") ?? "Активна";
            public string DurationDisplay => Duration.HasValue ? $"{Duration.Value} мин" : "—";
            public string CurrentDuration
            {
                get
                {
                    if (!IsActive) return DurationDisplay;
                    TimeSpan duration = DateTime.Now - StartTime;
                    return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                }
            }
            public string StatusDisplay => Status == "Active" ? "Активна" : (Status == "Completed" ? "Завершена" : "Прервана");

        }
       

        // Создание новой сессии
        public async Task<int> CreateSessionAsync(string computerNumber, int userId, string fullName,
            string groupCode, string purpose)
        {
            const string sql = @"
                INSERT INTO ComputerSessions (ComputerNumber, UserID, FullName, GroupCode, Purpose, 
                    StartTime, Status, CreatedDate)
                VALUES (@ComputerNumber, @UserID, @FullName, @GroupCode, @Purpose, 
                    @StartTime, 'Active', GETDATE());
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ComputerNumber", computerNumber);
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@FullName", fullName);
                    cmd.Parameters.AddWithValue("@GroupCode", string.IsNullOrEmpty(groupCode) ? (object)DBNull.Value : groupCode);
                    cmd.Parameters.AddWithValue("@Purpose", purpose);
                    cmd.Parameters.AddWithValue("@StartTime", DateTime.Now);

                    int sessionId = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                    await AddLogAsync(sessionId, "Создана", "Сессия начата").ConfigureAwait(false);
                    return sessionId;
                }
            }
        }

        // Завершение сессии
        public async Task<bool> EndSessionAsync(int sessionId)
        {
            const string sql = @"
                UPDATE ComputerSessions 
                SET EndTime = @EndTime, Status = 'Completed'
                WHERE ID_Session = @SessionId AND Status = 'Active'";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EndTime", DateTime.Now);
                    cmd.Parameters.AddWithValue("@SessionId", sessionId);
                    int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                    if (rows > 0)
                    {
                        await AddLogAsync(sessionId, "Завершена", "Сессия завершена").ConfigureAwait(false);
                        return true;
                    }
                    return false;
                }
            }
        }

        // Аварийное завершение (при закрытии приложения)
        public async Task TerminateActiveSessionAsync(int userId, string computerNumber)
        {
            const string sql = @"
                UPDATE ComputerSessions 
                SET EndTime = @EndTime, Status = 'Terminated'
                WHERE UserID = @UserId AND ComputerNumber = @ComputerNumber AND Status = 'Active'";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EndTime", DateTime.Now);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@ComputerNumber", computerNumber);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        // Добавление записи в лог
        private async Task AddLogAsync(int sessionId, string action, string details)
        {
            const string sql = @"
                INSERT INTO ComputerLogs (SessionID, Action, ActionTime, Details)
                VALUES (@SessionId, @Action, @ActionTime, @Details)";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@SessionId", sessionId);
                    cmd.Parameters.AddWithValue("@Action", action);
                    cmd.Parameters.AddWithValue("@ActionTime", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Details", details ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        // Проверка активной сессии для пользователя
        public async Task<bool> HasActiveSessionAsync(int userId)
        {
            const string sql = "SELECT COUNT(*) FROM ComputerSessions WHERE UserID = @UserId AND Status = 'Active'";
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    int count = (int)await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    return count > 0;
                }
            }
        }

        // Получение списка активных сессий для панели библиотекаря
        public async Task<ObservableCollection<SessionItem>> GetActiveSessionsAsync()
        {
            var sessions = new ObservableCollection<SessionItem>();
            const string sql = @"
        SELECT ID_Session, ComputerNumber, UserID, FullName, ISNULL(GroupCode, '') AS GroupCode,
               Purpose, StartTime, EndTime, Duration, Status
        FROM ComputerSessions
        WHERE Status = 'Active'
        ORDER BY StartTime DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        sessions.Add(new SessionItem
                        {
                            ID_Session = reader.GetInt32(0),
                            ComputerNumber = reader.GetString(1),
                            UserID = reader.GetInt32(2),
                            FullName = reader.GetString(3),
                            GroupCode = reader.GetString(4),
                            Purpose = reader.GetString(5),
                            StartTime = reader.GetDateTime(6),
                            EndTime = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                            Duration = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                            Status = reader.GetString(9)
                        });
                    }
                }
            }
            return sessions;
        }

        // Получение истории сессий (завершенные)
        public async Task<ObservableCollection<SessionItem>> GetCompletedSessionsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var sessions = new ObservableCollection<SessionItem>();
            string sql = @"
        SELECT ID_Session, ComputerNumber, UserID, FullName, ISNULL(GroupCode, '') AS GroupCode,
               Purpose, StartTime, EndTime, Duration, Status
        FROM ComputerSessions
        WHERE Status IN ('Completed', 'Terminated')";

            if (fromDate.HasValue)
                sql += " AND StartTime >= @FromDate";
            if (toDate.HasValue)
                sql += " AND StartTime <= @ToDate";

            sql += " ORDER BY StartTime DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (fromDate.HasValue)
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value);
                    if (toDate.HasValue)
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value);

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            sessions.Add(new SessionItem
                            {
                                ID_Session = reader.GetInt32(0),
                                ComputerNumber = reader.GetString(1),
                                UserID = reader.GetInt32(2),
                                FullName = reader.GetString(3),
                                GroupCode = reader.GetString(4),
                                Purpose = reader.GetString(5),
                                StartTime = reader.GetDateTime(6),
                                EndTime = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                Duration = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                Status = reader.GetString(9)
                            });
                        }
                    }
                }
            }
            return sessions;
        }

        // Принудительное завершение сессии библиотекарем
        public async Task<bool> ForceEndSessionAsync(int sessionId, string reason)
        {
            const string sql = @"
        UPDATE ComputerSessions 
        SET EndTime = @EndTime, Status = 'Terminated'
        WHERE ID_Session = @SessionId AND Status = 'Active'";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EndTime", DateTime.Now);
                    cmd.Parameters.AddWithValue("@SessionId", sessionId);
                    int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                    if (rows > 0)
                    {
                        await AddLogAsync(sessionId, "Принудительно завершена", reason).ConfigureAwait(false);
                        return true;
                    }
                    return false;
                }
            }
        }

        public async Task<SessionItem> GetActiveSessionByUserAsync(int userId)
        {
            const string sql = @"
        SELECT TOP 1 ID_Session, ComputerNumber, UserID, FullName, ISNULL(GroupCode, '') AS GroupCode,
               Purpose, StartTime, EndTime, Duration, Status
        FROM ComputerSessions
        WHERE UserID = @UserId AND Status = 'Active'";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))  // ← ИСПРАВЛЕНО: ConfigureAwwait → ConfigureAwait
                        {
                            return new SessionItem
                            {
                                ID_Session = reader.GetInt32(0),
                                ComputerNumber = reader.GetString(1),
                                UserID = reader.GetInt32(2),
                                FullName = reader.GetString(3),
                                GroupCode = reader.GetString(4),
                                Purpose = reader.GetString(5),
                                StartTime = reader.GetDateTime(6),
                                EndTime = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                Duration = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                Status = reader.GetString(9)
                            };
                        }
                    }
                }
            }
            return null;
        }
    }
}