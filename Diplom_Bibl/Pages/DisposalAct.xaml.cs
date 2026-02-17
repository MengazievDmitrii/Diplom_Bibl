using DocumentFormat.OpenXml.VariantTypes;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace Diplom_Bibl.Pages
{
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

            // Генерируем следующий номер акта (СП-0001, СП-0002...)
            string nextNumber = GetNextDisposalNumber();
            TbActNumber.Text = nextNumber;
        }

        private class BookItem
        {
            public int RowNum { get; set; }
            public string InventoryNumber { get; set; }
            public string Author { get; set; }        // ← ДОБАВЛЕНО
            public string Title { get; set; }
            public string PublishingHouse { get; set; }
            public int Year { get; set; }
            public int PriceRub { get; set; }
            public int PriceKop { get; set; }
            public int Quantity { get; set; }
            public decimal TotalPrice => (PriceRub + PriceKop / 100m) * Quantity;
            public string PlaceYear => $"{PublishingHouse}, {Year}";
        }

        private string GenerateDisposalNumber(int nextId)
        {
            return $"СП-{nextId:D4}"; // СП = Списание, D4 → 0001, 0002, ...
        }
        private void LoadReasons()
        {
            const string sql = "SELECT ID_Reason, ReasonName FROM  ReasonDisposalBook ORDER BY ReasonName";

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
        }
        private void BtnFindBook_Click(object sender, RoutedEventArgs e)
        {
            string inv = TbSearchInv.Text.Trim();
            if (string.IsNullOrEmpty(inv)) return;

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
            else
            {
                MessageBox.Show("Книга не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                    CAST(b.Price AS int) AS PriceRub,
                    CAST((b.Price - FLOOR(b.Price)) * 100 AS int) AS PriceKop
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
            DgItems.Visibility = Visibility.Visible;
            PnBookDetails.Visibility = Visibility.Collapsed;
            TbSearchInv.Clear();
            TbSearchInv.IsEnabled = true;
            UpdateTotal();
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
                MessageBox.Show("Добавьте хотя бы одну книгу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actNumber = TbActNumber.Text?.Trim();
            string participants = TbParticipants.Text?.Trim();
            if (CbReason.SelectedItem == null || !(CbReason.SelectedItem is ReasonDisposalBook))
            {
                MessageBox.Show("Выберите основание списания.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedReason = (ReasonDisposalBook)CbReason.SelectedItem;
            string reason = selectedReason.ReasonName;
            int reasonId = selectedReason.ID_Reason; // если нужно сохранить ID в БД

            if (string.IsNullOrEmpty(actNumber) || string.IsNullOrEmpty(participants) || string.IsNullOrEmpty(reason))
            {
                MessageBox.Show("Заполните номер акта, участников и основание.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int writeOffId;
                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                INSERT INTO DisposalBooks (DisposalNumber, DisposalDate, Participants, Reason)
                VALUES (@Number, @Date, @Participants, @Reason);
                SELECT SCOPE_IDENTITY();", conn))
                    {
                        cmd.Parameters.AddWithValue("@Number", actNumber);
                        cmd.Parameters.AddWithValue("@Date", DpDate.SelectedDate ?? DateTime.Today);
                        cmd.Parameters.AddWithValue("@Participants", participants);
                        cmd.Parameters.AddWithValue("@Reason", reasonId);
                        writeOffId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    foreach (var item in _items)
                    {
                        using (var cmd2 = new SqlCommand(@"
                    INSERT INTO DisposalItems (IDDisposal, InventoryNumber, TitleBook, PriceRub, PriceKop, Quantity, TotalPrice)
                    VALUES (@ID_Disposal, @Inv, @Title, @Rub, @Kop, @Qty, @Total);", conn))
                        {
                            cmd2.Parameters.AddWithValue("@ID_Disposal", writeOffId);
                            cmd2.Parameters.AddWithValue("@Inv", item.InventoryNumber);
                            cmd2.Parameters.AddWithValue("@Title", item.Title);
                            cmd2.Parameters.AddWithValue("@Rub", item.PriceRub);
                            cmd2.Parameters.AddWithValue("@Kop", item.PriceKop);
                            cmd2.Parameters.AddWithValue("@Qty", item.Quantity);
                            cmd2.Parameters.AddWithValue("@Total", item.TotalPrice);
                            cmd2.ExecuteNonQuery();
                        }

                        using (var cmd3 = new SqlCommand(@"
                    UPDATE Books SET ID_Disposal = @ID_Disposal WHERE InventaryNumber = @Inv;", conn))
                        {
                            cmd3.Parameters.AddWithValue("@ID_Disposal", writeOffId);
                            cmd3.Parameters.AddWithValue("@Inv", item.InventoryNumber);
                            cmd3.ExecuteNonQuery();
                        }
                    }
                }

                // === ГЕНЕРАЦИЯ PDF ===
                try
                {
                    byte[] pdfBytes = Diplom_Bibl.Services.DisposalActPdfService.GenerateDisposalActPdf(writeOffId);

                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"Акт_списания_{actNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                    string fullPath = System.IO.Path.Combine(desktop, fileName);

                    System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                    MessageBox.Show($"Акт списания '{actNumber}' успешно создан.\nPDF сохранён:\n{fullPath}",
                                    "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception pdfEx)
                {
                    MessageBox.Show($"Акт создан, но PDF не сгенерирован:\n{pdfEx.Message}",
                                    "Частичный успех", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании акта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            TbActNumber.Clear();
            DpDate.SelectedDate = DateTime.Today;
            TbParticipants.Clear();
            CbReason.SelectedItem = -1;
            _items.Clear();
            DgItems.Visibility = Visibility.Collapsed;
            PnBookDetails.Visibility = Visibility.Collapsed;
            TbSearchInv.Clear();
            TbSearchInv.IsEnabled = true;
            _nextRowNum = 1;
            TbTotal.Text = "0.00";
        }
        private string GetNextDisposalNumber()
        {
            const string sql = "SELECT ISNULL(MAX(CAST(SUBSTRING(DisposalNumber, 4, 4) AS INT)), 0) FROM DisposalBooks WHERE DisposalNumber LIKE 'СП-%'";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
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