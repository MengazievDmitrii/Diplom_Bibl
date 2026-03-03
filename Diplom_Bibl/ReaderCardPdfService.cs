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
        /// Генерирует читательский билет в формате PDF по ID читателя (формат А5, одна копия)
        /// </summary>
        public static byte[] GenerateReaderCardPdf(int readerId)
        {
            try
            {
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                using (var ms = new MemoryStream())
                {
                    // Формат А5 (148.5 x 210 мм) в пунктах
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // Шрифт Times New Roman
                    BaseFont baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\times.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    Font fontNormal = new Font(baseFont, 9);
                    Font fontBold = new Font(baseFont, 9, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 12, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 7);

                    GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);

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
        /// Генерирует два экземпляра читательского билета (две копии на разных страницах)
        /// </summary>
        public static byte[] GenerateReaderCardPdfTwoCopies(int readerId)
        {
            try
            {
                var reader = LoadReaderData(readerId);
                if (reader == null) throw new ArgumentException("Читатель не найден.");

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    BaseFont baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\times.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    Font fontNormal = new Font(baseFont, 12);
                    Font fontBold = new Font(baseFont, 16, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 14, Font.BOLD);
                    Font fontSmall = new Font(baseFont, 11);

                    // Первая копия
                    GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);

                    document.NewPage();

                    // Вторая копия
                    GenerateStudentCard(document, reader, fontNormal, fontBold, fontHeader, fontSmall);

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
            // Создаем таблицу для двух колонок
            PdfPTable mainTable = new PdfPTable(2);
            mainTable.WidthPercentage = 100;
            mainTable.SetWidths(new float[] { 50f, 50f }); // Левая колонка 40%, правая 60%
            mainTable.SpacingBefore = 10;
            mainTable.SpacingAfter = 10;

            // ======= ЛЕВАЯ КОЛОНКА (Заметки библиотекаря) =======
            PdfPCell leftCell = new PdfPCell();
            leftCell.Border = Rectangle.NO_BORDER;
            leftCell.VerticalAlignment = Element.ALIGN_TOP;
            leftCell.PaddingRight =115;

            // Заголовок заметок
            Paragraph leftNotesTitle = new Paragraph("Заметки библиотекаря", fontBold);
            leftNotesTitle.Alignment = Element.ALIGN_CENTER;
            leftCell.AddElement(leftNotesTitle);

            // Линии для заметок (8 линий для заполнения)
            for (int i = 0; i < 8; i++)
            {
                Paragraph line = new Paragraph("_____________________________________", fontSmall);
                line.Alignment = Element.ALIGN_LEFT;
                leftCell.AddElement(line);
                leftCell.AddElement(new Paragraph(" ")); // Пустая строка между линиями
            }

            mainTable.AddCell(leftCell);

            // ======= ПРАВАЯ КОЛОНКА (Данные читателя) =======
            PdfPCell rightCell = new PdfPCell();
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.VerticalAlignment = Element.ALIGN_TOP;
            rightCell.PaddingLeft = 10;

            // Заголовок ФОРМУЛЯР ЧИТАТЕЛЯ
            Paragraph title = new Paragraph("ФОРМУЛЯР ЧИТАТЕЛЯ", fontHeader);
            title.Alignment = Element.ALIGN_CENTER;
            rightCell.AddElement(title);

            rightCell.AddElement(new Paragraph(" "));

            // Номер читательского билета
            Paragraph numberParagraph = new Paragraph($"№ {reader.CardNumber}", fontBold);
            numberParagraph.Alignment = Element.ALIGN_LEFT;
            rightCell.AddElement(numberParagraph);

            rightCell.AddElement(new Paragraph(" "));

            // Данные читателя
            AddReaderFieldToCell(rightCell, "Фамилия", reader.LastName, fontBold, fontNormal);
            AddReaderFieldToCell(rightCell, "Имя", reader.FirstName, fontBold, fontNormal);
            AddReaderFieldToCell(rightCell, "Отчество", reader.MiddleName ?? "", fontBold, fontNormal);
            AddReaderFieldToCell(rightCell, "Специальность", reader.Speciality ?? "", fontBold, fontNormal);
            AddReaderFieldToCell(rightCell, "Группа", reader.GroupCode ?? "", fontBold, fontNormal);
            AddReaderFieldToCell(rightCell, "Телефон", reader.PhoneNumber ?? "", fontBold, fontNormal);
            AddAddressToCell(rightCell, "Домашний адрес (по прописке)", reader.HomeAddress ?? "_____________________________", fontBold, fontNormal);
            string membershipDate = reader.MembershipStartDate.ToString("dd.MM.yyyy");
            AddDateToCell(rightCell, "Состоит читателем библиотеки с", membershipDate, fontBold, fontNormal);
            // Правила обязуюсь выполнять
            AddSignatureToCell(rightCell, "Правила обязуюсь выполнять", "_________________________", fontBold, fontNormal);
            mainTable.AddCell(rightCell);
            // Добавляем основную таблицу в документ
            document.Add(mainTable);
        }

        private static void AddReaderFieldToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();

            // Жирная метка
            Chunk labelChunk = new Chunk($"{label}: ", fontBold);
            p.Add(labelChunk);

            // Значение
            Chunk valueChunk = new Chunk(value, fontNormal);
            p.Add(valueChunk);

            cell.AddElement(p);
        }

        private static void AddAddressToCell(PdfPCell cell, string label, string value, Font fontBold, Font fontNormal)
        {
            Paragraph labelPara = new Paragraph(label, fontBold);
            cell.AddElement(labelPara);

            Paragraph valuePara = new Paragraph(value, fontNormal);
            valuePara.IndentationLeft = 10;
            cell.AddElement(valuePara);
        }

        private static void AddDateToCell(PdfPCell cell, string label, string date, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();

            // Добавляем метку жирным шрифтом
            Chunk labelChunk = new Chunk($"{label} ", fontBold);
            p.Add(labelChunk);

            // Добавляем дату обычным шрифтом
            Chunk dateChunk = new Chunk(date, fontNormal);
            p.Add(dateChunk);

            cell.AddElement(p);
        }

        private static void AddSignatureToCell(PdfPCell cell, string label, string signature, Font fontBold, Font fontNormal)
        {
            Paragraph p = new Paragraph();

            // Добавляем метку жирным шрифтом
            Chunk labelChunk = new Chunk($"{label} ", fontBold);
            p.Add(labelChunk);

            // Добавляем строку для подписи обычным шрифтом
            Chunk signatureChunk = new Chunk(signature, fontNormal);
            p.Add(signatureChunk);

            cell.AddElement(p);
        }

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
            public DateTime MembershipStartDate { get; set; }
        }
    }
}