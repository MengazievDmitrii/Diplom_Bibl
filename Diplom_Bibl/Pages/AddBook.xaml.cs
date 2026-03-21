using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public partial class AddBook : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private int? _currentBookId = null;
        private ObservableCollection<dynamic> _allBooks;

        public AddBook()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
        }

        public AddBook(int bookId)
        {
            InitializeComponent();
            _currentBookId = bookId;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Загружаем данные с задержкой, чтобы UI успел отрисоваться
            await Task.Delay(10);
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Загружаем справочники последовательно, чтобы не создавать много потоков
                LoadAuthors();
                LoadViewBooks();
                LoadPublishers();
                LoadGenres();
                LoadStatuses();
                LoadReceipts();

                // Загружаем список книг
                await LoadBooksAsync();

                if (_currentBookId.HasValue)
                {
                    await LoadBookDataAsync(_currentBookId.Value);

                    BtnSaveNew.Visibility = Visibility.Collapsed;
                    BtnSaveChanges.Visibility = Visibility.Visible;
                    BtnDelete.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int ID_Autor = reader["ID_Autor"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Autor"]);
                            string SurnameAuthor = reader["SurnameAuthor"]?.ToString() ?? "";
                            string NameAuthor = reader["NameAuthor"]?.ToString() ?? "";
                            string PatronymicAuthor = reader["PatronymicAuthor"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_Autor,
                                DisplayAuthors = $"{SurnameAuthor} {NameAuthor} {PatronymicAuthor}"
                            });
                        }

                        CbAuthor.ItemsSource = items;
                        CbAuthor.DisplayMemberPath = "DisplayAuthors";
                        CbAuthor.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int ID_View_Book = reader["ID_View_Book"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_View_Book"]);
                            string View_Book = reader["View_Book"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_View_Book,
                                DisplayViewBook = $"{View_Book}"
                            });
                        }

                        CbViewBook.ItemsSource = items;
                        CbViewBook.DisplayMemberPath = "DisplayViewBook";
                        CbViewBook.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки видов книг: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int IDPublishingHouse = reader["IDPublishingHouse"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDPublishingHouse"]);
                            string TitlePublishingHouse = reader["TitlePublishingHouse"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = IDPublishingHouse,
                                DisplayPH = $"{TitlePublishingHouse}"
                            });
                        }

                        CbPublisher.ItemsSource = items;
                        CbPublisher.DisplayMemberPath = "DisplayPH";
                        CbPublisher.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int ID_Gener = reader["ID_Gener"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Gener"]);
                            string TitleGener = reader["TitleGener"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_Gener,
                                DisplayGen = $"{TitleGener}"
                            });
                        }

                        CbGenre.ItemsSource = items;
                        CbGenre.DisplayMemberPath = "DisplayGen";
                        CbGenre.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int ID_Status = reader["ID_Status"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Status"]);
                            string Title_Status = reader["Title_Status"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_Status,
                                DisplayStatus = $"{Title_Status}"
                            });
                        }

                        CbStatus.ItemsSource = items;
                        CbStatus.DisplayMemberPath = "DisplayStatus";
                        CbStatus.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статусов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            int IDReceipt = reader["IDReceipt"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDReceipt"]);
                            string ReceiptNumber = reader["ReceiptNumber"]?.ToString() ?? "";
                            string ReceiptDate = reader["ReceiptDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ReceiptDate"]).ToString("dd.MM.yyyy")
                                : "";

                            items.Add(new
                            {
                                ID = IDReceipt,
                                DisplayReceipt = $"{ReceiptNumber} от {ReceiptDate}"
                            });
                        }

                        CbReceipt.ItemsSource = items;
                        CbReceipt.DisplayMemberPath = "DisplayReceipt";
                        CbReceipt.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки актов поступления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadBooksAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    const string query = @"
                        SELECT 
                            b.IDBook,
                            b.InventaryNumber AS InventoryNumber,
                            b.TitleBook AS Title,
                            b.YearPublishing AS Year,
                            b.Author AS AuthorId,
                            ISNULL(a.NameAuthor, '') AS AuthorName,
                            ISNULL(a.SurnameAuthor, '') AS AuthorSurname,
                            ISNULL(a.PatronymicAuthor, '') AS AuthorPatronymic,
                            b.Gener AS GenreId,
                            ISNULL(g.TitleGener, '') AS GenreName,
                            b.StatusBook AS StatusId,
                            ISNULL(s.Title_Status, '') AS StatusTitle,
                            b.ID_View_Book AS ViewId,
                            ISNULL(v.View_Book, '') AS ViewName,
                            b.PublishingHouse AS PublisherId,
                            ISNULL(p.TitlePublishingHouse, '') AS PublisherName,
                            b.IDReceip AS ReceiptId,
                            ISNULL(r.ReceiptNumber, '') AS ReceiptNumber,
                            b.Anotation, 
                            b.Price
                        FROM Books b
                        LEFT JOIN Authors a ON b.Author = a.ID_Autor
                        LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                        LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                        LEFT JOIN ViewBook v ON b.ID_View_Book = v.ID_View_Book
                        LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                        LEFT JOIN ReceiptBooks r ON b.IDReceip = r.IDReceipt
                        ORDER BY b.TitleBook;";

                    using (var connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand(query, connection))
                        using (var reader = command.ExecuteReader())
                        {
                            var books = new ObservableCollection<dynamic>();

                            while (reader.Read())
                            {
                                books.Add(new
                                {
                                    IDBook = Convert.ToInt32(reader["IDBook"]),
                                    InventoryNumber = reader["InventoryNumber"].ToString(),
                                    Title = reader["Title"].ToString(),
                                    Year = Convert.ToInt32(reader["Year"]),
                                    AuthorId = reader["AuthorId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AuthorId"]),
                                    GenreId = reader["GenreId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["GenreId"]),
                                    StatusId = reader["StatusId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["StatusId"]),
                                    ViewId = Convert.ToInt32(reader["ViewId"]),
                                    PublisherId = reader["PublisherId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["PublisherId"]),
                                    ReceiptId = reader["ReceiptId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ReceiptId"]),
                                    AuthorName = $"{reader["AuthorSurname"]} {reader["AuthorName"]} {reader["AuthorPatronymic"]}".Trim(),
                                    GenreName = reader["GenreName"].ToString(),
                                    StatusTitle = reader["StatusTitle"].ToString(),
                                    ViewName = reader["ViewName"].ToString(),
                                    PublisherName = reader["PublisherName"].ToString(),
                                    Price = reader["Price"] != DBNull.Value
                                        ? Convert.ToDecimal(reader["Price"]).ToString("0.00", new CultureInfo("ru-RU"))
                                        : "",
                                    Anotation = reader["Anotation"]?.ToString()?.TrimEnd() ?? ""
                                });
                            }

                            Dispatcher.Invoke(() =>
                            {
                                _allBooks = books;
                                ApplyBookFilter("");
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private async Task LoadBookDataAsync(int bookId)
        {
            await Task.Run(() =>
            {
                try
                {
                    const string query = @"
                        SELECT 
                            b.IDBook,
                            b.InventaryNumber,
                            b.TitleBook,
                            b.Author,
                            b.PublishingHouse,
                            b.YearPublishing,
                            b.Gener,
                            b.StatusBook,
                            b.ID_View_Book,
                            b.Price,
                            b.Anotation,
                            b.IDReceip
                        FROM Books b
                        WHERE b.IDBook = @BookId";

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

                                        if (publishingHouse.HasValue)
                                            CbPublisher.SelectedValue = publishingHouse.Value;

                                        if (gener.HasValue)
                                            CbGenre.SelectedValue = gener.Value;

                                        if (statusBook.HasValue)
                                            CbStatus.SelectedValue = statusBook.Value;

                                        if (idReceip.HasValue)
                                            CbReceipt.SelectedValue = idReceip.Value;
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"Ошибка загрузки данных книги: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void ApplyBookFilter(string searchTerm)
        {
            if (_allBooks == null) return;

            var filtered = _allBooks.Where(book =>
            {
                string title = book.Title?.ToString() ?? "";
                string inv = book.InventoryNumber?.ToString() ?? "";
                string author = book.AuthorName?.ToString() ?? "";
                return string.IsNullOrEmpty(searchTerm) ||
                       title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       inv.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       author.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();

            Dispatcher.Invoke(() =>
            {
                LvBooks.ItemsSource = new ObservableCollection<dynamic>(filtered);
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyBookFilter(SearchBox.Text.Trim());
        }

        private void LvBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            dynamic selectedItem = LvBooks.SelectedItem;
            if (selectedItem == null) return;

            try
            {
                _currentBookId = selectedItem.IDBook;

                TbInventoryNumber.Text = selectedItem.InventoryNumber ?? "";
                TbTitle.Text = selectedItem.Title ?? "";
                TbYear.Text = selectedItem.Year?.ToString() ?? "";
                TbAnnotation.Text = selectedItem.Anotation ?? "";
                TbPrice.Text = selectedItem.Price ?? "";

                SetComboBoxSelection(CbAuthor, selectedItem.AuthorId);
                SetComboBoxSelection(CbViewBook, selectedItem.ViewId);
                SetComboBoxSelection(CbPublisher, selectedItem.PublisherId);
                SetComboBoxSelection(CbGenre, selectedItem.GenreId);
                SetComboBoxSelection(CbStatus, selectedItem.StatusId);
                SetComboBoxSelection(CbReceipt, selectedItem.ReceiptId);

                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки книги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetComboBoxSelection(ComboBox cb, object idValue)
        {
            if (idValue == null || idValue == DBNull.Value)
            {
                cb.SelectedIndex = -1;
                return;
            }

            if (int.TryParse(idValue.ToString(), out int id))
                cb.SelectedValue = id;
            else
                cb.SelectedIndex = -1;
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
                NavigationService.GoBack();
            else
                Window.GetWindow(this)?.Close();
        }

        private async void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbInventoryNumber.Text) ||
                string.IsNullOrWhiteSpace(TbTitle.Text) ||
                CbAuthor.SelectedValue == null ||
                CbViewBook.SelectedValue == null ||
                !int.TryParse(TbYear.Text, out int year))
            {
                MessageBox.Show("Заполните обязательные поля (*).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal price = 0;
            string priceText = TbPrice.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(priceText))
            {
                priceText = priceText.Replace(',', '.');
                if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    MessageBox.Show("Укажите корректную цену.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                

                await Task.Run(() =>
                {
                    const string sql = @"
                        INSERT INTO Books (
                            InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
                            Gener, StatusBook, Anotation, IDReceip, ID_View_Book, Price
                        ) VALUES (
                            @Inv, @Title, @Author, @Pub, @Year,
                            @Genre, @Status, @Annot, @Receipt, @View, @Price
                        )";

                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Inv", TbInventoryNumber.Text.Trim());
                            cmd.Parameters.AddWithValue("@Title", TbTitle.Text.Trim());
                            cmd.Parameters.AddWithValue("@Author", CbAuthor.SelectedValue);
                            cmd.Parameters.AddWithValue("@Pub", CbPublisher.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Year", year);
                            cmd.Parameters.AddWithValue("@Genre", CbGenre.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", CbStatus.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Annot", TbAnnotation.Text.Trim() ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Receipt", CbReceipt.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@View", CbViewBook.SelectedValue);
                            cmd.Parameters.AddWithValue("@Price", price);

                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                MessageBox.Show("Книга сохранена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                await LoadBooksAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
           
        }

        private async void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной книги для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TbInventoryNumber.Text) ||
                string.IsNullOrWhiteSpace(TbTitle.Text) ||
                CbAuthor.SelectedValue == null ||
                CbViewBook.SelectedValue == null ||
                !int.TryParse(TbYear.Text, out int year))
            {
                MessageBox.Show("Заполните обязательные поля (*).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal price = 0;
            string priceText = TbPrice.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(priceText))
            {
                priceText = priceText.Replace(',', '.');
                if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    MessageBox.Show("Укажите корректную цену.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {


                await Task.Run(() =>
                {
                    const string updateQuery = @"
                        UPDATE Books 
                        SET 
                            InventaryNumber = @Inv,
                            TitleBook = @Title,
                            Author = @Author,
                            PublishingHouse = @Pub,
                            YearPublishing = @Year,
                            Gener = @Genre,
                            StatusBook = @Status,
                            Anotation = @Annot,
                            IDReceip = @Receipt,
                            ID_View_Book = @View,
                            Price = @Price
                        WHERE IDBook = @Id;";

                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Inv", TbInventoryNumber.Text.Trim());
                            cmd.Parameters.AddWithValue("@Title", TbTitle.Text.Trim());
                            cmd.Parameters.AddWithValue("@Author", CbAuthor.SelectedValue);
                            cmd.Parameters.AddWithValue("@Pub", CbPublisher.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Year", year);
                            cmd.Parameters.AddWithValue("@Genre", CbGenre.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", CbStatus.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Annot", TbAnnotation.Text.Trim() ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Receipt", CbReceipt.SelectedValue ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@View", CbViewBook.SelectedValue);
                            cmd.Parameters.AddWithValue("@Price", price);
                            cmd.Parameters.AddWithValue("@Id", _currentBookId.Value);

                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                MessageBox.Show("Книга успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                await LoadBooksAsync();

                if (NavigationService.CanGoBack)
                    NavigationService.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной книги для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить эту книгу?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
               

                await Task.Run(() =>
                {
                    const string deleteQuery = "DELETE FROM Books WHERE IDBook = @Id;";

                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(deleteQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", _currentBookId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                MessageBox.Show("Книга удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                await LoadBooksAsync();

                if (NavigationService.CanGoBack)
                    NavigationService.GoBack();
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show("Невозможно удалить книгу: на неё ссылаются другие записи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void ClearForm()
        {
            Dispatcher.Invoke(() =>
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
                    MessageBox.Show($"PDF сохранён:\n{fullPath}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка генерации PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите акт для формирования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

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
                    MessageBox.Show($"Год издания не может быть больше текущего ({currentYear}).", "Некорректный год", MessageBoxButton.OK, MessageBoxImage.Warning);
                    tb.Text = currentYear.ToString();
                    tb.SelectAll();
                    tb.Focus();
                }
                else if (year < 1000)
                {
                    MessageBox.Show("Год издания должен быть не меньше 1000.", "Некорректный год", MessageBoxButton.OK, MessageBoxImage.Warning);
                    tb.Clear();
                    tb.Focus();
                }
            }
            else
            {
                MessageBox.Show("Введите корректный год (только цифры).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Clear();
                tb.Focus();
            }
        }

        private bool IsTextNumeric(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$");

        private void TbPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string text = e.Text;
            if (text.All(c => char.IsDigit(c) || c == ',')) return;
            e.Handled = true;
        }

        private void TbPrice_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (text.All(c => char.IsDigit(c) || c == ',')) return;
            }
            e.CancelCommand();
        }

        private void TbPrice_LostFocus(object sender, RoutedEventArgs e)
        {
            string input = TbPrice.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                TbPrice.Text = "";
                return;
            }

            string normalized = input.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
            {
                value = Math.Round(value, 2);
                TbPrice.Text = value.ToString("0.00", new CultureInfo("ru-RU"));
            }
            else
            {
                MessageBox.Show("Введите корректную цену (например: 625,00 или 625.00)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                TbPrice.Clear();
            }
        }
    }
}