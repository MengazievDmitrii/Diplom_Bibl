using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace Diplom_Bibl.Services
{
    /// <summary>
    /// Сервис для импорта читателей из Excel/CSV файлов
    /// При импорте автоматически создаются учётные записи пользователей
    /// Логин и пароль = номер читательского билета
    /// </summary>
    public class ExcelImportService
    {
        private readonly string _connectionString;

        /// <summary>
        /// Результат импорта - содержит статистику, ошибки, предупреждения и учётные данные
        /// </summary>
        public class ImportResult
        {
            public int TotalRows { get; set; }          // Всего строк в файле
            public int SuccessRows { get; set; }        // Успешно импортировано
            public int ErrorRows { get; set; }          // Строк с ошибками
            public List<string> Errors { get; set; } = new List<string>();     // Список ошибок
            public List<string> Warnings { get; set; } = new List<string>();   // Список предупреждений
            public List<string> UserCredentials { get; set; } = new List<string>(); // Логины/пароли
            public bool HasErrors => Errors.Count > 0;  // Есть ли ошибки
        }

        /// <summary>
        /// Модель данных одной строки из Excel/CSV
        /// </summary>
        public class ExcelReaderRow
        {
            public string CardNumber { get; set; }          // Номер читательского билета
            public string LastName { get; set; }            // Фамилия
            public string FirstName { get; set; }           // Имя
            public string MiddleName { get; set; }          // Отчество
            public string TypeName { get; set; }            // Тип читателя (Студент/Сотрудник)
            public string GroupCode { get; set; }           // Код группы (для студентов)
            public string PostName { get; set; }            // Должность (для сотрудников)
            public string Speciality { get; set; }          // Специальность
            public string PhoneNumber { get; set; }         // Телефон
            public string HomeAddress { get; set; }         // Домашний адрес
            public string MembershipStartDate { get; set; } // Дата начала членства
            public string FormFillDate { get; set; }        // Дата оформления билета
            public int RowNumber { get; set; }              // Номер строки в файле (для сообщений)
        }

        public ExcelImportService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Импорт из Excel файла (.xlsx, .xls)
        /// </summary>
        public ImportResult ImportFromFile(string filePath)
        {
            var result = new ImportResult();

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    IExcelDataReader reader;

                    try
                    {
                        reader = ExcelReaderFactory.CreateReader(stream);
                    }
                    catch
                    {
                        // Если не получилось прочитать как Excel - пробуем как CSV
                        return ImportFromCsv(filePath);
                    }

                    using (reader)
                    {
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true  // Первая строка - заголовки
                            }
                        });

                        System.Data.DataTable dt = dataSet.Tables[0];

                        if (dt.Rows.Count == 0)
                        {
                            result.Errors.Add("Файл не содержит данных");
                            return result;
                        }

                        ProcessDataTable(dt, result);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Ошибка открытия файла: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Проверка наличия обязательных колонок в файле
        /// </summary>
        private void ValidateColumns(DataTable dt, ImportResult result)
        {
            var requiredColumns = new[] {
                "Номер читательского билета",
                "Фамилия",
                "Имя",
                "Тип читателя"
            };

            var existingColumns = new List<string>();

            foreach (DataColumn col in dt.Columns)
                existingColumns.Add(col.ColumnName.Trim());

            foreach (var required in requiredColumns)
            {
                bool found = false;
                foreach (var col in existingColumns)
                {
                    if (col.Equals(required, StringComparison.OrdinalIgnoreCase) ||
                        col.Replace(" ", "").Equals(required.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    result.Errors.Add($"Не найдена обязательная колонка: {required}");
                }
            }
        }

        /// <summary>
        /// Парсинг строки данных в объект ExcelReaderRow
        /// </summary>
        private ExcelReaderRow ParseRow(DataRow row, int rowNumber)
        {
            var result = new ExcelReaderRow();

            foreach (DataColumn col in row.Table.Columns)
            {
                string colName = col.ColumnName.Trim().ToLower().Replace(" ", "");
                string value = row[col]?.ToString()?.Trim() ?? "";

                // Маппинг колонок (поддержка разных вариантов написания)
                switch (colName)
                {
                    case "номерчитательскогобилета":
                    case "cardnumber":
                        result.CardNumber = value;
                        break;
                    case "фамилия":
                    case "lastname":
                        result.LastName = value;
                        break;
                    case "имя":
                    case "firstname":
                        result.FirstName = value;
                        break;
                    case "отчество":
                    case "middlename":
                        result.MiddleName = value;
                        break;
                    case "типчитателя":
                    case "typename":
                        result.TypeName = value;
                        break;
                    case "группа":
                    case "groupcode":
                        result.GroupCode = value;
                        break;
                    case "должность":
                    case "postname":
                        result.PostName = value;
                        break;
                    case "специальность":
                    case "speciality":
                        result.Speciality = value;
                        break;
                    case "телефон":
                    case "phonenumber":
                        result.PhoneNumber = value;
                        break;
                    case "домашнийадрес":
                    case "адрес":
                    case "homeaddress":
                        result.HomeAddress = value;
                        break;
                    case "датаначалачленства":
                    case "датаначала":
                    case "membershipstartdate":
                        result.MembershipStartDate = value;
                        break;
                    case "датаоформления":
                    case "formfilldate":
                        result.FormFillDate = value;
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Валидация строки перед импортом
        /// </summary>
        private bool ValidateRow(ExcelReaderRow row, ImportResult result)
        {
            bool isValid = true;

            // Проверка обязательных полей
            if (string.IsNullOrWhiteSpace(row.CardNumber))
            {
                result.Errors.Add($"Строка {row.RowNumber}: Не указан номер читательского билета");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(row.LastName))
            {
                result.Errors.Add($"Строка {row.RowNumber}: Не указана фамилия");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(row.FirstName))
            {
                result.Errors.Add($"Строка {row.RowNumber}: Не указано имя");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(row.TypeName))
            {
                result.Errors.Add($"Строка {row.RowNumber}: Не указан тип читателя");
                isValid = false;
            }

            // Проверка уникальности номера билета
            if (!string.IsNullOrWhiteSpace(row.CardNumber))
            {
                if (CheckCardNumberExists(row.CardNumber))
                {
                    result.Warnings.Add($"Строка {row.RowNumber}: Читатель с номером {row.CardNumber} уже существует в базе. Будет пропущен.");
                    isValid = false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// Проверка существования номера читательского билета в БД
        /// </summary>
        private bool CheckCardNumberExists(string cardNumber)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ReaderForm WHERE CardNumber = @CardNumber", conn))
                {
                    cmd.Parameters.AddWithValue("@CardNumber", cardNumber);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Проверка существования логина в БД (номер билета)
        /// </summary>
        private bool CheckLoginExists(string login)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Login = @Login", conn))
                {
                    cmd.Parameters.AddWithValue("@Login", login);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Генерация пароля (номер билета, минимум 4 символа)
        /// Если номер короче 4 символов, добавляем "lib" в начало
        /// </summary>
        private string GeneratePassword(string cardNumber)
        {
            string password = cardNumber.Trim();
            if (password.Length < 4)
            {
                password = "lib" + password;
            }
            return password;
        }

        /// <summary>
        /// Создание учётной записи пользователя и назначение роли READER
        /// Логин и пароль = номер читательского билета
        /// </summary>
        private void CreateUserForReader(SqlConnection conn, SqlTransaction transaction, int readerId, string surname, string name, string patronymic, string cardNumber, ExcelReaderRow row, ImportResult result)
        {
            try
            {
                // Логин = номер читательского билета
                string login = cardNumber.Trim();

                // Пароль = номер читательского билета (с возможным дополнением до 4 символов)
                string plainPassword = GeneratePassword(cardNumber);

                // Хэшируем пароль с помощью BCrypt
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

                // Проверяем уникальность логина (номер билета должен быть уникальным)
                // Это уже проверено в ValidateRow, но на всякий случай проверяем ещё раз
                if (CheckLoginExists(login))
                {
                    result.Warnings.Add($"Строка {row.RowNumber}: Учётная запись с номером {login} уже существует. Пропущено.");
                    return;
                }

                // Вставляем пользователя
                string userQuery = @"
                    INSERT INTO Users (Surname, Name, Patronymic, Login, PasswordHash, ReaderID)
                    VALUES (@Surname, @Name, @Patronymic, @Login, @PasswordHash, @ReaderID);
                    SELECT SCOPE_IDENTITY();";

                int userId = 0;
                using (var cmd = new SqlCommand(userQuery, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Surname", surname);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Patronymic", string.IsNullOrWhiteSpace(patronymic) ? (object)DBNull.Value : patronymic);
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@ReaderID", readerId);
                    object resultObj = cmd.ExecuteScalar();
                    if (resultObj != null && resultObj != DBNull.Value)
                    {
                        userId = Convert.ToInt32(resultObj);
                    }
                }

                if (userId > 0)
                {
                    // Получаем ID роли READER
                    string roleQuery = "SELECT ID_Role FROM UserRoles WHERE RoleCode = 'READER'";
                    int readerRoleId = 0;
                    using (var cmd = new SqlCommand(roleQuery, conn, transaction))
                    {
                        object resultObj = cmd.ExecuteScalar();
                        if (resultObj != null && resultObj != DBNull.Value)
                        {
                            readerRoleId = Convert.ToInt32(resultObj);
                        }
                    }

                    // Назначаем роль пользователю
                    if (readerRoleId > 0)
                    {
                        string assignRoleQuery = @"
                            INSERT INTO UserRolesAssignments (ID_User, ID_Role)
                            VALUES (@UserId, @RoleId)";
                        using (var cmd = new SqlCommand(assignRoleQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@RoleId", readerRoleId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Сохраняем учётные данные для отображения пользователю
                    result.UserCredentials.Add($"{surname} {name} (билет №{cardNumber}): Логин = {login}, Пароль = {plainPassword}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Строка {row.RowNumber}: Читатель добавлен, но ошибка создания учётной записи: {ex.Message}");
            }
        }

        /// <summary>
        /// Импорт одного читателя в БД (в транзакции)
        /// </summary>
        private void ImportReader(ExcelReaderRow row, ImportResult result)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool isStudent = row.TypeName.ToLower().Contains("студент");
                        bool isEmployee = row.TypeName.ToLower().Contains("сотрудник");

                        // Получаем ID типа читателя
                        int typeId = GetReaderTypeId(conn, row.TypeName, transaction);

                        // Для студента - получаем ID группы
                        int? groupId = null;
                        if (isStudent && !string.IsNullOrWhiteSpace(row.GroupCode))
                        {
                            groupId = GetGroupId(conn, row.GroupCode, transaction);
                            if (groupId == null)
                            {
                                result.Warnings.Add($"Строка {row.RowNumber}: Группа '{row.GroupCode}' не найдена. Поле будет пустым.");
                            }
                        }

                        // Для сотрудника - получаем ID должности
                        int? postId = null;
                        if (isEmployee && !string.IsNullOrWhiteSpace(row.PostName))
                        {
                            postId = GetPostId(conn, row.PostName, transaction);
                            if (postId == null)
                            {
                                result.Warnings.Add($"Строка {row.RowNumber}: Должность '{row.PostName}' не найдена. Поле будет пустым.");
                            }
                        }

                        // Парсим даты (если не указаны - текущая дата)
                        DateTime membershipStartDate = ParseDate(row.MembershipStartDate, DateTime.Today);
                        DateTime formFillDate = ParseDate(row.FormFillDate, DateTime.Today);

                        // Вставка читателя
                        string query = @"
                            INSERT INTO ReaderForm (
                                CardNumber, LastName, FirstName, MiddleName,
                                TypeID, GroupID, Post, Speciality,
                                PhoneNumber, HomeAddress, MembershipStartDate, FormFillDate
                            ) VALUES (
                                @CardNumber, @LastName, @FirstName, @MiddleName,
                                @TypeID, @GroupID, @Post, @Speciality,
                                @PhoneNumber, @HomeAddress, @MembershipStartDate, @FormFillDate
                            );
                            SELECT SCOPE_IDENTITY();";

                        int newReaderId = 0;

                        using (var cmd = new SqlCommand(query, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CardNumber", row.CardNumber);
                            cmd.Parameters.AddWithValue("@LastName", row.LastName);
                            cmd.Parameters.AddWithValue("@FirstName", row.FirstName);
                            cmd.Parameters.AddWithValue("@MiddleName", string.IsNullOrEmpty(row.MiddleName) ? DBNull.Value : (object)row.MiddleName);
                            cmd.Parameters.AddWithValue("@TypeID", typeId);
                            cmd.Parameters.AddWithValue("@GroupID", groupId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Post", postId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Speciality", string.IsNullOrEmpty(row.Speciality) ? DBNull.Value : (object)row.Speciality);
                            cmd.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrEmpty(row.PhoneNumber) ? DBNull.Value : (object)row.PhoneNumber);
                            cmd.Parameters.AddWithValue("@HomeAddress", string.IsNullOrEmpty(row.HomeAddress) ? DBNull.Value : (object)row.HomeAddress);
                            cmd.Parameters.AddWithValue("@MembershipStartDate", membershipStartDate);
                            cmd.Parameters.AddWithValue("@FormFillDate", formFillDate);

                            object resultObj = cmd.ExecuteScalar();
                            if (resultObj != null && resultObj != DBNull.Value)
                            {
                                newReaderId = Convert.ToInt32(resultObj);
                            }
                        }

                        if (newReaderId > 0)
                        {
                            // Создаём учётную запись пользователя
                            CreateUserForReader(conn, transaction, newReaderId, row.LastName, row.FirstName, row.MiddleName, row.CardNumber, row, result);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Убираем MessageBox из транзакции - просто добавляем ошибку в результат
                        result.Errors.Add($"Строка {row.RowNumber}: Ошибка импорта - {ex.Message}");
                        transaction.Rollback();
                        // Не выбрасываем исключение, чтобы продолжить обработку остальных строк
                    }
                }
            }
        }

        /// <summary>
        /// Получение ID типа читателя по названию
        /// Сначала ищем точное совпадение, потом по вхождению, по умолчанию - студент (1)
        /// </summary>
        private int GetReaderTypeId(SqlConnection conn, string typeName, SqlTransaction transaction)
        {
            // Прямое совпадение
            string query = "SELECT TypeID FROM ReaderType WHERE TypeName = @TypeName";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            // Поиск по вхождению (например, "Студент" найдёт "Студент очной формы")
            query = "SELECT TOP 1 TypeID FROM ReaderType WHERE @TypeName LIKE '%' + TypeName + '%' OR TypeName LIKE '%' + @TypeName + '%'";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            return 1; // По умолчанию - студент
        }

        /// <summary>
        /// Получение ID группы по коду
        /// </summary>
        private int? GetGroupId(SqlConnection conn, string groupCode, SqlTransaction transaction)
        {
            string query = "SELECT GroupID FROM Groups WHERE GroupCode = @GroupCode";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@GroupCode", groupCode);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : (int?)null;
            }
        }

        /// <summary>
        /// Получение ID должности по названию
        /// </summary>
        private int? GetPostId(SqlConnection conn, string postName, SqlTransaction transaction)
        {
            string query = "SELECT ID_Post FROM Post WHERE Post_Name = @PostName";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@PostName", postName);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : (int?)null;
            }
        }

        /// <summary>
        /// Парсинг даты из строки с поддержкой разных форматов
        /// Поддерживаются форматы: ДД.ММ.ГГГГ, ДД/ММ/ГГГГ, ГГГГ-ММ-ДД и другие
        /// </summary>
        private DateTime ParseDate(string dateString, DateTime defaultValue)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return defaultValue;

            string[] formats = {
                "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
                "dd.MM.yy", "dd/MM/yy", "dd-MM-yy",
                "d.M.yyyy", "d/M/yyyy"
            };

            foreach (string format in formats)
            {
                if (DateTime.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out DateTime result))
                    return result;
            }

            if (DateTime.TryParse(dateString, out DateTime standardResult))
                return standardResult;

            return defaultValue;
        }

        /// <summary>
        /// Импорт из CSV файла
        /// </summary>
        public ImportResult ImportFromCsv(string filePath)
        {
            var result = new ImportResult();

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using (var reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    string headerLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(headerLine))
                    {
                        result.Errors.Add("Файл пуст");
                        return result;
                    }

                    var headers = headerLine.Split(',');
                    var rows = new List<string[]>();

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            rows.Add(ParseCsvLine(line));
                        }
                    }

                    result.TotalRows = rows.Count;

                    var dt = new System.Data.DataTable();
                    foreach (var header in headers)
                    {
                        dt.Columns.Add(header.Trim());
                    }

                    foreach (var row in rows)
                    {
                        if (row.Length >= headers.Length)
                        {
                            dt.Rows.Add(row);
                        }
                        else
                        {
                            var paddedRow = new string[headers.Length];
                            Array.Copy(row, paddedRow, row.Length);
                            for (int i = row.Length; i < headers.Length; i++)
                            {
                                paddedRow[i] = "";
                            }
                            dt.Rows.Add(paddedRow);
                        }
                    }

                    ProcessDataTable(dt, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Ошибка чтения CSV: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Парсинг CSV строки с учётом кавычек (поддержка полей, содержащих запятые)
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Экранирование кавычек внутри поля
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Обработка DataTable - построчный импорт
        /// </summary>
        private void ProcessDataTable(System.Data.DataTable dt, ImportResult result)
        {
            // Проверяем наличие обязательных колонок
            ValidateColumns(dt, result);

            // Если есть критические ошибки и нет данных - выходим
            if (result.HasErrors && result.Errors.Count > 0 && dt.Rows.Count == 0) return;

            // Обрабатываем каждую строку
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                try
                {
                    var row = ParseRow(dt.Rows[i], i + 2); // +2 потому что 1 - заголовок, а индексация с 0
                    row.RowNumber = i + 2;

                    if (ValidateRow(row, result))
                    {
                        ImportReader(row, result);
                        result.SuccessRows++;
                    }
                    else
                    {
                        result.ErrorRows++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorRows++;
                    result.Errors.Add($"Строка {i + 2}: Ошибка обработки - {ex.Message}");
                }
            }
        }
    }
}