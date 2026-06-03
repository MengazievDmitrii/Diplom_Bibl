using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Windows;
using ExcelDataReader;

namespace Diplom_Bibl.Services
{
    public class ExcelImportService
    {
        private readonly string _connectionString;

        // Результат импорта
        public class ImportResult
        {
            public int TotalRows { get; set; }
            public int SuccessRows { get; set; }
            public int ErrorRows { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> UserCredentials { get; set; } = new List<string>(); // Уже есть
            public bool HasErrors => Errors.Count > 0;
        }
        // Модель данных из Excel
        public class ExcelReaderRow
        {
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string TypeName { get; set; }
            public string GroupCode { get; set; }
            public string PostName { get; set; }
            public string Speciality { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public string MembershipStartDate { get; set; }
            public string FormFillDate { get; set; }
            public int RowNumber { get; set; }
        }

        public ExcelImportService(string connectionString)
        {
            _connectionString = connectionString;
        }

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
                        return ImportFromCsv(filePath);
                    }

                    using (reader)
                    {
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true
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

        private ExcelReaderRow ParseRow(DataRow row, int rowNumber)
        {
            var result = new ExcelReaderRow();

            foreach (DataColumn col in row.Table.Columns)
            {
                string colName = col.ColumnName.Trim().ToLower().Replace(" ", "");
                string value = row[col]?.ToString()?.Trim() ?? "";

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

        private bool ValidateRow(ExcelReaderRow row, ImportResult result)
        {
            bool isValid = true;

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

        private string GenerateLogin(string lastName, string firstName)
        {
            // Логин = Фамилия + Имя (например: ИвановИван)
            string login = lastName + firstName;

            // Убираем пробелы и лишние символы
            login = login.Replace(" ", "").Replace("-", "");

            // Проверяем уникальность
            string uniqueLogin = login;
            int counter = 1;

            while (CheckLoginExists(uniqueLogin))
            {
                uniqueLogin = login + counter.ToString();
                counter++;
            }

            return uniqueLogin;
        }

        private string GeneratePassword(string cardNumber)
        {
            string password = cardNumber.Trim();

            // Если пароль короче 4 символов, добавляем "lib" в начало
            if (password.Length < 4)
            {
                password = "lib" + password;
            }

            return password;
        }

        private void CreateUserForReader(SqlConnection conn, SqlTransaction transaction, int readerId, string surname, string name, string patronymic, string cardNumber, ExcelReaderRow row, ImportResult result)
        {
            try
            {
                string login = GenerateLogin(surname, name);
                string plainPassword = GeneratePassword(cardNumber);
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

                string query = @"
            INSERT INTO Users (Surname, Name, Patronymic, Login, PasswordHash, ReaderID)
            VALUES (@Surname, @Name, @Patronymic, @Login, @PasswordHash, @ReaderID)";

                using (var cmd = new SqlCommand(query, conn, transaction)) // Добавлен transaction
                {
                    cmd.Parameters.AddWithValue("@Surname", surname);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Patronymic", string.IsNullOrWhiteSpace(patronymic) ? (object)DBNull.Value : patronymic);
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@ReaderID", readerId);
                    cmd.ExecuteNonQuery();
                }

                // Сохраняем данные для входа в результат
                string loginByName = GenerateLogin(surname, name);
                result.UserCredentials.Add($"Читатель {surname} {name} (билет №{cardNumber}): Логин = {loginByName}, Пароль = {plainPassword}");
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Строка {row.RowNumber}: Читатель добавлен, но ошибка создания учётной записи: {ex.Message}");
            }
        }

        private void ImportReader(ExcelReaderRow row, ImportResult result)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Начинаем транзакцию
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool isStudent = row.TypeName.ToLower().Contains("студент");
                        bool isEmployee = row.TypeName.ToLower().Contains("сотрудник");

                        int typeId = GetReaderTypeId(conn, row.TypeName, transaction);

                        int? groupId = null;
                        if (isStudent && !string.IsNullOrWhiteSpace(row.GroupCode))
                        {
                            groupId = GetGroupId(conn, row.GroupCode, transaction);
                            if (groupId == null)
                            {
                                result.Warnings.Add($"Строка {row.RowNumber}: Группа '{row.GroupCode}' не найдена. Поле будет пустым.");
                            }
                        }

                        int? postId = null;
                        if (isEmployee && !string.IsNullOrWhiteSpace(row.PostName))
                        {
                            postId = GetPostId(conn, row.PostName, transaction);
                            if (postId == null)
                            {
                                result.Warnings.Add($"Строка {row.RowNumber}: Должность '{row.PostName}' не найдена. Поле будет пустым.");
                            }
                        }

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

                            newReaderId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        if (newReaderId > 0)
                        {
                            // Создаём учётную запись пользователя - передаём transaction
                            CreateUserForReader(conn, transaction, newReaderId, row.LastName, row.FirstName, row.MiddleName, row.CardNumber, row, result);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private int GetReaderTypeId(SqlConnection conn, string typeName, SqlTransaction transaction)
        {
            string query = "SELECT TypeID FROM ReaderType WHERE TypeName = @TypeName";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return Convert.ToInt32(result);
            }

            query = "SELECT TOP 1 TypeID FROM ReaderType WHERE @TypeName LIKE '%' + TypeName + '%' OR TypeName LIKE '%' + @TypeName + '%'";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return Convert.ToInt32(result);
            }

            return 1;
        }

        private int? GetGroupId(SqlConnection conn, string groupCode, SqlTransaction transaction)
        {
            string query = "SELECT GroupID FROM Groups WHERE GroupCode = @GroupCode";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@GroupCode", groupCode);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
        }

        private int? GetPostId(SqlConnection conn, string postName, SqlTransaction transaction)
        {
            string query = "SELECT ID_Post FROM Post WHERE Post_Name = @PostName";
            using (var cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@PostName", postName);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
        }

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

        private void ProcessDataTable(System.Data.DataTable dt, ImportResult result)
        {
            ValidateColumns(dt, result);
            if (result.HasErrors && result.Errors.Count > 0 && dt.Rows.Count == 0) return;

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                try
                {
                    var row = ParseRow(dt.Rows[i], i + 2);
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