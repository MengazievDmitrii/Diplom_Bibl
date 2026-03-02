using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

namespace Diplom_Bibl.Services
{
    public static class ReaderCardPdfService
    {
        private const string ConnectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        /// <summary>
        /// Генерирует читательский билет в формате PDF по ID читателя
        /// </summary>
        public static byte[] GenerateReaderCardPdf(int readerId)
        {
            try
            {
                // Загружаем данные читателя
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                // Определяем тип читателя и выбираем соответствующий шаблон
                bool isStudent = reader.TypeName?.ToLower().Contains("студент") ?? false;

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // Шрифты
                    BaseFont baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    Font fontNormal = new Font(baseFont, 11);
                    Font fontBold = new Font(baseFont, 11, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 14, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 9);

                    if (isStudent)
                    {
                        GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }
                    else
                    {
                        GenerateEmployeeCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }

                    document.Close();
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка генерации читательского билета: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Генерирует два экземпляра читательского билета на одной странице
        /// </summary>
        public static byte[] GenerateReaderCardPdfTwoCopies(int readerId)
        {
            try
            {
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                bool isStudent = reader.TypeName?.ToLower().Contains("студент") ?? false;

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    BaseFont baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    Font fontNormal = new Font(baseFont, 11);
                    Font fontBold = new Font(baseFont, 11, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 14, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 9);

                    // Первый экземпляр
                    if (isStudent)
                    {
                        GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }
                    else
                    {
                        GenerateEmployeeCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }

                    document.NewPage();

                    // Второй экземпляр
                    if (isStudent)
                    {
                        GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }
                    else
                    {
                        GenerateEmployeeCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);
                    }

                    document.Close();
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка генерации читательского билета: {ex.Message}", ex);
            }
        }

        private static void GenerateStudentCard(Document document, ReaderData reader, Font fontNormal, Font fontBold, Font fontHeader, Font fontSmall)
        {
            // Верхние заметки библиотекаря
            document.Add(new Paragraph("Заметки библиотекаря", fontBold));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph(""));

            // Заголовок
            Paragraph title = new Paragraph("ФОРМУЛЯР ЧИТАТЕЛЯ", fontHeader);
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);
            document.Add(new Paragraph(""));

            // Номер читательского билета
            PdfPTable numberTable = new PdfPTable(2);
            numberTable.WidthPercentage = 100;
            numberTable.SetWidths(new float[] { 10f, 90f });

            numberTable.AddCell(new PdfPCell(new Phrase("№", fontBold)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });
            numberTable.AddCell(new PdfPCell(new Phrase($" {reader.CardNumber}", fontNormal)) { Border = Rectangle.NO_BORDER });
            document.Add(numberTable);
            document.Add(new Paragraph(""));

            // Данные читателя
            AddReaderField(document, "Фамилия", reader.LastName, fontBold, fontNormal);
            AddReaderField(document, "Имя", reader.FirstName, fontBold, fontNormal);
            AddReaderField(document, "Отчество", reader.MiddleName ?? "", fontBold, fontNormal);
            AddReaderField(document, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);
            AddReaderField(document, "Группа", reader.GroupCode ?? "", fontBold, fontNormal);
            AddReaderField(document, "Телефон", reader.PhoneNumber ?? "", fontBold, fontNormal);

            // Домашний адрес (с полями для заполнения)
            document.Add(new Paragraph($"Домашний адрес (по прописке) {reader.HomeAddress ?? "_____________________________"}", fontNormal));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph(""));

            // Дата и подпись
            string membershipDate = reader.MembershipStartDate.ToString("dd.MM.yyyy");
            document.Add(new Paragraph($"Состоит читателем библиотеки с {membershipDate}", fontNormal));
            document.Add(new Paragraph($"Правила обязуюсь выполнять _________________________", fontNormal));
            document.Add(new Paragraph(""));

