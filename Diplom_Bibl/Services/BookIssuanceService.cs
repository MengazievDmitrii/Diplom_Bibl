using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplom_Bibl.Services
{
    /// <summary>
    /// Класс для работы с выдачами книг
    /// </summary>
    public class BookIssuanceService
    {
        private readonly string _connectionString;

        // Модели данных
        public class IssuanceItem
        {
            public string IssuanceType { get; set; }
            public int Id { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ReturnDate { get; set; }
            public string Status { get; set; }
            public int RecipientId { get; set; }
            public string RecipientName { get; set; }
            public string RecipientType { get; set; }
            public int BookId { get; set; }
            public string BookTitle { get; set; }
            public string InventaryNumber { get; set; }
            public int Quantity { get; set; }
            public string Notes { get; set; }

            public string DisplayInfo => $"{IssuanceDate:dd.MM.yyyy} - {RecipientName} - {BookTitle} ({Quantity} шт.)";
            public bool IsOverdue => !ReturnDate.HasValue && DateTime.Now > PlannedReturnDate;
        }

        public class BookInfo
        {
            public int IDBook { get; set; } // Можно оставить для ссылки, но не для отображения
            public string TitleBook { get; set; }
            public string AuthorName { get; set; }
            public int AvailableCopies { get; set; }
            public int TotalCopies { get; set; }

            // Для обратной совместимости (если нужно)
            public string InventaryNumber { get; set; } // Можно не использовать

            public string DisplayInfo => $"{TitleBook} - {AuthorName} (в наличии: {AvailableCopies})";
        }

        public class ReaderInfo
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string TypeName { get; set; }

            public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
            public string DisplayInfo => $"{FullName} (№{CardNumber})";
        }

        public class GroupInfo
        {
            public int GroupID { get; set; }
            public string GroupCode { get; set; }
            public string SpecialityName { get; set; }
            public string DisplayInfo => $"{GroupCode}";
        }

        public class TeacherInfo
        {
            public int TeacherID { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
            public string DisplayInfo => FullName;
        }

        public BookIssuanceService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Получение списка доступных книг (сгруппированных по названию и автору)
        /// </summary>
        public ObservableCollection<BookInfo> GetAvailableBooks(string searchTerm = "")
        {
            var books = new ObservableCollection<BookInfo>();

            string sql = @"
        SELECT 
            b.TitleBook,
            ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
            SUM(b.AvailableCopies) AS TotalAvailableCopies,
            SUM(b.TotalCopies) AS TotalCopies
        FROM Books b
        LEFT JOIN Authors a ON b.Author = a.ID_Autor
        WHERE b.AvailableCopies > 0";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " AND (b.TitleBook LIKE @Search OR a.SurnameAuthor LIKE @Search)";
            }

            sql += @" GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
              ORDER BY b.TitleBook";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                            cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                books.Add(new BookInfo
                                {
                                    TitleBook = reader["TitleBook"]?.ToString() ?? "",
                                    AuthorName = reader["AuthorName"]?.ToString() ?? "",
                                    AvailableCopies = Convert.ToInt32(reader["TotalAvailableCopies"]),
                                    TotalCopies = Convert.ToInt32(reader["TotalCopies"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг: {ex.Message}");
            }

            return books;
        }

        /// <summary>
        /// Получение списка читателей (студентов)
        /// </summary>
        public ObservableCollection<ReaderInfo> GetReaders(string searchTerm = "")
        {
            var readers = new ObservableCollection<ReaderInfo>();

            string sql = @"
                SELECT 
                    r.ReaderID,
                    r.CardNumber,
                    r.LastName,
                    r.FirstName,
                    r.MiddleName,
                    rt.TypeName
                FROM ReaderForm r
                INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
                WHERE rt.TypeName LIKE '%студент%'";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " AND (r.LastName LIKE @Search OR r.FirstName LIKE @Search OR r.CardNumber LIKE @Search)";
            }

            sql += " ORDER BY r.LastName, r.FirstName";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            readers.Add(new ReaderInfo
                            {
                                ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeName = reader["TypeName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return readers;
        }

        /// <summary>
        /// Получение списка групп
        /// </summary>
        public ObservableCollection<GroupInfo> GetGroups(string searchTerm = "")
        {
            var groups = new ObservableCollection<GroupInfo>();

            string sql = @"
                SELECT 
                    g.GroupID,
                    g.GroupCode,
                    s.NameSpeciality AS SpecialityName
                FROM Groups g
                LEFT JOIN Speciality s ON g.SpecialityID = s.SpecialityID";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " WHERE g.GroupCode LIKE @Search";
            }

            sql += " ORDER BY g.GroupCode";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            groups.Add(new GroupInfo
                            {
                                GroupID = Convert.ToInt32(reader["GroupID"]),
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                SpecialityName = reader["SpecialityName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return groups;
        }

        /// <summary>
        /// Получение списка преподавателей (и сотрудников)
        /// </summary>
        public ObservableCollection<TeacherInfo> GetTeachers(string searchTerm = "")
        {
            var teachers = new ObservableCollection<TeacherInfo>();

            string sql = @"
        SELECT 
            r.ReaderID,
            r.LastName,
            r.FirstName,
            r.MiddleName,
            rt.TypeName
        FROM ReaderForm r
        INNER JOIN ReaderType rt ON r.TypeID = rt.TypeID
        WHERE rt.TypeName LIKE '%сотрудник%' OR rt.TypeName LIKE '%преподаватель%'";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " AND (r.LastName LIKE @Search OR r.FirstName LIKE @Search)";
            }

            sql += " ORDER BY r.LastName, r.FirstName";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                            cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                teachers.Add(new TeacherInfo
                                {
                                    TeacherID = Convert.ToInt32(reader["ReaderID"]),
                                    LastName = reader["LastName"]?.ToString() ?? "",
                                    FirstName = reader["FirstName"]?.ToString() ?? "",
                                    MiddleName = reader["MiddleName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Найдено преподавателей/сотрудников: {teachers.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки преподавателей: {ex.Message}");
            }

            return teachers;
        }

        /// <summary>
        /// Создание индивидуальной выдачи
        /// </summary>
        public int CreateIndividualIssuance(int bookId, int readerId, DateTime plannedReturnDate, string notes = "")
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CreateIndividualIssuance", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@BookId", bookId);
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);
                    cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                    cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : (object)notes);

                    var outputParam = cmd.Parameters.Add("@IssuanceId", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(outputParam.Value);
                }
            }
        }

        /// <summary>
        /// Создание групповой выдачи
        /// </summary>
        public int CreateGroupIssuance(
            int groupId,
            int teacherId,
            DateTime plannedReturnDate,
            Dictionary<int, int> booksWithQuantity,
            string subjectName = "",
            string notes = "")
        {
            // Формируем строку для процедуры "BookId1:Quantity1,BookId2:Quantity2"
            var booksList = new StringBuilder();
            foreach (var book in booksWithQuantity)
            {
                if (booksList.Length > 0)
                    booksList.Append(",");
                booksList.Append($"{book.Key}:{book.Value}");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CreateGroupIssuance", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                    cmd.Parameters.AddWithValue("@TeacherId", teacherId);
                    cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                    cmd.Parameters.AddWithValue("@SubjectName", string.IsNullOrWhiteSpace(subjectName) ? DBNull.Value : (object)subjectName);
                    cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : (object)notes);
                    cmd.Parameters.AddWithValue("@BooksList", booksList.ToString());

                    var outputParam = cmd.Parameters.Add("@GroupIssuanceId", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(outputParam.Value);
                }
            }
        }

        /// <summary>
        /// Возврат книги
        /// </summary>
        public void ReturnBook(string issuanceType, int issuanceId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReturnBook", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@IssuanceType", issuanceType);
                    cmd.Parameters.AddWithValue("@IssuanceId", issuanceId);
                    cmd.Parameters.AddWithValue("@ReturnDate", DateTime.Now);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Получение списка выдач за период
        /// </summary>
        public ObservableCollection<IssuanceItem> GetIssuancesByDate(DateTime date)
        {
            var issuances = new ObservableCollection<IssuanceItem>();

            string sql = @"
                SELECT 
                    IssuanceType,
                    Id,
                    IssuanceDate,
                    PlannedReturnDate,
                    ReturnDate,
                    Status,
                    RecipientId,
                    RecipientName,
                    RecipientType,
                    BookId,
                    BookTitle,
                    InventaryNumber,
                    Quantity,
                    Notes
                FROM vw_AllIssuances
                WHERE CAST(IssuanceDate AS DATE) = @Date
                ORDER BY IssuanceDate DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Date", date.Date);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            issuances.Add(new IssuanceItem
                            {
                                IssuanceType = reader["IssuanceType"]?.ToString() ?? "",
                                Id = Convert.ToInt32(reader["Id"]),
                                IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                ReturnDate = reader["ReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReturnDate"]) : (DateTime?)null,
                                Status = reader["Status"]?.ToString() ?? "",
                                RecipientId = Convert.ToInt32(reader["RecipientId"]),
                                RecipientName = reader["RecipientName"]?.ToString() ?? "",
                                RecipientType = reader["RecipientType"]?.ToString() ?? "",
                                BookId = Convert.ToInt32(reader["BookId"]),
                                BookTitle = reader["BookTitle"]?.ToString() ?? "",
                                InventaryNumber = reader["InventaryNumber"]?.ToString() ?? "",
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                Notes = reader["Notes"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return issuances;
        }

        /// <summary>
        /// Получение активных выдач читателя
        /// </summary>
        public ObservableCollection<IssuanceItem> GetReaderActiveIssuances(int readerId)
        {
            var issuances = new ObservableCollection<IssuanceItem>();

            string sql = @"
                SELECT 
                    'individual' AS IssuanceType,
                    i.IssuanceId AS Id,
                    i.IssuanceDate,
                    i.PlannedReturnDate,
                    i.ReturnDate,
                    i.Status,
                    i.ReaderId AS RecipientId,
                    CONCAT(r.LastName, ' ', r.FirstName, ' ', r.MiddleName) AS RecipientName,
                    'Читатель' AS RecipientType,
                    b.IDBook AS BookId,
                    b.TitleBook AS BookTitle,
                    b.InventaryNumber,
                    1 AS Quantity,
                    i.Notes
                FROM BookIssuance i
                INNER JOIN ReaderForm r ON i.ReaderId = r.ReaderID
                INNER JOIN Books b ON i.BookId = b.IDBook
                WHERE i.ReaderId = @ReaderId AND i.ReturnDate IS NULL
                ORDER BY i.PlannedReturnDate";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            issuances.Add(new IssuanceItem
                            {
                                IssuanceType = reader["IssuanceType"]?.ToString() ?? "",
                                Id = Convert.ToInt32(reader["Id"]),
                                IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                ReturnDate = reader["ReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReturnDate"]) : (DateTime?)null,
                                Status = reader["Status"]?.ToString() ?? "",
                                RecipientId = Convert.ToInt32(reader["RecipientId"]),
                                RecipientName = reader["RecipientName"]?.ToString() ?? "",
                                RecipientType = reader["RecipientType"]?.ToString() ?? "",
                                BookId = Convert.ToInt32(reader["BookId"]),
                                BookTitle = reader["BookTitle"]?.ToString() ?? "",
                                InventaryNumber = reader["InventaryNumber"]?.ToString() ?? "",
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                Notes = reader["Notes"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return issuances;
        }
        /// <summary>
        /// Создание групповой выдачи с несколькими книгами
        /// </summary>
        public int CreateGroupIssuanceWithBooks(
            int groupId,
            int teacherId,
            DateTime plannedReturnDate,
            List<int> bookIds,
            string subjectName = "",
            string notes = "")
        {
            if (bookIds == null || bookIds.Count == 0)
                throw new ArgumentException("Список книг не может быть пустым");

            // Формируем строку с ID книг через запятую
            string bookIdsString = string.Join(",", bookIds);

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CreateGroupIssuanceWithBooks", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                    cmd.Parameters.AddWithValue("@TeacherId", teacherId);
                    cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                    cmd.Parameters.AddWithValue("@SubjectName", string.IsNullOrWhiteSpace(subjectName) ? DBNull.Value : (object)subjectName);
                    cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : (object)notes);
                    cmd.Parameters.AddWithValue("@BookIds", bookIdsString);

                    var outputParam = cmd.Parameters.Add("@GroupIssuanceId", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    return Convert.ToInt32(outputParam.Value);
                }
            }
        }
    }
}