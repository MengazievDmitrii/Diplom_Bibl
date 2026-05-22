using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Data.SqlClient;
using System.IO;
using Diplom_Bibl.Helpers;


namespace Diplom_Bibl.Services
{
    public static class DisposalActPdfService
    {
        static string ConnectionString = DatabaseHelper.ConnectionString;

        public static byte[] GenerateDisposalActPdf(int writeOffId)
        {
            try
            {
                //var book = LoadItems(writeOffId);
                //string booktitle = LoadItems(book.TitleBook);

                var act = LoadAct(writeOffId);
                if (act == null) throw new ArgumentException("Акт списания не найден.");

                // Получаем текст причины по ID
                string reasonText = GetReasonName(act.ReasonID);
                var items = LoadItems(writeOffId);
                int totalCopies = 0;
                decimal totalSum = 0;
                foreach (var item in items)
                {
                    totalCopies += item.Quantity;
                    totalSum += item.TotalPrice;
                }

                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // === Надёжная загрузка Arial с поддержкой кириллицы ===
                    BaseFont baseFont;
                    try
                    {
                        baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    catch
                    {
                        // Fallback: стандартный шрифт с поддержкой UTF-8
                        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    }

                    Font fontNormal = new Font(baseFont, 11);
                    Font fontBold = new Font(baseFont, 11, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 16, Font.BOLD);

                    // === Заголовок ===
                    Paragraph title = new Paragraph($"Акт N {act.DisposalNumber}", fontHeader);
                    title.Alignment = Element.ALIGN_LEFT;
                    document.Add(title);
                    document.Add(new Paragraph(" "));

                    // === Дата ===
                    string month = GetMonthNameRu(act.DisposalDate.Month);
                    string dateLine = $"Настоящий акт составлен \"{act.DisposalDate.Day}\" {month} {act.DisposalDate.Year} г.";
                    document.Add(new Paragraph(dateLine, fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Участники ===
                    document.Add(new Paragraph(act.Participants, fontNormal));
                    document.Add(new Paragraph("(фамилии, имена, отчества и должности лиц, участвующих", fontNormal));
                    document.Add(new Paragraph("в составлении акта)", fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Основание списания ===
                    document.Add(new Paragraph($"о списании из библиотеки  {reasonText}", fontNormal));
                    document.Add(new Paragraph("(вид документа и основание: ветхость, утрата и т.п.)", fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Итоговая сумма ===
                    string totalRub = Math.Floor(totalSum).ToString("N0");
                    string totalKop = ((int)(totalSum * 100) % 100).ToString("D2");
                    string totalWords = NumberToWordsRubles((int)totalSum);

                    document.Add(new Paragraph($"в количестве {totalCopies} экземпляров на общую сумму {totalRub} руб. {totalKop} коп.", fontNormal));
                    document.Add(new Paragraph($"({totalWords})", fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Примечание ===
                    document.Add(new Paragraph("Список книг (или других документов) прилагается.", fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Подписи ===
                    document.Add(new Paragraph("Подписи ________________________________________ (руководителя)", fontNormal));
                    document.Add(new Paragraph("________________________________________ (сотрудника)", fontNormal));
                    document.Add(new Paragraph("________________________________________ (лица, принявшего документы)", fontNormal));
                    document.Add(new Paragraph(" "));

                    // === Таблица ===
                    document.Add(new Paragraph($"Список книг или других документов к акту N {act.DisposalNumber}*", fontBold));
                    document.Add(new Paragraph(" "));

                    PdfPTable table = new PdfPTable(6);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 5f, 30f, 5f, 5f, 8f, 10f });

                    table.AddCell(new PdfPCell(new Phrase("N п/п", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Автор, заглавие книги или вид документа", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("руб.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("коп.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Кол-во экз.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Сумма", fontBold)));

                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        table.AddCell(new PdfPCell(new Phrase((i + 1).ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(item.TitleBook, fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(item.PriceRub.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(item.PriceKop.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(item.Quantity.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase($"{item.TotalPrice:N2}", fontNormal)));
                    }

                    document.Add(table);
                    document.Close();
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка генерации PDF: {ex.Message}", ex);
            }
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

        private static string GetMonthNameRu(int month)
        {
            var months = new[]
            {
                "января", "февраля", "марта", "апреля", "мая", "июня",
                "июля", "августа", "сентября", "октября", "ноября", "декабря"
            };
            return months[month - 1];
        }

        private static string NumberToWordsRubles(int number)
        {
            if (number == 0) return "ноль рублей 00 копеек";
            // Для диплома можно оставить цифры
            return $"{number} рублей 00 копеек";
        }

        // === ЗАГРУЗКА ДАННЫХ ===

        private static ActData LoadAct(int id)
        {
            const string sql = @"
                SELECT DisposalNumber, DisposalDate, Participants, Reason
                FROM DisposalBooks
                WHERE IDDisposal = @Id";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ActData
                            {
                                DisposalNumber = reader["DisposalNumber"].ToString(),
                                DisposalDate = Convert.ToDateTime(reader["DisposalDate"]),
                                Participants = reader["Participants"].ToString(),
                                ReasonID = Convert.ToInt32(reader["Reason"])
                            };
                        }
                    }
                }
            }
            return null;
        }

        private static string GetReasonName(int reasonId)
        {
            const string sql = "SELECT ReasonName FROM ReasonDisposalBook WHERE ID_Reason = @Id";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", reasonId);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Не указано";
                }
            }
        }

        private static ItemData[] LoadItems(int writeOffId)
        {
            var list = new System.Collections.Generic.List<ItemData>();
            const string sql = @"
                SELECT TitleBook, PriceRub, PriceKop, Quantity,
                       (PriceRub + PriceKop / 100.0) * Quantity AS TotalPrice
                FROM DisposalItems
                WHERE IDDisposal = @Id";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", writeOffId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ItemData
                            {
                                TitleBook = reader["TitleBook"].ToString(),
                                PriceRub = Convert.ToInt32(reader["PriceRub"]),
                                PriceKop = Convert.ToInt32(reader["PriceKop"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                TotalPrice = Convert.ToDecimal(reader["TotalPrice"])
                            });
                        }
                    }
                }
            }
            return list.ToArray();
        }

        // === DTO ===
        private class ActData
        {
            public string DisposalNumber { get; set; }
            public DateTime DisposalDate { get; set; }
            public string Participants { get; set; }
            public int ReasonID { get; set; } // ← теперь int
        }

        private class ItemData
        {
            public string TitleBook { get; set; }
            public int PriceRub { get; set; }
            public int PriceKop { get; set; }
            public int Quantity { get; set; }
            public decimal TotalPrice { get; set; }
        }
    }
}