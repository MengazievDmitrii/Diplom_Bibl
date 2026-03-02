using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
                MessageBox.Show($"Ошибка загрузки причин: {ex.Message}");
            }
        }

        private void BtnFindBook_Click(object sender, RoutedEventArgs e)
        {
            string inv = TbSearchInv.Text.Trim();

            if (string.IsNullOrEmpty(inv))
            {
                MessageBox.Show("Введите инвентарный номер книги!");
                return;
            }

            // Проверка дубликатов в текущем акте
            if (_items.Any(item => item.InventoryNumber == inv))
            {
                MessageBox.Show($"Книга {inv} уже добавлена в акт.");
                TbSearchInv.Clear();
                return;
            }

            // Проверка статуса книги
            var bookCheckResult = CheckBookStatus(inv);
            if (!bookCheckResult.CanBeDisposed)
            {
                MessageBox.Show(bookCheckResult.Message);
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

        private class BookStatusCheckResult
        {
            public bool CanBeDisposed { get; set; }
            public string Message { get; set; }
        }

        private BookStatusCheckResult CheckBookStatus(string inventoryNumber)
        {
            const string sql = @"
                SELECT 
                    b.ID_Disposal,
                    s.Title_Status AS StatusName
                FROM Books b
                LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
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

                            return new BookStatusCheckResult { CanBeDisposed = true };
                        }
                    }
                }
            }
            return new BookStatusCheckResult { CanBeDisposed = false, Message = "Книга не найдена" };
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
                    CAST((ISNULL(b.Price, 0) - FLOOR(ISNULL(b.Price, 0))) * 100 AS int) AS PriceKop
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
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
                var item = new BookItem
                {
                    RowNum = _nextRowNum++,
                    InventoryNumber = LbInventory.Text,
                    Title = LbTitle.Text.Split('—')[1].Trim(),
                    PublishingHouse = LbPubYear.Text.Split(',')[0].Trim(),
                    Year = int.Parse(LbPubYear.Text.Split(',')[1].Trim()),
                    PriceRub = int.Parse(LbPrice.Text.Split('.')[0]),
                    PriceKop = int.Parse(LbPrice.Text.Split('.')[1].Substring(0, 2)),
                    Quantity = 1
                };

                _items.Add(item);
                UpdateTotal();
                PnBookDetails.Visibility = Visibility.Collapsed;
                TbSearchInv.IsEnabled = true;
                TbSearchInv.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void UpdateTotal()
        {
            decimal total = _items.Sum(i => i.TotalPrice);
            TbTotal.Text = total.ToString("N2");
        }

        private void BtnGenerateAct_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Добавьте книги в акт.");
                return;
            }

            if (CbReason.SelectedItem == null)
            {
                MessageBox.Show("Выберите причину списания.");
                return;
            }

            var selectedReason = (ReasonDisposalBook)CbReason.SelectedItem;
            string actNumber = TbActNumber.Text.Trim();
            string participants = TbParticipants.Text.Trim();

            if (string.IsNullOrEmpty(actNumber) || string.IsNullOrEmpty(participants))
            {
                MessageBox.Show("Заполните все поля.");
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
                            MessageBox.Show("Акт списания создан!");
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
                MessageBox.Show($"Ошибка: {ex.Message}");
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