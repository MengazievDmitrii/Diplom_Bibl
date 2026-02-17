using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Windows;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Diplom_Bibl.Services
{
    public static class DocumentService
    {
        /// <summary>
        /// Генерирует акт о поступлении книг по ID акта.
        /// </summary>
        /// <param name="receiptId">ID записи в таблице ReceiptBooks</param>
        /// <param name="connectionString">Строка подключения к БД</param>
        public static void GenerateReceiptAct(int receiptId, string connectionString)
        {
            try
            {
                // 1. Загружаем данные акта
                var act = LoadReceiptData(receiptId, connectionString);
                if (act == null)
                {
                    MessageBox.Show("Акт не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Загружаем книги
                var books = LoadBooksByReceipt(receiptId, connectionString);

                // 3. Формируем путь для сохранения
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string safeNumber = string.Join("", act.ReceiptNumber.Split(Path.GetInvalidFileNameChars()));
                string fileName = Path.Combine(desktop, $"Акт_поступления_{safeNumber}.docx");

                // 4. Создаём документ
                using (WordprocessingDocument doc = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // === Шапка акта ===
                    AddParagraph(body, $"Акт N {act.ReceiptNumber}", bold: true, fontSize: "24");
                    AddParagraph(body, "");

                    // Дата прописью: "__" ________ 19__ г.
                    string day = act.ReceiptDate.Day.ToString("D2");
                    string month = GetMonthNameRu(act.ReceiptDate.Month);
                    string year = act.ReceiptDate.Year.ToString();
                    AddParagraph(body, $"Настоящий акт составлен \"{day}\" {month} {year} г.");

                    AddParagraph(body, "");
                    AddParagraph(body, act.ResponsiblePerson ?? "");
                    AddParagraph(body, "(фамилии, имена, отчества и должности лиц, участвующих");
                    AddParagraph(body, "в составлении акта)");
                    AddParagraph(body, "");
                    AddParagraph(body, $"о приеме в библиотеку {act.Comment ?? "(не указано)"}");
                    AddParagraph(body, "(вид документа и от кого получено)");
                    AddParagraph(body, "");

                    // Итоговая сумма
                    decimal totalSum = 0;
                    int totalCopies = 0;
                    foreach (var book in books)
                    {
                        totalSum += book.TotalPrice;
                        totalCopies += book.Quantity;
                    }

                    string totalRub = Math.Floor(totalSum).ToString("N0");
                    string totalKop = ((int)(totalSum * 100) % 100).ToString("D2");
                    string totalWords = NumberToWordsRubles((int)totalSum);

                    AddParagraph(body, $"в количестве {totalCopies} экземпляров на общую сумму {totalRub} руб. {totalKop} коп.");
                    AddParagraph(body, $"({totalWords})");
                    AddParagraph(body, "");
                    AddParagraph(body, "Список книг (или других документов) прилагается.");
                    AddParagraph(body, "");

                    // Подписи
                    AddParagraph(body, "Подписи ________________________________________ (руководителя)");
                    AddParagraph(body, "________________________________________ (сотрудника)");
                    AddParagraph(body, "________________________________________ (лица, сдавшего книги или другие документы)");
                    AddParagraph(body, "");

                    // Таблица
                    AddParagraph(body, $"Список книг или других документов к акту N {act.ReceiptNumber}*");

                    Table table = CreateBooksTable(books);
                    body.Append(table);

                    mainPart.Document.Save();
                }

                MessageBox.Show($"Акт успешно сформирован:\n{fileName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании акта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =============== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===============

        private static ReceiptData LoadReceiptData(int receiptId, string connectionString)
        {
            const string sql = @"
                SELECT ReceiptNumber, ReceiptDate, ResponsiblePerson, Comment 
                FROM ReceiptBooks 
                WHERE IDReceipt = @Id";

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", receiptId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ReceiptData
                            {
                                ReceiptNumber = reader["ReceiptNumber"].ToString(),
                                ReceiptDate = Convert.ToDateTime(reader["ReceiptDate"]),
                                ResponsiblePerson = reader["ResponsiblePerson"]?.ToString(),
                                Comment = reader["Comment"]?.ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }

        private static List<BookItem> LoadBooksByReceipt(int receiptId, string connectionString)
        {
            var list = new List<BookItem>();
            const string sql = @"
                SELECT 
                    b.TitleBook,
                    ISNULL(b.Price, 0) AS Price,
                    1 AS Quantity -- Если у вас нет поля количества, считаем 1 экз.
                FROM Books b
                WHERE b.IDReceip = @Id";

            using (var conn = new SqlConnection(connectionString))
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
                            int rub = (int)price;
                            int kop = (int)((price - rub) * 100 + 0.5m);

                            list.Add(new BookItem
                            {
                                Title = reader["TitleBook"].ToString(),
                                PriceRub = rub,
                                PriceKop = kop,
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                TotalPrice = price * Convert.ToInt32(reader["Quantity"])
                            });
                        }
                    }
                }
            }
            return list;
        }

        private static Table CreateBooksTable(List<BookItem> books)
        {
            Table table = new Table();

            // Свойства таблицы
            table.AppendChild(new TableProperties(
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 12 },
                    new BottomBorder() { Val = BorderValues.Single, Size = 12 },
                    new LeftBorder() { Val = BorderValues.Single, Size = 12 },
                    new RightBorder() { Val = BorderValues.Single, Size = 12 },
                    new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 12 },
                    new InsideVerticalBorder() { Val = BorderValues.Single, Size = 12 }
                )
            ));

            // Заголовки
            TableRow header = new TableRow();
            AddTableCell(header, "N п/п", bold: true);
            AddTableCell(header, "Автор, заглавие книги или вид документа", bold: true);
            AddTableCell(header, "Цена\nруб.", bold: true);
            AddTableCell(header, "коп.", bold: true);
            AddTableCell(header, "Кол-во экз.", bold: true);
            AddTableCell(header, "Сумма", bold: true);
            table.AppendChild(header);

            // Строки
            for (int i = 0; i < books.Count; i++)
            {
                var b = books[i];
                TableRow row = new TableRow();
                AddTableCell(row, (i + 1).ToString());
                AddTableCell(row, b.Title);
                AddTableCell(row, b.PriceRub.ToString());
                AddTableCell(row, b.PriceKop.ToString());
                AddTableCell(row, b.Quantity.ToString());
                AddTableCell(row, $"{b.TotalPrice:N2}");
                table.AppendChild(row);
            }

            return table;
        }

        private static void AddParagraph(Body body, string text, bool bold = false, string fontSize = "22")
        {
            var runProperties = new RunProperties();
            if (bold) runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = fontSize });

            var para = new Paragraph(
                new Run(
                    runProperties,
                    new Text(text)
                )
            );
            body.Append(para);
        }

        private static void AddTableCell(TableRow row, string text, bool bold = false)
        {
            var cell = new TableCell();
            var runProps = new RunProperties();
            if (bold) runProps.Append(new Bold());
            cell.Append(new Paragraph(new Run(runProps, new Text(text))));
            cell.Append(new TableCellProperties(new TableCellWidth() { Width = "1200" }));
            row.Append(cell);
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

        // Простая реализация прописи суммы (до 99 999 999 рублей)
        private static string NumberToWordsRubles(int number)
        {
            if (number == 0)
                return "ноль рублей 00 копеек";

            if (number < 0 || number > 99999999)
                return $"{number:N0} руб. 00 копеек"; // fallback для некорректных значений

            string[] units = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            // Специальные формы для тысяч
            string[] unitsForThousands = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };

            List<string> parts = new List<string>();

            // === Миллионы (до 99) ===
            if (number >= 1000000)
            {
                int millions = number / 1000000;
                number %= 1000000;

                string millionPart = ConvertHundreds(millions, units, teens, tens, hundreds);
                parts.Add(millionPart);

                // Склонение "миллион"
                string millionWord = GetPluralForm(millions, "миллион", "миллиона", "миллионов");
                parts.Add(millionWord);
            }

            // === Тысячи (до 999) ===
            if (number >= 1000)
            {
                int thousands = number / 1000;
                number %= 1000;

                string thousandPart = ConvertHundreds(thousands, unitsForThousands, teens, tens, hundreds);
                parts.Add(thousandPart);

                // Склонение "тысяча"
                string thousandWord = GetPluralForm(thousands, "тысяча", "тысячи", "тысяч");
                parts.Add(thousandWord);
            }

            // === Остаток (рубли, до 999) ===
            if (number > 0)
            {
                string rublePart = ConvertHundreds(number, units, teens, tens, hundreds);
                parts.Add(rublePart);
            }

            // Склонение "рубль"
            string rubleWord = GetPluralForm(number, "рубль", "рубля", "рублей");
            parts.Add(rubleWord);

            parts.Add("00 копеек");

            return string.Join(" ", parts).Trim();
        }

        // Вспомогательный метод: преобразует число 0–999 в слова
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

        // Вспомогательный метод: возвращает правильную форму слова
        private static string GetPluralForm(int n, string form1, string form2, string form5)
        {
            int lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 19)
                return form5;

            int last = n % 10;
            if (last == 1)
                return form1;
            if (last >= 2 && last <= 4)
                return form2;
            return form5;
        }

        // =============== DTO ===============
        private class ReceiptData
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
        }
    }
}