            // Вторая копия (нижняя часть)
            document.Add(new Paragraph("Заметки библиотекаря", fontBold));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph(""));

            // Второй заголовок
            Paragraph title2 = new Paragraph("ФОРМУЛЯР ЧИТАТЕЛЯ", fontHeader);
            title2.Alignment = Element.ALIGN_CENTER;
            document.Add(title2);
            document.Add(new Paragraph(""));

            // Номер для второй копии
            PdfPTable numberTable2 = new PdfPTable(2);
            numberTable2.WidthPercentage = 100;
            numberTable2.SetWidths(new float[] { 10f, 90f });

            numberTable2.AddCell(new PdfPCell(new Phrase("№", fontBold)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });
            numberTable2.AddCell(new PdfPCell(new Phrase($" {reader.CardNumber}", fontNormal)) { Border = Rectangle.NO_BORDER });
            document.Add(numberTable2);
            document.Add(new Paragraph(""));

            // Данные для второй копии
            AddReaderField(document, "Фамилия", reader.LastName, fontBold, fontNormal);
            AddReaderField(document, "Имя", reader.FirstName, fontBold, fontNormal);
            AddReaderField(document, "Отчество", reader.MiddleName ?? "", fontBold, fontNormal);
            AddReaderField(document, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);
            AddReaderField(document, "Группа", reader.GroupCode ?? "", fontBold, fontNormal);
            AddReaderField(document, "Телефон", reader.PhoneNumber ?? "", fontBold, fontNormal);

            document.Add(new Paragraph($"Домашний адрес (по прописке) {reader.HomeAddress ?? "_____________________________"}", fontNormal));
            document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
            document.Add(new Paragraph(""));

            document.Add(new Paragraph($"Состоит читателем библиотеки с {membershipDate}", fontNormal));
            document.Add(new Paragraph($"Правила обязуюсь выполнять _________________________", fontNormal));

            //// Добавляем таблицу выдачи книг
            //document.NewPage();
            //AddBooksIssuedTable(document, reader, fontBold, fontNormal, fontSmall);
        }

        private static void GenerateEmployeeCard(Document document, ReaderData reader, Font fontNormal, Font fontBold, Font fontHeader, Font fontSmall)
        {
            try
            {
                // Первая копия
                Paragraph title = new Paragraph($"ФОРМУЛЯР ЧИТАТЕЛЯ №{reader.CardNumber}", fontHeader);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                document.Add(new Paragraph(""));

                // Данные сотрудника
                AddReaderField(document, "Фамилия", reader.LastName ?? "", fontBold, fontNormal);

                string fullName = $"{reader.FirstName} {reader.MiddleName}".Trim();
                AddReaderField(document, "Имя, отчество", fullName, fontBold, fontNormal);

                AddReaderField(document, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);

                // Безопасное получение должности
                string postName = !string.IsNullOrEmpty(reader.PostName) ? reader.PostName : "_________________________";
                AddReaderField(document, "Должность", postName, fontBold, fontNormal);

                // Домашний адрес и телефон
                string address = !string.IsNullOrEmpty(reader.HomeAddress) ? reader.HomeAddress : "_________________________";
                string phone = !string.IsNullOrEmpty(reader.PhoneNumber) ? reader.PhoneNumber : "_________________________";
                string addressPhone = $"{address}, тел. {phone}";

                document.Add(new Paragraph($"Домашний адрес, телефон {addressPhone}", fontNormal));
                document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
                document.Add(new Paragraph(""));

                // Дата и подпись
                string membershipDate = reader.MembershipStartDate.ToString("dd.MM.yyyy");
                document.Add(new Paragraph($"Состоит читателем библиотеки с {membershipDate}", fontNormal));
                document.Add(new Paragraph("Правила библиотеки обязуюсь выполнять", fontNormal));
                document.Add(new Paragraph("_________________________ (подпись читателя)", fontNormal));
                document.Add(new Paragraph(""));

                document.NewPage();

                // Вторая копия
                Paragraph title2 = new Paragraph($"ФОРМУЛЯР ЧИТАТЕЛЯ №{reader.CardNumber}", fontHeader);
                title2.Alignment = Element.ALIGN_CENTER;
                document.Add(title2);
                document.Add(new Paragraph(""));

                AddReaderField(document, "Фамилия", reader.LastName ?? "", fontBold, fontNormal);
                AddReaderField(document, "Имя, отчество", fullName, fontBold, fontNormal);
                AddReaderField(document, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);
                AddReaderField(document, "Должность", postName, fontBold, fontNormal);

                document.Add(new Paragraph($"Домашний адрес, телефон {addressPhone}", fontNormal));
                document.Add(new Paragraph("____________________________________________________________________________________________________", fontSmall));
                document.Add(new Paragraph(""));

                document.Add(new Paragraph($"Состоит читателем библиотеки с {membershipDate}", fontNormal));
                document.Add(new Paragraph("Правила библиотеки обязуюсь выполнять", fontNormal));
                document.Add(new Paragraph("_________________________ (подпись читателя)", fontNormal));
                document.Add(new Paragraph(""));

                //// Добавляем таблицу выдачи книг
                //document.NewPage();
                //AddBooksIssuedTable(document, reader, fontBold, fontNormal, fontSmall);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при формировании карточки сотрудника: {ex.Message}", ex);
            }
        }
        private static void AddReaderField(Document document, string label, string value, Font fontBold, Font fontNormal)
        {
            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 20f, 80f });

            PdfPCell labelCell = new PdfPCell(new Phrase($"{label}:", fontBold))
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingRight = 10
            };

            PdfPCell valueCell = new PdfPCell(new Phrase(value, fontNormal))
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_LEFT
            };

            table.AddCell(labelCell);
            table.AddCell(valueCell);
            document.Add(table);
        }

        //private static void AddBooksIssuedTable(Document document, ReaderData reader, Font fontBold, Font fontNormal, Font fontSmall)
        //{
        //    // Заголовок таблицы
        //    Paragraph tableTitle = new Paragraph("Книги, выданные читателю", fontBold);
        //    tableTitle.Alignment = Element.ALIGN_CENTER;
        //    document.Add(tableTitle);
        //    document.Add(new Paragraph(""));

        //    // Создаем таблицу для книг
        //    PdfPTable table = new PdfPTable(5);
        //    table.WidthPercentage = 100;
        //    table.SetWidths(new float[] { 10f, 10f, 40f, 15f, 25f });

        //    // Заголовки таблицы
        //    string[] headers = { "Дата выдачи", "Инвент. номер", "Автор и заглавие книги", "Роспись читателя", "Отметка о возвращении" };
        //    foreach (string header in headers)
        //    {
        //        PdfPCell cell = new PdfPCell(new Phrase(header, fontBold));
        //        cell.BackgroundColor = BaseColor.LIGHT_GRAY;
        //        cell.HorizontalAlignment = Element.ALIGN_CENTER;
        //        cell.Padding = 5;
        //        table.AddCell(cell);
        //    }

        //    // Загружаем выданные книги читателя
        //    var issuedBooks = LoadIssuedBooks(reader.ReaderID);

        //    if (issuedBooks.Count > 0)
        //    {
        //        foreach (var book in issuedBooks)
        //        {
        //            table.AddCell(new PdfPCell(new Phrase(book.IssueDate.ToString("dd.MM.yyyy"), fontNormal)) { HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase(book.InventoryNumber, fontNormal)) { HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase($"{book.Author} - {book.Title}", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase(book.ReturnDate?.ToString("dd.MM.yyyy") ?? "", fontNormal)) { Padding = 3 });
        //        }
        //    }
        //    else
        //    {
        //        // Добавляем пустые строки для заполнения
        //        for (int i = 0; i < 15; i++)
        //        {
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //            table.AddCell(new PdfPCell(new Phrase("", fontNormal)) { Padding = 3 });
        //        }
        //    }

        //    document.Add(table);
        //}

        private static ReaderData LoadReaderData(int readerId)
        {
            const string sql = @"
        SELECT 
            r.ReaderID, r.CardNumber, r.LastName, r.FirstName, r.MiddleName,
            r.TypeID, rt.TypeName,
            r.GroupID, g.GroupCode,
            r.Post, p.Post_Name,
            r.Speciality, r.PhoneNumber, r.HomeAddress,
            r.MembershipStartDate, r.FormFillDate
        FROM ReaderForm r
        LEFT JOIN ReaderType rt ON r.TypeID = rt.TypeID
        LEFT JOIN Groups g ON r.GroupID = g.GroupID
        LEFT JOIN Post p ON r.Post = p.ID_Post
        WHERE r.ReaderID = @ReaderId";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Безопасное чтение всех полей с проверкой на DBNull
                            int readerId_val = 0;
                            if (reader["ReaderID"] != DBNull.Value)
                                readerId_val = Convert.ToInt32(reader["ReaderID"]);

                            int typeId = 0;
                            if (reader["TypeID"] != DBNull.Value)
                                typeId = Convert.ToInt32(reader["TypeID"]);

                            int? groupId = null;
                            if (reader["GroupID"] != DBNull.Value)
                                groupId = Convert.ToInt32(reader["GroupID"]);

                            int? postId = null;
                            if (reader["Post"] != DBNull.Value)
                                postId = Convert.ToInt32(reader["Post"]);

                            DateTime membershipStartDate = DateTime.Now;
                            if (reader["MembershipStartDate"] != DBNull.Value)
                                membershipStartDate = Convert.ToDateTime(reader["MembershipStartDate"]);

                            DateTime formFillDate = DateTime.Now;
                            if (reader["FormFillDate"] != DBNull.Value)
                                formFillDate = Convert.ToDateTime(reader["FormFillDate"]);

                            return new ReaderData
                            {
                                ReaderID = readerId_val,
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeID = typeId,
                                TypeName = reader["TypeName"]?.ToString() ?? "",
                                GroupID = groupId,
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                PostID = postId,
                                PostName = reader["Post_Name"]?.ToString() ?? "",
                                Speciality = reader["Speciality"]?.ToString() ?? "",
                                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                                HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                                MembershipStartDate = membershipStartDate,
                                FormFillDate = formFillDate
                            };
                        }
                    }
                }
            }
            return null;
        }

        //private static List<IssuedBookData> LoadIssuedBooks(int readerId)
        //{
        //    var books = new List<IssuedBookData>();

        //    const string sql = @"
        //        SELECT 
        //            bi.IssueDate,
        //            b.InventaryNumber,
        //            b.TitleBook,
        //            ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS Author,
        //            bi.ReturnDate
        //        FROM BookIssuance bi
        //        INNER JOIN Books b ON bi.BookID = b.IDBook
        //        LEFT JOIN Authors a ON b.Author = a.ID_Autor
        //        WHERE bi.ReaderID = @ReaderId
        //        ORDER BY bi.IssueDate DESC";

        //    using (var conn = new SqlConnection(ConnectionString))
        //    {
        //        conn.Open();
        //        using (var cmd = new SqlCommand(sql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@ReaderId", readerId);
        //            using (var reader = cmd.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    books.Add(new IssuedBookData
        //                    {
        //                        IssueDate = Convert.ToDateTime(reader["IssueDate"]),
        //                        InventoryNumber = reader["InventaryNumber"]?.ToString() ?? "",
        //                        Title = reader["TitleBook"]?.ToString() ?? "",
        //                        Author = reader["Author"]?.ToString() ?? "",
        //                        ReturnDate = reader["ReturnDate"] == DBNull.Value ?  : Convert.ToDateTime(reader["ReturnDate"])
        //                    });
        //                }
        //            }
        //        }
        //    }
        //    return books;
        //}

        // DTO классы
        private class ReaderData
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public int TypeID { get; set; }
            public string TypeName { get; set; }
            public int? GroupID { get; set; }
            public string GroupCode { get; set; }
            public int? PostID { get; set; }
            public string PostName { get; set; }
            public string Speciality { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public DateTime MembershipStartDate { get; set; }
            public DateTime FormFillDate { get; set; }
        }

        private class IssuedBookData
        {
            public DateTime IssueDate { get; set; }
            public string InventoryNumber { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public DateTime? ReturnDate { get; set; }
        }
    }
}