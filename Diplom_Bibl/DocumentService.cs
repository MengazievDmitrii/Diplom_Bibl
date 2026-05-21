using Diplom_Bibl.Helpers;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace Diplom_Bibl.Services
{
    public static class ReceiptActPdfService
    {
        static string ConnectionString = DatabaseHelper.ConnectionString;

        /// <summary>
        /// Генерирует акт о поступлении книг в формате PDF по ID акта.
        /// </summary>
        public static byte[] GenerateReceiptActPdf(int receiptId)
        {
            try
            {
                // 1. Загружаем данные акта + ФИО ответственного лица
                var act = LoadReceiptData(receiptId);
                if (act == null) throw new ArgumentException("Акт не найден.");

                // 2. Загружаем книги с информацией о виде документа и издательстве
                var books = LoadBooksByReceipt(receiptId);
                int totalCopies = books.Sum(b => b.Quantity);
                decimal totalSum = books.Sum(b => b.TotalPrice);

                // 3. Получаем информацию о виде документа и издательстве из первой книги
                string viewBookName = books.FirstOrDefault()?.ViewBookName ?? "";
                string publisherName = books.FirstOrDefault()?.PublisherName ?? "";

                // 4. Создаём PDF
                using (var ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // Шрифт: Arial (встроенный, поддержка кириллицы)
                    BaseFont baseFont = BaseFont.CreateFont(@"C:\Windows\Fonts\arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    Font fontNormal = new Font(baseFont, 11);
                    Font fontBold = new Font(baseFont, 11, Font.BOLD);
                    Font fontHeader = new Font(baseFont, 16, Font.BOLD);

                    // === Шапка ===
                    Paragraph title = new Paragraph($"Акт N {act.ReceiptNumber}", fontHeader);
                    title.Alignment = Element.ALIGN_LEFT;
                    document.Add(title);
                    document.Add(new Paragraph(""));

                    // Дата прописью
                    string month = GetMonthNameRu(act.ReceiptDate.Month);
                    string dateLine = $"Настоящий акт составлен \"{act.ReceiptDate.Day}\" {month} {act.ReceiptDate.Year} г.";
                    document.Add(new Paragraph(dateLine, fontNormal));
                    document.Add(new Paragraph(""));

                    // Ответственное лицо
                    document.Add(new Paragraph(act.ResponsiblePerson, fontNormal));
                    document.Add(new Paragraph("(фамилии, имена, отчества и должности лиц, участвующих", fontNormal));
                    document.Add(new Paragraph("в составлении акта)", fontNormal));
                    document.Add(new Paragraph(""));

                    // О приеме - исправлено: теперь выводим вид документа и издательство
                    string receiptText = $"о приеме в библиотеку {viewBookName}";
                    if (!string.IsNullOrEmpty(publisherName))
                    {
                        receiptText += $" \"{publisherName}\"";
                    }
                    document.Add(new Paragraph(receiptText, fontNormal));
                    document.Add(new Paragraph("(вид документа и от кого получено)", fontNormal));
                    document.Add(new Paragraph(""));

                    // Итоговая сумма - исправлено: теперь правильно считаем копейки
                    int totalRub = (int)Math.Floor(totalSum);
                    int totalKop = (int)((totalSum - totalRub) * 100 + 0.5m); // Округление до целых копеек
                    string totalWords = NumberToWordsRublesWithKopecks(totalRub, totalKop);

                    document.Add(new Paragraph($"в количестве {totalCopies} экземпляров на общую сумму {totalRub:N0} руб. {totalKop:D2} коп.", fontNormal));
                    document.Add(new Paragraph($"({totalWords})", fontNormal));
                    document.Add(new Paragraph(""));

                    // Примечание
                    document.Add(new Paragraph("Список книг (или других документов) прилагается.", fontNormal));
                    document.Add(new Paragraph(""));

                    // Подписи
                    document.Add(new Paragraph("Подписи ________________________________________ (руководителя)", fontNormal));
                    document.Add(new Paragraph("________________________________________ (сотрудника)", fontNormal));
                    document.Add(new Paragraph("________________________________________ (лица, сдавшего книги или другие документы)", fontNormal));
                    document.Add(new Paragraph(""));

                    // Таблица заголовок
                    document.Add(new Paragraph($"Список книг или других документов к акту N {act.ReceiptNumber}*", fontBold));
                    document.Add(new Paragraph(""));

                    // Таблица
                    PdfPTable table = new PdfPTable(6);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 5f, 30f, 5f, 5f, 8f, 10f });

                    // Заголовки
                    table.AddCell(new PdfPCell(new Phrase("N п/п", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Автор, заглавие книги или вид документа", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("руб.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("коп.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Кол-во экз.", fontBold)));
                    table.AddCell(new PdfPCell(new Phrase("Сумма", fontBold)));

                    // Данные
                    for (int i = 0; i < books.Count; i++)
                    {
                        var b = books[i];
                        table.AddCell(new PdfPCell(new Phrase((i + 1).ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(b.Title, fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(b.PriceRub.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(b.PriceKop.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase(b.Quantity.ToString(), fontNormal)));
                        table.AddCell(new PdfPCell(new Phrase($"{b.TotalPrice:N2}", fontNormal)));
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

        // =============== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===============

        private static ActData LoadReceiptData(int receiptId)
        {
            const string sql = @"
                SELECT 
                    r.ReceiptNumber,
                    r.ReceiptDate,
                    u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS ResponsiblePerson,
                    r.Comment
                FROM ReceiptBooks r
                LEFT JOIN Users u ON r.ID_ResponsibleUser = u.ID_User
                WHERE r.IDReceipt = @Id";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", receiptId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ActData
                            {
                                ReceiptNumber = reader["ReceiptNumber"].ToString(),
                                ReceiptDate = Convert.ToDateTime(reader["ReceiptDate"]),
                                ResponsiblePerson = reader["ResponsiblePerson"].ToString(),
                                Comment = reader["Comment"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }
            return null;
        }

        private static List<BookItem> LoadBooksByReceipt(int receiptId)
        {
            var list = new List<BookItem>();
            const string sql = @"
                SELECT 
                    b.TitleBook,
                    ISNULL(b.Price, 0) AS Price,
                    1 AS Quantity,
                    ISNULL(vb.View_Book, '') AS ViewBookName,
                    ISNULL(ph.TitlePublishingHouse, '') AS PublisherName
                FROM Books b
                LEFT JOIN ViewBook vb ON b.ID_View_Book = vb.ID_View_Book
                LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
                WHERE b.IDReceip = @Id";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", receiptId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal price = Convert.ToDecimal(reader["Price"]);
                            int rub = (int)Math.Floor(price);
                            int kop = (int)((price - rub) * 100 + 0.5m); // Округление до целых копеек

                            list.Add(new BookItem
                            {
                                Title = reader["TitleBook"].ToString(),
                                PriceRub = rub,
                                PriceKop = kop,
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                TotalPrice = price * Convert.ToInt32(reader["Quantity"]),
                                ViewBookName = reader["ViewBookName"]?.ToString() ?? "",
                                PublisherName = reader["PublisherName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return list;
        }

        private static string GetMonthNameRu(int month)
        {
            var months = new[]
            {
                "января", "февраля", "марта", "апреля", "мая", "июня",
                "июля", "августа", "сентября", "октября", "ноября", "декабря"
            };
            return months[month - 1];
        }

        // Новая функция для формирования суммы прописью с копейками
        private static string NumberToWordsRublesWithKopecks(int rubles, int kopecks)
        {
            string rublesWords = NumberToWords(rubles, true);
            return $"{rublesWords} {kopecks:D2} копеек";
        }

        // Универсальная функция для преобразования числа в пропись
        private static string NumberToWords(int number, bool isRubles)
        {
            if (number == 0) return "ноль рублей";

            string[] units = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] unitsForThousands = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            List<string> parts = new List<string>();
            int originalNumber = number;

            // Миллионы
            if (number >= 1000000)
            {
                int millions = number / 1000000;
                number %= 1000000;
                parts.Add(ConvertHundreds(millions, units, teens, tens, hundreds));
                parts.Add(GetPluralForm(millions, "миллион", "миллиона", "миллионов"));
            }

            // Тысячи
            if (number >= 1000)
            {
                int thousands = number / 1000;
                number %= 1000;
                parts.Add(ConvertHundreds(thousands, unitsForThousands, teens, tens, hundreds));
                parts.Add(GetPluralForm(thousands, "тысяча", "тысячи", "тысяч"));
            }

            // Остаток
            if (number > 0)
            {
                parts.Add(ConvertHundreds(number, units, teens, tens, hundreds));
            }

            // Добавляем слово "рубль" в правильной форме
            if (isRubles)
            {
                parts.Add(GetPluralForm(originalNumber, "рубль", "рубля", "рублей"));
            }

            return string.Join(" ", parts).Trim();
        }

        private static string ConvertHundreds(int n, string[] units, string[] teens, string[] tens, string[] hundreds)
        {
            List<string> words = new List<string>();
            if (n >= 100)
            {
                words.Add(hundreds[n / 100]);
                n %= 100;
            }
            if (n >= 20)
            {
                words.Add(tens[n / 10]);
                n %= 10;
            }
            else if (n >= 10)
            {
                words.Add(teens[n - 10]);
                n = 0;
            }
            if (n > 0)
            {
                words.Add(units[n]);
            }
            return string.Join(" ", words);
        }

        private static string GetPluralForm(int n, string form1, string form2, string form5)
        {
            int lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 19) return form5;
            int last = n % 10;
            return last == 1 ? form1 : (last >= 2 && last <= 4 ? form2 : form5);
        }

        // =============== DTO ===============
        private class ActData
        {
            public string ReceiptNumber { get; set; }
            public DateTime ReceiptDate { get; set; }
            public string ResponsiblePerson { get; set; }
            public string Comment { get; set; }
        }

        private class BookItem
        {
            public string Title { get; set; }
            public int PriceRub { get; set; }
            public int PriceKop { get; set; }
            public int Quantity { get; set; }
            public decimal TotalPrice { get; set; }
            public string ViewBookName { get; set; }
            public string PublisherName { get; set; }
        }
    }
}