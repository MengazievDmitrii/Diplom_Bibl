using Diplom_Bibl.Helpers;
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
        static string conn_str = DatabaseHelper.ConnectionString;

        // Модель истории выдачи
        private class IssuanceHistoryItem
        {
            public DateTime IssuanceDate { get; set; }
            public string InventaryNumber { get; set; }
            public string BookTitle { get; set; }
            public string AuthorName { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ReturnDate { get; set; }
        }

        /// <summary>
        /// Генерирует читательский билет в формате PDF по ID читателя
        /// </summary>
        public static byte[] GenerateReaderCardPdf(int readerId)
        {
            try
            {
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                var history = LoadIssuanceHistory(readerId);

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 30, 30, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    BaseFont baseFont;
                    try
                    {
                        baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\times.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    catch
                    {
                        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    }

                    Font fontNormal = new Font(baseFont, 10);
                    Font fontBold = new Font(baseFont, 10, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 14, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 8);
                    Font fontTitle = new Font(baseFont, 16, Font.BOLD);

                    // Определяем тип читателя (студент или сотрудник/преподаватель)
                    bool isStudent = reader.TypeName?.ToLower().Contains("студент") ?? false;

                    if (isStudent)
                    {
                        GenerateStudentCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);
                    }
                    else
                    {
                        GenerateEmployeeCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);
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
        /// Генерирует два экземпляра читательского билета
        /// </summary>
        public static byte[] GenerateReaderCardPdfTwoCopies(int readerId)
        {
            try
            {
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                var history = LoadIssuanceHistory(readerId);

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 30, 30, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    BaseFont baseFont;
                    try
                    {
                        baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\times.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    catch
                    {
                        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    }

                    Font fontNormal = new Font(baseFont, 10);
                    Font fontBold = new Font(baseFont, 10, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 14, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 8);
                    Font fontTitle = new Font(baseFont, 16, Font.BOLD);

                    bool isStudent = reader.TypeName?.ToLower().Contains("студент") ?? false;

                    // Первая копия
                    if (isStudent)
                        GenerateStudentCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);
                    else
                        GenerateEmployeeCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);

                    document.NewPage();

                    // Вторая копия
                    if (isStudent)
                        GenerateStudentCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);
                    else
                        GenerateEmployeeCard(document, reader, history, fontNormal, fontBold, fontHeader, fontSmall, fontTitle);

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
        /// Генерация читательского билета для СТУДЕНТА
        /// </summary>
        private static void GenerateStudentCard(Document document, ReaderData reader, List<IssuanceHistoryItem> history,
            Font fontNormal, Font fontBold, Font fontHeader, Font fontSmall, Font fontTitle)
        {
            // Создаем таблицу для двух колонок
            PdfPTable mainTable = new PdfPTable(2);
            mainTable.WidthPercentage = 100;
            mainTable.SetWidths(new float[] { 35f, 65f });
            mainTable.SpacingBefore = 10;
            mainTable.SpacingAfter = 10;

            // ======= ЛЕВАЯ КОЛОНКА (Заметки библиотекаря) =======
            PdfPCell leftCell = new PdfPCell();
            leftCell.Border = Rectangle.NO_BORDER;
            leftCell.VerticalAlignment = Element.ALIGN_TOP;
            leftCell.PaddingRight = 10;

            Paragraph leftNotesTitle = new Paragraph("Заметки библиотекаря", fontBold);
            leftNotesTitle.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(leftNotesTitle);
            leftCell.AddElement(new Paragraph(" "));

            for (int i = 0; i < 10; i++)
            {
                Paragraph line = new Paragraph("__________________________________________", fontSmall);
                line.Alignment = Element.ALIGN_LEFT;
                leftCell.AddElement(line);
                leftCell.AddElement(new Paragraph(" "));
            }

            mainTable.AddCell(leftCell);

            // ======= ПРАВАЯ КОЛОНКА (Данные читателя + история) =======
            PdfPCell rightCell = new PdfPCell();
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.VerticalAlignment = Element.ALIGN_TOP;
            rightCell.PaddingLeft = 10;

            // Заголовок
            Paragraph title = new Paragraph("ФОРМУЛЯР ЧИТАТЕЛЯ", fontTitle);
            title.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(title);
            rightCell.AddElement(new Paragraph(" "));

            // Номер билета
            Paragraph numberParagraph = new Paragraph($"№ {reader.CardNumber}", fontBold);
            numberParagraph.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(numberParagraph);
            rightCell.AddElement(new Paragraph(" "));

            // Данные читателя
            AddStudentFieldToCell(rightCell, "Фамилия", reader.LastName, fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Имя", reader.FirstName, fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Отчество", reader.MiddleName ?? "", fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Группа", reader.GroupCode ?? "", fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Телефон", reader.PhoneNumber ?? "", fontBold, fontNormal);
            AddStudentAddressToCell(rightCell, "Домашний адрес (по прописке)", reader.HomeAddress ?? "_____________________________", fontBold, fontNormal);
            AddStudentFieldToCell(rightCell, "Состоит читателем библиотеки с", reader.MembershipStartDate.ToString("dd.MM.yyyy"), fontBold, fontNormal);
            AddStudentSignatureToCell(rightCell, "Правила обязуюсь выполнять", "_________________________", fontBold, fontNormal);

            rightCell.AddElement(new Paragraph(" "));
            rightCell.AddElement(new Paragraph(" "));

            // Таблица истории выдач
            Paragraph historyTitle = new Paragraph("История выдачи книг", fontBold);
            historyTitle.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(historyTitle);
            rightCell.AddElement(new Paragraph(" "));

            PdfPTable historyTable = new PdfPTable(5);
            historyTable.WidthPercentage = 100;
            historyTable.SetWidths(new float[] { 10f, 15f, 40f, 15f, 20f });

            // Заголовки таблицы
            AddTableHeader(historyTable, "Дата выдачи", fontBold);
            AddTableHeader(historyTable, "Инв. №", fontBold);
            AddTableHeader(historyTable, "Автор и заглавие книги", fontBold);
            AddTableHeader(historyTable, "Роспись читателя", fontBold);
            AddTableHeader(historyTable, "Отметка о возвращении", fontBold);

            // Данные истории (последние 10 выдач)
            int rowCount = 0;
            foreach (var item in history)
            {
                if (rowCount++ >= 15) break;
                AddTableCell(historyTable, item.IssuanceDate.ToString("dd.MM.yyyy"), fontNormal);
                AddTableCell(historyTable, item.InventaryNumber, fontNormal);
                AddTableCell(historyTable, $"{item.AuthorName} — {item.BookTitle}", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, item.ReturnDate?.ToString("dd.MM.yyyy") ?? "", fontNormal);
            }

            // Заполняем пустые строки до 15
            for (int i = rowCount; i < 15; i++)
            {
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
            }

            rightCell.AddElement(historyTable);
            mainTable.AddCell(rightCell);
            document.Add(mainTable);
        }

        /// <summary>
        /// Генерация читательского билета для СОТРУДНИКА/ПРЕПОДАВАТЕЛЯ
        /// </summary>
        private static void GenerateEmployeeCard(Document document, ReaderData reader, List<IssuanceHistoryItem> history,
            Font fontNormal, Font fontBold, Font fontHeader, Font fontSmall, Font fontTitle)
        {
            // Создаем таблицу для двух колонок
            PdfPTable mainTable = new PdfPTable(2);
            mainTable.WidthPercentage = 100;
            mainTable.SetWidths(new float[] { 35f, 65f });
            mainTable.SpacingBefore = 10;
            mainTable.SpacingAfter = 10;

            // ======= ЛЕВАЯ КОЛОНКА (Заметки библиотекаря) =======
            PdfPCell leftCell = new PdfPCell();
            leftCell.Border = Rectangle.NO_BORDER;
            leftCell.VerticalAlignment = Element.ALIGN_TOP;
            leftCell.PaddingRight = 10;

            Paragraph leftNotesTitle = new Paragraph("Заметки библиотекаря", fontBold);
            leftNotesTitle.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(leftNotesTitle);
            leftCell.AddElement(new Paragraph(" "));

            for (int i = 0; i < 10; i++)
            {
                Paragraph line = new Paragraph("______________________________________", fontSmall);
                line.Alignment = Element.ALIGN_LEFT;
                leftCell.AddElement(line);
                leftCell.AddElement(new Paragraph(" "));
            }

            mainTable.AddCell(leftCell);

            // ======= ПРАВАЯ КОЛОНКА (Данные читателя) =======
            PdfPCell rightCell = new PdfPCell();
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.VerticalAlignment = Element.ALIGN_TOP;
            rightCell.PaddingLeft = 10;

            // Заголовок
            Paragraph title = new Paragraph($"ФОРМУЛЯР ЧИТАТЕЛЯ № {reader.CardNumber}", fontTitle);
            title.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(title);
            rightCell.AddElement(new Paragraph(" "));

            // Данные читателя
            AddEmployeeFieldToCell(rightCell, "Фамилия", reader.LastName, fontBold, fontNormal);
            AddEmployeeFieldToCell(rightCell, "Имя, отчество", $"{reader.FirstName} {reader.MiddleName}".Trim(), fontBold, fontNormal);
            AddEmployeeFieldToCell(rightCell, "Должность", reader.PostName ?? "", fontBold, fontNormal);
            AddEmployeeAddressToCell(rightCell, "Домашний адрес, телефон",
                $"{reader.HomeAddress ?? ""}, {reader.PhoneNumber ?? ""}".Trim(',', ' '), fontBold, fontNormal);
            AddEmployeeFieldToCell(rightCell, "Состоит читателем библиотеки с", reader.MembershipStartDate.ToString("dd.MM.yyyy"), fontBold, fontNormal);

            Paragraph signaturePara = new Paragraph("Правила библиотеки обязуюсь выполнять", fontNormal);
            rightCell.AddElement(signaturePara);
            rightCell.AddElement(new Paragraph("____________________________________ (подпись читателя)", fontNormal));

            rightCell.AddElement(new Paragraph(" "));
            rightCell.AddElement(new Paragraph(" "));

            // Таблица истории выдач
            Paragraph historyTitle = new Paragraph("История выдачи книг", fontBold);
            historyTitle.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(historyTitle);
            rightCell.AddElement(new Paragraph(" "));

            PdfPTable historyTable = new PdfPTable(5);
            historyTable.WidthPercentage = 100;
            historyTable.SetWidths(new float[] { 10f, 15f, 40f, 15f, 20f });

            // Заголовки таблицы
            AddTableHeader(historyTable, "Дата выдачи", fontBold);
            AddTableHeader(historyTable, "Инв. №", fontBold);
            AddTableHeader(historyTable, "Автор и заглавие книги", fontBold);
            AddTableHeader(historyTable, "Роспись читателя", fontBold);
            AddTableHeader(historyTable, "Отметка о возвращении", fontBold);

            // Данные истории (последние 15 выдач)
            int rowCount = 0;
            foreach (var item in history)
            {
                if (rowCount++ >= 15) break;
                AddTableCell(historyTable, item.IssuanceDate.ToString("dd.MM.yyyy"), fontNormal);
                AddTableCell(historyTable, item.InventaryNumber, fontNormal);
                AddTableCell(historyTable, $"{item.AuthorName} — {item.BookTitle}", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, item.ReturnDate?.ToString("dd.MM.yyyy") ?? "", fontNormal);
            }

            // Заполняем пустые строки до 15
            for (int i = rowCount; i < 15; i++)
            {
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
                AddTableCell(historyTable, "", fontNormal);
            }

            rightCell.AddElement(historyTable);
            mainTable.AddCell(rightCell);
            document.Add(mainTable);
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        private static void AddStudentFieldToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();
            p.Add(new Chunk($"{label}: ", fontBold));
            p.Add(new Chunk(value, fontNormal));
            cell.AddElement(p);
        }

        private static void AddStudentAddressToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph labelPara = new Paragraph(label, fontBold);
            cell.AddElement(labelPara);
            Paragraph valuePara = new Paragraph(value, fontNormal);
            valuePara.IndentationLeft = 10;
            cell.AddElement(valuePara);
        }

        private static void AddStudentSignatureToCell(PdfPCell cell, string label, string signature, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();
            p.Add(new Chunk($"{label} ", fontBold));
            p.Add(new Chunk(signature, fontNormal));
            cell.AddElement(p);
        }

        private static void AddEmployeeFieldToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();
            p.Add(new Chunk($"{label} ", fontBold));
            p.Add(new Chunk(value, fontNormal));
            cell.AddElement(p);
        }

        private static void AddEmployeeAddressToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();
            p.Add(new Chunk($"{label}", fontBold));
            p.Add(new Chunk(value, fontNormal));
            cell.AddElement(p);
        }

        private static void AddTableHeader(PdfPTable table, string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = new BaseColor(230, 230, 230);
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private static void AddTableCell(PdfPTable table, string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.Padding = 4;
            table.AddCell(cell);
        }

        // ==================== ЗАГРУЗКА ДАННЫХ ИЗ БД ====================

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

            using (var conn = new SqlConnection(conn_str))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ReaderData
                            {
                                ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeName = reader["TypeName"]?.ToString() ?? "",
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                Speciality = reader["Speciality"]?.ToString() ?? "",
                                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                                HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                                PostName = reader["Post_Name"]?.ToString() ?? "",
                                MembershipStartDate = reader["MembershipStartDate"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["MembershipStartDate"])
                                    : DateTime.Now
                            };
                        }
                    }
                }
            }
            return null;
        }

        private static List<IssuanceHistoryItem> LoadIssuanceHistory(int readerId)
        {
            var history = new List<IssuanceHistoryItem>();

            const string sql = @"
                SELECT 
                    i.IssuanceDate,
                    b.InventaryNumber,
                    b.TitleBook,
                    ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                    i.PlannedReturnDate,
                    i.ReturnDate
                FROM BookIssuance i
                INNER JOIN Books b ON i.BookId = b.IDBook
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                WHERE i.ReaderId = @ReaderId
                ORDER BY i.IssuanceDate DESC";

            using (var conn = new SqlConnection(conn_str))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            history.Add(new IssuanceHistoryItem
                            {
                                IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                InventaryNumber = reader["InventaryNumber"]?.ToString() ?? "",
                                BookTitle = reader["TitleBook"]?.ToString() ?? "",
                                AuthorName = reader["AuthorName"]?.ToString() ?? "",
                                PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                ReturnDate = reader["ReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReturnDate"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }

            return history;
        }

        private class ReaderData
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string TypeName { get; set; }
            public string GroupCode { get; set; }
            public string Speciality { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public string PostName { get; set; }
            public DateTime MembershipStartDate { get; set; }
        }
    }
}