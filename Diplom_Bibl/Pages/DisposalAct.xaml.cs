using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    // Класс для причины списания
    public class ReasonDisposalBook
    {
        public int ID_Reason { get; set; }
        public string ReasonName { get; set; }
    }

    public partial class DisposalAct : Page
    {
        private readonly string _conn = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private readonly ObservableCollection<BookItem> _items = new ObservableCollection<BookItem>();
        private int _nextRowNum = 1;
        private int? _currentWriteOffId = null;

        public DisposalAct()
        {
            InitializeComponent();
            DgItems.ItemsSource = _items;
            LoadReasons();
            DpDate.SelectedDate = DateTime.Today;

            // Генерируем следующий номер акта
            string nextNumber = GetNextDisposalNumber();
            TbActNumber.Text = nextNumber;
        }

        private class BookItem
        {
            public int RowNum { get; set; }
            public string InventoryNumber { get; set; }
            public string Author { get; set; }
            public string Title { get; set; }
            public string PublishingHouse { get; set; }
            public int Year { get; set; }
            public int PriceRub { get; set; }
            public int PriceKop { get; set; }
            public int Quantity { get; set; }
            public decimal TotalPrice => (PriceRub + PriceKop / 100m) * Quantity;
            public string PlaceYear => $"{PublishingHouse}, {Year}";
        }

        private class BookStatusCheckResult
        {
            public bool CanBeDisposed { get; set; }
            public string Message { get; set; }
            public bool IsIssued { get; set; }
            public string IssuedInfo { get; set; }
        }

        private void LoadReasons()
        {
            try
            {
                const string sql = "SELECT ID_Reason, ReasonName FROM ReasonDisposalBook ORDER BY ReasonName";
                var reasons = new ObservableCollection<ReasonDisposalBook>();

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reasons.Add(new ReasonDisposalBook
                            {
                                ID_Reason = Convert.ToInt32(reader["ID_Reason"]),
                                ReasonName = reader["ReasonName"].ToString()
                            });
                        }
                    }
                }

                CbReason.ItemsSource = reasons;
                CbReason.DisplayMemberPath = "ReasonName";
                CbReason.SelectedValuePath = "ID_Reason";
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки причин: {ex.Message}");
            }
        }

        private void BtnFindBook_Click(object sender, RoutedEventArgs e)
        {
            string inv = TbSearchInv.Text.Trim();

            if (string.IsNullOrEmpty(inv))
            {
                 CustomMessageBox.Show("Введите инвентарный номер книги!");
                return;
            }

            // Проверка дубликатов в текущем акте
            if (_items.Any(item => item.InventoryNumber == inv))
            {
                 CustomMessageBox.Show($"Книга {inv} уже добавлена в акт.");
                TbSearchInv.Clear();
                return;
            }

            // Проверка статуса книги
            var bookCheckResult = CheckBookStatus(inv);
            if (!bookCheckResult.CanBeDisposed)
            {
                // Если книга выдана, показываем подробную информацию
                if (bookCheckResult.Message.Contains("находится на руках"))
                {
                    string issuedInfo = GetIssuedInfo(inv);
                     CustomMessageBox.Show($"{bookCheckResult.Message}\n\n{issuedInfo}",
                        "Невозможно списать", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
                else
                {
                     CustomMessageBox.Show(bookCheckResult.Message);
                }
                TbSearchInv.Clear();
                return;
            }

            var book = LoadBookByInventory(inv);
            if (book != null)
            {
                LbInventory.Text = book.InventoryNumber;
                LbTitle.Text = $"{book.Author} — {book.Title}";
                LbPubYear.Text = book.PlaceYear;
                LbPrice.Text = $"{book.PriceRub}.{book.PriceKop:D2} руб.";
                PnBookDetails.Visibility = Visibility.Visible;
                TbSearchInv.IsEnabled = false;
            }
        }

        private BookStatusCheckResult CheckBookStatus(string inventoryNumber)
        {
            const string sql = @"
                SELECT 
                    b.ID_Disposal,
                    s.Title_Status AS StatusName,
                    b.StatusBook,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL OR gi.GroupIssuanceId IS NOT NULL THEN 1 
                        ELSE 0 
                    END AS IsIssued
                FROM Books b
                LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                LEFT JOIN BookIssuance bi ON b.IDBook = bi.BookId AND bi.ReturnDate IS NULL
                LEFT JOIN GroupIssuanceItems git ON b.IDBook = git.BookId
                LEFT JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId AND gi.ReturnDate IS NULL
                WHERE b.InventaryNumber = @Inv";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Проверяем, выдана ли книга
                            bool isIssued = Convert.ToInt32(reader["IsIssued"]) == 1;

                            if (isIssued)
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга {inventoryNumber} находится на руках у читателя. Сначала необходимо вернуть книгу.",
                                    IsIssued = true
                                };
                            }

                            // Проверяем ID_Disposal
                            if (reader["ID_Disposal"] != DBNull.Value)
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга {inventoryNumber} уже списана."
                                };
                            }

                            string statusName = reader["StatusName"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(statusName) && statusName.ToLower().Contains("списана"))
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга имеет статус '{statusName}'."
                                };
                            }

                            return new BookStatusCheckResult { CanBeDisposed = true, Message = "" };
                        }
                    }
                }
            }
            return new BookStatusCheckResult { CanBeDisposed = false, Message = "Книга не найдена" };
        }

        private string GetIssuedInfo(string inventoryNumber)
        {
            const string sql = @"
                SELECT 
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN 
                            'читателю ' + r.LastName + ' ' + r.FirstName + ' ' + ISNULL(r.MiddleName, '')
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN 
                            'группе ' + g.GroupCode + ' (преп. ' + t.LastName + ' ' + t.FirstName + ')'
                        ELSE ''
                    END AS RecipientInfo,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN bi.IssuanceDate
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN gi.IssuanceDate
                        ELSE NULL
                    END AS IssueDate,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN bi.PlannedReturnDate
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN gi.PlannedReturnDate
                        ELSE NULL
                    END AS PlannedReturnDate
                FROM Books b
                LEFT JOIN BookIssuance bi ON b.IDBook = bi.BookId AND bi.ReturnDate IS NULL
                LEFT JOIN ReaderForm r ON bi.ReaderId = r.ReaderID
                LEFT JOIN GroupIssuanceItems git ON b.IDBook = git.BookId
                LEFT JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId AND gi.ReturnDate IS NULL
                LEFT JOIN Groups g ON gi.GroupId = g.GroupID
                LEFT JOIN ReaderForm t ON gi.TeacherId = t.ReaderID
                WHERE b.InventaryNumber = @Inv";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string recipientInfo = reader["RecipientInfo"]?.ToString() ?? "";
                            DateTime? issueDate = reader["IssueDate"] != DBNull.Value ? Convert.ToDateTime(reader["IssueDate"]) : (DateTime?)null;
                            DateTime? plannedReturnDate = reader["PlannedReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["PlannedReturnDate"]) : (DateTime?)null;

                            string result = recipientInfo;
                            if (issueDate.HasValue)
                            {
                                result += $"\nДата выдачи: {issueDate.Value:dd.MM.yyyy}";
                            }
                            if (plannedReturnDate.HasValue)
                            {
                                result += $"\nПлановая дата возврата: {plannedReturnDate.Value:dd.MM.yyyy}";
                                if (plannedReturnDate.Value < DateTime.Today)
                                {
                                    result += "\nВНИМАНИЕ: Просрочена!";
                                }
                            }
                            return result;
                        }
                    }
                }
            }
            return "";
        }

        private BookItem LoadBookByInventory(string inv)
        {
            const string sql = @"
                SELECT 
                    b.InventaryNumber AS Inv,
                    b.TitleBook AS Title,
                    ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS Author,
                    ISNULL(p.TitlePublishingHouse, '') AS Pub,
                    b.YearPublishing AS Year,
                    CAST(FLOOR(ISNULL(b.Price, 0)) AS int) AS PriceRub,
                    CAST((ISNULL(b.Price, 0) - FLOOR(ISNULL(b.Price, 0))) * 100 AS int) AS PriceKop,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL OR gi.GroupIssuanceId IS NOT NULL THEN 1 
                        ELSE 0 
                    END AS IsIssued
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                LEFT JOIN BookIssuance bi ON b.IDBook = bi.BookId AND bi.ReturnDate IS NULL
                LEFT JOIN GroupIssuanceItems git ON b.IDBook = git.BookId
                LEFT JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId AND gi.ReturnDate IS NULL
                WHERE b.InventaryNumber = @Inv";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", inv);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bool isIssued = Convert.ToInt32(reader["IsIssued"]) == 1;

                            if (isIssued)
                            {
                                // Книга выдана, показываем информацию
                                string issuedInfo = GetIssuedInfo(inv);
                                 CustomMessageBox.Show($"Книга {inv} находится на руках!\n\n{issuedInfo}",
                                    "Невозможно списать", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                                return null;
                            }

                            return new BookItem
                            {
                                InventoryNumber = reader["Inv"].ToString(),
                                Title = reader["Title"].ToString(),
                                Author = reader["Author"].ToString(),
                                PublishingHouse = reader["Pub"].ToString(),
                                Year = Convert.ToInt32(reader["Year"]),
                                PriceRub = Convert.ToInt32(reader["PriceRub"]),
                                PriceKop = Convert.ToInt32(reader["PriceKop"])
                            };
                        }
                    }
                }
            }
            return null;
        }

        private void BtnAddToAct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем, что все необходимые поля заполнены
                if (string.IsNullOrEmpty(LbInventory.Text) || LbInventory.Text == "не найдена")
                {
                     CustomMessageBox.Show("Сначала найдите книгу по инвентарному номеру.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                // Парсим цену
                string priceText = LbPrice.Text.Replace(" руб.", "").Trim();
                string[] priceParts = priceText.Split('.');
                int priceRub = int.Parse(priceParts[0]);
                int priceKop = priceParts.Length > 1 ? int.Parse(priceParts[1]) : 0;

                // Парсим год и издательство
                string pubYearText = LbPubYear.Text;
                string publishingHouse = "";
                int year = DateTime.Now.Year;

                if (!string.IsNullOrEmpty(pubYearText) && pubYearText.Contains(","))
                {
                    var parts = pubYearText.Split(',');
                    publishingHouse = parts[0].Trim();
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int parsedYear))
                    {
                        year = parsedYear;
                    }
                }

                // Парсим название и автора
                string titleText = LbTitle.Text;
                string author = "";
                string title = titleText;

                if (!string.IsNullOrEmpty(titleText) && titleText.Contains("—"))
                {
                    var parts = titleText.Split('—');
                    author = parts[0].Trim();
                    title = parts.Length > 1 ? parts[1].Trim() : titleText;
                }

                var item = new BookItem
                {
                    RowNum = _nextRowNum++,
                    InventoryNumber = LbInventory.Text,
                    Title = title,
                    Author = author,
                    PublishingHouse = publishingHouse,
                    Year = year,
                    PriceRub = priceRub,
                    PriceKop = priceKop,
                    Quantity = 1
                };

                _items.Add(item);

                // Показываем DataGrid и обновляем UI
                DgItems.Visibility = Visibility.Visible;
                DgItems.ItemsSource = _items;

                // Показываем заголовок таблицы и панель итогов
                TableHeader.Visibility = Visibility.Visible;
                TotalPanel.Visibility = Visibility.Visible;

                // Скрываем панель с деталями книги
                PnBookDetails.Visibility = Visibility.Collapsed;

                // Очищаем поле поиска
                TbSearchInv.Clear();
                TbSearchInv.IsEnabled = true;

                // Обновляем счетчик книг
                BooksCountLabel.Text = $"(всего: {_items.Count} книг)";

                // Обновляем итоговую сумму
                UpdateTotal();

                // Очищаем поля найденной книги
                ClearBookDetails();

                 CustomMessageBox.Show($"Книга '{title}' добавлена в акт списания.", "Успешно",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при добавлении книги: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ClearBookDetails()
        {
            LbInventory.Text = "";
            LbTitle.Text = "";
            LbPubYear.Text = "";
            LbPrice.Text = "";
        }

        private void UpdateTotal()
        {
            decimal total = 0;
            foreach (var i in _items)
            {
                total += i.TotalPrice;
            }
            TbTotal.Text = total.ToString("N2");
        }

        private void BtnGenerateAct_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                CustomMessageBox.Show("Добавьте книги в акт.");
                return;
            }

            if (CbReason.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите причину списания.");
                return;
            }

            var selectedReason = (ReasonDisposalBook)CbReason.SelectedItem;
            string actNumber = TbActNumber.Text.Trim();
            string participants = TbParticipants.Text.Trim();

            if (string.IsNullOrEmpty(actNumber) || string.IsNullOrEmpty(participants))
            {
                CustomMessageBox.Show("Заполните все поля.");
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем акт списания
                            int writeOffId;
                            using (var cmd = new SqlCommand(@"
                        INSERT INTO DisposalBooks (DisposalNumber, DisposalDate, Participants, Reason)
                        VALUES (@Number, @Date, @Participants, @Reason);
                        SELECT SCOPE_IDENTITY();", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Number", actNumber);
                                cmd.Parameters.AddWithValue("@Date", DpDate.SelectedDate ?? DateTime.Today);
                                cmd.Parameters.AddWithValue("@Participants", participants);
                                cmd.Parameters.AddWithValue("@Reason", selectedReason.ID_Reason);

                                writeOffId = Convert.ToInt32(cmd.ExecuteScalar());
                                _currentWriteOffId = writeOffId;
                            }

                            // 2. Получаем ID статуса "Списана"
                            int disposedStatusId = GetDisposedStatusId(conn, transaction);

                            // 3. Добавляем книги в DisposalItems и обновляем Books
                            foreach (var item in _items)
                            {
                                using (var cmd = new SqlCommand(@"
                            INSERT INTO DisposalItems (IDDisposal, InventoryNumber, TitleBook, PriceRub, PriceKop, Quantity, TotalPrice)
                            VALUES (@ID, @Inv, @Title, @Rub, @Kop, @Qty, @Total);
                            
                            UPDATE Books 
                            SET ID_Disposal = @ID, StatusBook = @StatusId
                            WHERE InventaryNumber = @Inv;", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", writeOffId);
                                    cmd.Parameters.AddWithValue("@Inv", item.InventoryNumber);
                                    cmd.Parameters.AddWithValue("@Title", item.Title);
                                    cmd.Parameters.AddWithValue("@Rub", item.PriceRub);
                                    cmd.Parameters.AddWithValue("@Kop", item.PriceKop);
                                    cmd.Parameters.AddWithValue("@Qty", item.Quantity);
                                    cmd.Parameters.AddWithValue("@Total", item.TotalPrice);
                                    cmd.Parameters.AddWithValue("@StatusId", disposedStatusId);

                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            // Генерируем PDF-документ
                            byte[] pdfBytes = Services.DisposalActPdfService.GenerateDisposalActPdf(writeOffId);

                            // Сохраняем PDF на рабочий стол
                            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            string fileName = $"Акт_списания_{actNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                            string fullPath = System.IO.Path.Combine(desktop, fileName);
                            System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                            CustomMessageBox.Show($"Акт списания успешно создан!\n\nPDF сохранён:\n{fullPath}", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearForm();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private int GetDisposedStatusId(SqlConnection conn, SqlTransaction transaction)
        {
            using (var cmd = new SqlCommand(
                "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'",
                conn, transaction))
            {
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 1;
            }
        }

        private void ClearForm()
        {
            TbActNumber.Text = GetNextDisposalNumber();
            DpDate.SelectedDate = DateTime.Today;
            TbParticipants.Clear();
            CbReason.SelectedIndex = -1;
            _items.Clear();
            _nextRowNum = 1;
            TbTotal.Text = "0.00";
            PnBookDetails.Visibility = Visibility.Collapsed;
            TbSearchInv.IsEnabled = true;
            DgItems.Visibility = Visibility.Collapsed;
            TableHeader.Visibility = Visibility.Collapsed;
            TotalPanel.Visibility = Visibility.Collapsed;
        }

        private string GetNextDisposalNumber()
        {
            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(MAX(CAST(SUBSTRING(DisposalNumber, 4, 4) AS INT)), 0) FROM DisposalBooks",
                    conn))
                {
                    int lastNum = Convert.ToInt32(cmd.ExecuteScalar());
                    return $"СП-{(lastNum + 1):D4}";
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}