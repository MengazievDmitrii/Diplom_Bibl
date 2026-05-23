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
            public bool HasErrors => Errors.Count > 0;
        }

        // Модель данных из Excel
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
            public string HomeAddress { get; set; }         // Адрес
            public string MembershipStartDate { get; set; } // Дата начала членства
            public string FormFillDate { get; set; }        // Дата оформления

            // Для валидации
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
                    // Автоматическое определение формата Excel
                    IExcelDataReader reader;

                    try
                    {
                        reader = ExcelReaderFactory.CreateReader(stream);
                    }
                    catch
                    {
                        // Если не удалось прочитать как Excel, пробуем как CSV
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

                        // Берем первый лист
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

            // Проверка на дубликат номера билета
            if (!string.IsNullOrWhiteSpace(row.CardNumber))
            {
                if (CheckCardNumberExists(row.CardNumber))
                {
                    result.Warnings.Add($"Строка {row.RowNumber}: Читатель с номером {row.CardNumber} уже существует в базе. Будет пропущен.");
                    isValid = false; // Пропускаем дубликаты
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

        private void ImportReader(ExcelReaderRow row, ImportResult result)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Определяем тип читателя
                    bool isStudent = row.TypeName.ToLower().Contains("студент");
                    bool isEmployee = row.TypeName.ToLower().Contains("сотрудник");

                    // Получаем ID типа читателя
                    int typeId = GetReaderTypeId(conn, row.TypeName);

                    // Получаем ID группы (для студентов)
                    int? groupId = null;
                    if (isStudent && !string.IsNullOrWhiteSpace(row.GroupCode))
                    {
                        groupId = GetGroupId(conn, row.GroupCode);
                        if (groupId == null)
                        {
                            result.Warnings.Add($"Строка {row.RowNumber}: Группа '{row.GroupCode}' не найдена. Поле будет пустым.");
                        }
                    }

                    // Получаем ID должности (для сотрудников)
                    int? postId = null;
                    if (isEmployee && !string.IsNullOrWhiteSpace(row.PostName))
                    {
                        postId = GetPostId(conn, row.PostName);
                        if (postId == null)
                        {
                            result.Warnings.Add($"Строка {row.RowNumber}: Должность '{row.PostName}' не найдена. Поле будет пустым.");
                        }
                    }

                    // Парсим даты
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
                        )";

                    using (var cmd = new SqlCommand(query, conn))
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

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) // Нарушение уникальности
                {
                    result.Warnings.Add($"Строка {row.RowNumber}: Читатель с номером {row.CardNumber} уже существует в базе");
                }
                else
                {
                    throw;
                }
            }
        }

        private int GetReaderTypeId(SqlConnection conn, string typeName)
        {
            // Сначала пытаемся найти точное совпадение
            string query = "SELECT TypeID FROM ReaderType WHERE TypeName = @TypeName";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return Convert.ToInt32(result);
            }

            // Если не нашли, ищем по частичному совпадению
            query = "SELECT TOP 1 TypeID FROM ReaderType WHERE @TypeName LIKE '%' + TypeName + '%' OR TypeName LIKE '%' + @TypeName + '%'";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return Convert.ToInt32(result);
            }

            // Если ничего не нашли, возвращаем тип по умолчанию (1 - обычно Студент)
            return 1;
        }

        private int? GetGroupId(SqlConnection conn, string groupCode)
        {
            string query = "SELECT GroupID FROM Groups WHERE GroupCode = @GroupCode";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@GroupCode", groupCode);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
        }

        private int? GetPostId(SqlConnection conn, string postName)
        {
            string query = "SELECT ID_Post FROM Post WHERE Post_Name = @PostName";
            using (var cmd = new SqlCommand(query, conn))
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

            // Попробуем разные форматы дат
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

            // Если ничего не подошло, пробуем стандартный парсинг
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
                    // Читаем заголовки
                    string headerLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(headerLine))
                    {
                        result.Errors.Add("Файл пуст");
                        return result;
                    }

                    var headers = headerLine.Split(',');
                    var rows = new List<string[]>();

                    // Читаем все строки
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            rows.Add(ParseCsvLine(line));
                        }
                    }

                    result.TotalRows = rows.Count;

                    // Создаем DataTable в памяти
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

                    // Обрабатываем как обычный DataTable
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
                        // Экранированная кавычка
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

        // Вынесите логику обработки DataTable в отдельный метод
        private void ProcessDataTable(System.Data.DataTable dt, ImportResult result)
        {
            // Проверяем наличие необходимых колонок
            ValidateColumns(dt, result);
            if (result.HasErrors) return;

            // Обрабатываем каждую строку
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