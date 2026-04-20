using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public class BookListItem
    {
        public int IDBook { get; set; }
        public string InventoryNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public int Year { get; set; }
        public string Price { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string PublishingHouseName { get; set; } = "";
    }

    public partial class AddBook : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private int? _currentBookId = null;
        private ObservableCollection<BookListItem> _allBooks;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoadingBooks = false;
        private bool _isClosing = false;

        public AddBook()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
            SubscribeInventoryNumberEvents();
        }

        public AddBook(int bookId)
        {
            InitializeComponent();
            _currentBookId = bookId;
            SubscribeInventoryNumberEvents();
        }

        private void SubscribeInventoryNumberEvents()
        {
            TbInventoryNumber.PreviewTextInput += TbInventoryNumber_PreviewTextInput;
            TbInventoryNumber.PreviewKeyDown += TbInventoryNumber_PreviewKeyDown;
            TbInventoryNumber.TextChanged += TbInventoryNumber_TextChanged;
            TbInventoryNumber.LostFocus += TbInventoryNumber_LostFocus;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            _isClosing = false;
            ShowBooksLoading(true);
            await Task.Delay(10);
            await LoadDataAsync();

            // Автогенерация номера для новой книги
            if (!_currentBookId.HasValue && string.IsNullOrWhiteSpace(TbInventoryNumber.Text))
            {
                string nextNumber = await GetNextInventoryNumberAsync();
                TbInventoryNumber.Text = nextNumber;
            }
        }

        private void ShowBooksLoading(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                if (BooksLoadingPanel != null)
                    BooksLoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (LvBooks != null)
                    LvBooks.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await Task.Run(() => LoadAuthors());
                await Task.Run(() => LoadViewBooks());
                await Task.Run(() => LoadPublishers());
                await Task.Run(() => LoadGenres());
                await Task.Run(() => LoadStatuses());
                await Task.Run(() => LoadReceipts());

                _cancellationTokenSource = new CancellationTokenSource();
                await LoadBooksToListAsync(_cancellationTokenSource.Token);

                if (_currentBookId.HasValue)
                {
                    await LoadBookDataAsync(_currentBookId.Value);
                    BtnSaveNew.Visibility = Visibility.Collapsed;
                    BtnSaveChanges.Visibility = Visibility.Visible;
                    BtnDelete.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Загрузка данных отменена");
            }
            catch (Exception ex)
            {
                if (!_isClosing)
                    CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                ShowBooksLoading(false);
            }
        }

        // ==================== АВТОГЕНЕРАЦИЯ ИНВЕНТАРНОГО НОМЕРА ====================
        private async Task<string> GetNextInventoryNumberAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    const string query = "SELECT MAX(CAST(InventaryNumber AS INT)) FROM Books WHERE ISNUMERIC(InventaryNumber) = 1";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            var result = cmd.ExecuteScalar();
                            int maxNumber = (result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                            int nextNumber = maxNumber + 1;
                            return nextNumber.ToString("D6");
                        }
                    }
                }
                catch
                {
                    return DateTime.Now.Ticks.ToString().Substring(0, 6);
                }
            });
        }

        // ==================== МАСКА ВВОДА ДЛЯ ИНВЕНТАРНОГО НОМЕРА ====================
        private void TbInventoryNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void TbInventoryNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Tab ||
                e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Home ||
                e.Key == Key.End || e.Key == Key.Left || e.Key == Key.Right)
                return;
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void TbInventoryNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            if (textBox.Text.Length > 6)
            {
                textBox.Text = textBox.Text.Substring(0, 6);
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void TbInventoryNumber_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!_currentBookId.HasValue && int.TryParse(text, out int number))
            {
                textBox.Text = number.ToString("D6");
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        // ==================== МАСКА ВВОДА ДЛЯ ГОДА ====================
        private void TbYear_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !IsTextNumeric(e.Text);
        private void TbYear_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextNumeric(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void TbYear_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            string input = tb.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            if (int.TryParse(input, out int year))
            {
                int currentYear = DateTime.Now.Year;
                if (year > currentYear)
                {
                    CustomMessageBox.Show($"Год издания не может быть больше текущего ({currentYear}).", "Некорректный год", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    tb.Text = currentYear.ToString();
                    tb.SelectAll();
                    tb.Focus();
                }
                else if (year < 1000)
                {
                    CustomMessageBox.Show("Год издания должен быть не меньше 1000.", "Некорректный год", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    tb.Clear();
                    tb.Focus();
                }
            }
            else
            {
                CustomMessageBox.Show("Введите корректный год (только цифры).", "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                tb.Clear();
                tb.Focus();
            }
        }

        // ==================== МАСКА ВВОДА ДЛЯ ЦЕНЫ ====================
        private void TbPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string text = e.Text;
            TextBox textBox = sender as TextBox;
            string currentText = textBox?.Text ?? "";
            string newText = currentText + text;

            // Разрешаем только цифры, запятую и точку
            if (!Regex.IsMatch(text, @"^[\d,.]$"))
            {
                e.Handled = true;
                return;
            }

            // Проверяем, чтобы не было больше одного разделителя
            if ((text == "," || text == ".") && (currentText.Contains(",") || currentText.Contains(".")))
            {
                e.Handled = true;
                return;
            }

            // Проверяем, чтобы после разделителя было не более 2 цифр
            int separatorIndex = currentText.IndexOfAny(new[] { ',', '.' });
            if (separatorIndex >= 0)
            {
                string afterSeparator = currentText.Substring(separatorIndex + 1);
                if (afterSeparator.Length >= 2 && text != "," && text != ".")
                {
                    e.Handled = true;
                    return;
                }
            }
        }
        private void TbPrice_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, @"^\d+([,.]\d{0,2})?$"))
                    e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void TbPrice_LostFocus(object sender, RoutedEventArgs e)
        {
            string input = TbPrice.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                TbPrice.Text = "";
                return;
            }

            // Приводим разделитель к запятой (для единообразия)
            string normalized = input.Replace('.', ',');

            // Удаляем всё, кроме цифр и запятой
            normalized = Regex.Replace(normalized, @"[^\d,]", "");

            // Оставляем только первую запятую (удаляем лишние)
            int firstComma = normalized.IndexOf(',');
            if (firstComma >= 0)
            {
                string before = normalized.Substring(0, firstComma);
                string after = normalized.Substring(firstComma + 1).Replace(",", "");
                normalized = before + "," + after;
            }

            // Удаляем запятую в конце, если она есть
            if (normalized.EndsWith(","))
                normalized = normalized.TrimEnd(',');

            // Парсим число с использованием культуры, где разделитель — запятая
            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("ru-RU"), out decimal value))
            {
                // Определяем, была ли в исходном вводе дробная часть
                bool hadFraction = normalized.Contains(",");

                if (hadFraction)
                {
                    // Если есть дробная часть, показываем с двумя знаками (дополняем нулями)
                    string[] parts = normalized.Split(',');
                    string fraction = parts[1].PadRight(2, '0');
                    TbPrice.Text = $"{parts[0]},{fraction}";
                }
                else
                {
                    // Если целое число — показываем без копеек
                    TbPrice.Text = value.ToString("0", new CultureInfo("ru-RU"));
                }
            }
            else
            {
                CustomMessageBox.Show("Введите корректную цену (например: 700, 700,50 или 700.50)", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbPrice.Clear();
            }
        }
        // ==================== ЗАГРУЗКА ДАННЫХ ====================
        private async Task LoadBooksToListAsync(CancellationToken cancellationToken)
        {
            if (_isLoadingBooks) return;
            _isLoadingBooks = true;

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    const string query = @"
                        SELECT TOP 100
                            b.IDBook,
                            b.InventaryNumber AS InventoryNumber,
                            b.TitleBook AS Title,
                            b.YearPublishing AS Year,
                            b.Price,
                            ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                            ISNULL(p.TitlePublishingHouse, '') AS PublishingHouseName
                        FROM Books b
                        LEFT JOIN Authors a ON b.Author = a.ID_Autor
                        LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                        ORDER BY b.TitleBook";

                    using (var connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();
                        cancellationToken.ThrowIfCancellationRequested();

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = command.ExecuteReader())
                        {
                            var books = new ObservableCollection<BookListItem>();
                            while (reader.Read())
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                books.Add(new BookListItem
                                {
                                    IDBook = Convert.ToInt32(reader["IDBook"]),
                                    InventoryNumber = reader["InventoryNumber"]?.ToString() ?? "",
                                    Title = reader["Title"]?.ToString() ?? "",
                                    Year = Convert.ToInt32(reader["Year"]),
                                    Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]).ToString("F2") : "0,00",
                                    AuthorName = reader["AuthorName"]?.ToString() ?? "",
                                    PublishingHouseName = reader["PublishingHouseName"]?.ToString() ?? ""
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _allBooks = books;
                                LvBooks.ItemsSource = _allBooks;
                                ApplyBookFilter("");
                            });
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_isClosing)
                    Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
            finally
            {
                _isLoadingBooks = false;
            }
        }

        private void LoadAuthors()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Autor"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Autor"]);
                            items.Add(new { ID = id, DisplayAuthors = $"{reader["SurnameAuthor"]} {reader["NameAuthor"]} {reader["PatronymicAuthor"]}" });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbAuthor.ItemsSource = items;
                            CbAuthor.DisplayMemberPath = "DisplayAuthors";
                            CbAuthor.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private void LoadViewBooks()
        {
            try
            {
                const string sql = "SELECT ID_View_Book, View_Book FROM ViewBook ORDER BY View_Book";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["ID_View_Book"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_View_Book"]);
                            items.Add(new { ID = id, DisplayViewBook = reader["View_Book"].ToString() });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbViewBook.ItemsSource = items;
                            CbViewBook.DisplayMemberPath = "DisplayViewBook";
                            CbViewBook.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки видов книг: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private void LoadPublishers()
        {
            try
            {
                const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["IDPublishingHouse"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDPublishingHouse"]);
                            items.Add(new { ID = id, DisplayPH = reader["TitlePublishingHouse"].ToString() });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbPublisher.ItemsSource = items;
                            CbPublisher.DisplayMemberPath = "DisplayPH";
                            CbPublisher.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private void LoadGenres()
        {
            try
            {
                const string sql = "SELECT ID_Gener, TitleGener FROM GenersBook ORDER BY TitleGener";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Gener"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Gener"]);
                            items.Add(new { ID = id, DisplayGen = reader["TitleGener"].ToString() });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbGenre.ItemsSource = items;
                            CbGenre.DisplayMemberPath = "DisplayGen";
                            CbGenre.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private void LoadStatuses()
        {
            try
            {
                const string sql = "SELECT ID_Status, Title_Status FROM StatusBook ORDER BY Title_Status";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Status"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Status"]);
                            items.Add(new { ID = id, DisplayStatus = reader["Title_Status"].ToString() });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbStatus.ItemsSource = items;
                            CbStatus.DisplayMemberPath = "DisplayStatus";
                            CbStatus.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки статусов: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private void LoadReceipts()
        {
            try
            {
                const string sql = "SELECT IDReceipt, ReceiptNumber, ReceiptDate FROM ReceiptBooks ORDER BY ReceiptNumber";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<object>();
                        while (reader.Read())
                        {
                            int id = reader["IDReceipt"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDReceipt"]);
                            string date = reader["ReceiptDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReceiptDate"]).ToString("dd.MM.yyyy") : "";
                            items.Add(new { ID = id, DisplayReceipt = $"{reader["ReceiptNumber"]} от {date}" });
                        }
                        Dispatcher.Invoke(() =>
                        {
                            CbReceipt.ItemsSource = items;
                            CbReceipt.DisplayMemberPath = "DisplayReceipt";
                            CbReceipt.SelectedValuePath = "ID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки актов поступления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private async Task LoadBookDataAsync(int bookId)
        {
            await Task.Run(() =>
            {
                try
                {
                    const string query = @"
                        SELECT 
                            b.IDBook, b.InventaryNumber, b.TitleBook, b.Author, b.PublishingHouse,
                            b.YearPublishing, b.Gener, b.StatusBook, b.ID_View_Book, b.Price,
                            b.Anotation, b.IDReceip
                        FROM Books b WHERE b.IDBook = @BookId";

                    using (var connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@BookId", bookId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var inventaryNumber = reader["InventaryNumber"]?.ToString() ?? "";
                                    var titleBook = reader["TitleBook"]?.ToString() ?? "";
                                    var author = reader["Author"] != DBNull.Value ? Convert.ToInt32(reader["Author"]) : 0;
                                    var publishingHouse = reader["PublishingHouse"] != DBNull.Value ? Convert.ToInt32(reader["PublishingHouse"]) : (int?)null;
                                    var yearPublishing = Convert.ToInt32(reader["YearPublishing"]);
                                    var gener = reader["Gener"] != DBNull.Value ? Convert.ToInt32(reader["Gener"]) : (int?)null;
                                    var statusBook = reader["StatusBook"] != DBNull.Value ? Convert.ToInt32(reader["StatusBook"]) : (int?)null;
                                    var idViewBook = Convert.ToInt32(reader["ID_View_Book"]);
                                    var price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0;
                                    var anotation = reader["Anotation"]?.ToString() ?? "";
                                    var idReceip = reader["IDReceip"] != DBNull.Value ? Convert.ToInt32(reader["IDReceip"]) : (int?)null;

                                    Dispatcher.Invoke(() =>
                                    {
                                        TbInventoryNumber.Text = inventaryNumber;
                                        TbTitle.Text = titleBook;
                                        TbYear.Text = yearPublishing.ToString();
                                        TbAnnotation.Text = anotation;
                                        TbPrice.Text = price.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
                                        CbAuthor.SelectedValue = author;
                                        CbViewBook.SelectedValue = idViewBook;
                                        if (publishingHouse.HasValue) CbPublisher.SelectedValue = publishingHouse.Value;
                                        if (gener.HasValue) CbGenre.SelectedValue = gener.Value;
                                        if (statusBook.HasValue) CbStatus.SelectedValue = statusBook.Value;
                                        if (idReceip.HasValue) CbReceipt.SelectedValue = idReceip.Value;
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => CustomMessageBox.Show($"Ошибка загрузки данных книги: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
                }
            });
        }

        private void ApplyBookFilter(string searchTerm)
        {
            if (_allBooks == null) return;
            var filtered = _allBooks.Where(book =>
            {
                string title = book.Title ?? "";
                string inv = book.InventoryNumber ?? "";
                string author = book.AuthorName ?? "";
                string publisher = book.PublishingHouseName ?? "";
                return string.IsNullOrEmpty(searchTerm) ||
                       title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       inv.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       author.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       publisher.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();
            Dispatcher.Invoke(() => LvBooks.ItemsSource = new ObservableCollection<BookListItem>(filtered));
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyBookFilter(SearchBox.Text.Trim());

        private async void LvBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvBooks.SelectedItem is BookListItem selectedItem)
            {
                _currentBookId = selectedItem.IDBook;
                await LoadBookDataAsync(_currentBookId.Value);
                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            _isClosing = true;
            _cancellationTokenSource?.Cancel();
            NavigationService?.GoBack();
        }

        private async void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbInventoryNumber.Text) || string.IsNullOrWhiteSpace(TbTitle.Text) ||
                CbAuthor.SelectedValue == null || CbViewBook.SelectedValue == null || !int.TryParse(TbYear.Text, out int year))
            {
                CustomMessageBox.Show("Заполните обязательные поля (*).", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            decimal price = 0;
            string priceText = TbPrice.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(priceText))
            {
                priceText = priceText.Replace(',', '.');
                if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    CustomMessageBox.Show("Укажите корректную цену.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }
            }

            string inventoryNumber = TbInventoryNumber.Text.Trim();
            string title = TbTitle.Text.Trim();
            int authorId = (int)CbAuthor.SelectedValue;
            int viewBookId = (int)CbViewBook.SelectedValue;
            object publisherId = CbPublisher.SelectedValue ?? DBNull.Value;
            object genreId = CbGenre.SelectedValue ?? DBNull.Value;
            object statusId = CbStatus.SelectedValue ?? DBNull.Value;
            string annotation = TbAnnotation.Text.Trim();
            object receiptId = CbReceipt.SelectedValue ?? DBNull.Value;

            try
            {
                await Task.Run(() =>
                {
                    const string sql = @"
                        INSERT INTO Books (InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
                            Gener, StatusBook, Anotation, IDReceip, ID_View_Book, Price)
                        VALUES (@Inv, @Title, @Author, @Pub, @Year, @Genre, @Status, @Annot, @Receipt, @View, @Price)";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                            cmd.Parameters.AddWithValue("@Title", title);
                            cmd.Parameters.AddWithValue("@Author", authorId);
                            cmd.Parameters.AddWithValue("@Pub", publisherId);
                            cmd.Parameters.AddWithValue("@Year", year);
                            cmd.Parameters.AddWithValue("@Genre", genreId);
                            cmd.Parameters.AddWithValue("@Status", statusId);
                            cmd.Parameters.AddWithValue("@Annot", string.IsNullOrEmpty(annotation) ? DBNull.Value : (object)annotation);
                            cmd.Parameters.AddWithValue("@Receipt", receiptId);
                            cmd.Parameters.AddWithValue("@View", viewBookId);
                            cmd.Parameters.AddWithValue("@Price", price);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
                CustomMessageBox.Show("Книга сохранена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                await Dispatcher.InvokeAsync(() => ClearForm());
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadBooksToListAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => CustomMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private async void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                CustomMessageBox.Show("Нет выбранной книги для редактирования.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(TbInventoryNumber.Text) || string.IsNullOrWhiteSpace(TbTitle.Text) ||
                CbAuthor.SelectedValue == null || CbViewBook.SelectedValue == null || !int.TryParse(TbYear.Text, out int year))
            {
                CustomMessageBox.Show("Заполните обязательные поля (*).", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            decimal price = 0;
            string priceText = TbPrice.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(priceText))
            {
                priceText = priceText.Replace(',', '.');
                if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    CustomMessageBox.Show("Укажите корректную цену.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }
            }

            int bookId = _currentBookId.Value;
            string inventoryNumber = TbInventoryNumber.Text.Trim();
            string title = TbTitle.Text.Trim();
            int authorId = (int)CbAuthor.SelectedValue;
            int viewBookId = (int)CbViewBook.SelectedValue;
            object publisherId = CbPublisher.SelectedValue ?? DBNull.Value;
            object genreId = CbGenre.SelectedValue ?? DBNull.Value;
            object statusId = CbStatus.SelectedValue ?? DBNull.Value;
            string annotation = TbAnnotation.Text.Trim();
            object receiptId = CbReceipt.SelectedValue ?? DBNull.Value;

            try
            {
                await Task.Run(() =>
                {
                    const string updateQuery = @"
                        UPDATE Books SET InventaryNumber=@Inv, TitleBook=@Title, Author=@Author, PublishingHouse=@Pub,
                            YearPublishing=@Year, Gener=@Genre, StatusBook=@Status, Anotation=@Annot,
                            IDReceip=@Receipt, ID_View_Book=@View, Price=@Price WHERE IDBook=@Id";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                            cmd.Parameters.AddWithValue("@Title", title);
                            cmd.Parameters.AddWithValue("@Author", authorId);
                            cmd.Parameters.AddWithValue("@Pub", publisherId);
                            cmd.Parameters.AddWithValue("@Year", year);
                            cmd.Parameters.AddWithValue("@Genre", genreId);
                            cmd.Parameters.AddWithValue("@Status", statusId);
                            cmd.Parameters.AddWithValue("@Annot", string.IsNullOrEmpty(annotation) ? DBNull.Value : (object)annotation);
                            cmd.Parameters.AddWithValue("@Receipt", receiptId);
                            cmd.Parameters.AddWithValue("@View", viewBookId);
                            cmd.Parameters.AddWithValue("@Price", price);
                            cmd.Parameters.AddWithValue("@Id", bookId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
                CustomMessageBox.Show("Книга успешно обновлена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                await Dispatcher.InvokeAsync(() => ClearForm());
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadBooksToListAsync(_cancellationTokenSource.Token);
               
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => CustomMessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error));
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                CustomMessageBox.Show("Нет выбранной книги для удаления.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }
            if (CustomMessageBox.Show("Вы уверены, что хотите удалить эту книгу?", "Подтверждение удаления",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question) != CustomMessageBoxResult.Yes) return;

            try
            {
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand("DELETE FROM Books WHERE IDBook = @Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", _currentBookId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
                CustomMessageBox.Show("Книга удалена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                ClearForm();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadBooksToListAsync(_cancellationTokenSource.Token);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                CustomMessageBox.Show("Невозможно удалить книгу: на неё ссылаются другие записи.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void ClearForm()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TbInventoryNumber.Clear();
                TbTitle.Clear();
                TbYear.Clear();
                TbAnnotation.Clear();
                TbPrice.Clear();
                CbAuthor.SelectedIndex = -1;
                CbViewBook.SelectedIndex = -1;
                CbPublisher.SelectedIndex = -1;
                CbGenre.SelectedIndex = -1;
                CbStatus.SelectedIndex = -1;
                CbReceipt.SelectedIndex = -1;
                _currentBookId = null;
                BtnSaveNew.Visibility = Visibility.Visible;
                BtnSaveChanges.Visibility = Visibility.Collapsed;
                BtnDelete.Visibility = Visibility.Collapsed;
            });
            string nextNumber = await GetNextInventoryNumberAsync();
            TbInventoryNumber.Text = nextNumber;
        }

        private void BtnAddAuthor_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddAuthor());
        private void BtnAddViewBook_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddViewBook());
        private void BtnAddPublisher_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddPublishingHouse());
        private void BtnAddGenre_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddGener());
        private void BtnAddStatus_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddStatusBook());
        private void BtnAddReceipt_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AdmissionBook());

        private void BtnGenerateAct_Click(object sender, RoutedEventArgs e)
        {
            if (CbReceipt.SelectedValue is int receiptId)
            {
                try
                {
                    byte[] pdfBytes = Services.ReceiptActPdfService.GenerateReceiptActPdf(receiptId);
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"Акт_поступления_{receiptId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                    string fullPath = System.IO.Path.Combine(desktop, fileName);
                    System.IO.File.WriteAllBytes(fullPath, pdfBytes);
                    CustomMessageBox.Show($"PDF сохранён:\n{fullPath}", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка генерации PDF: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
            else
            {
                CustomMessageBox.Show("Выберите акт для формирования.", "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
        }

        private bool IsTextNumeric(string text) => Regex.IsMatch(text, @"^\d+$");
    }
}