using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public partial class BulkAddBooks : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";

        // Коллекции для комбобоксов
        private ObservableCollection<AuthorItem> _authors;
        private ObservableCollection<ViewBookItem> _viewBooks;
        private ObservableCollection<PublisherItem> _publishers;
        private ObservableCollection<GenreItem> _genres;
        private ObservableCollection<ReceiptItem> _receipts;

        public class AuthorItem
        {
            public int ID { get; set; }
            public string DisplayAuthors { get; set; }
        }

        public class ViewBookItem
        {
            public int ID { get; set; }
            public string DisplayViewBook { get; set; }
        }

        public class PublisherItem
        {
            public int ID { get; set; }
            public string DisplayPH { get; set; }
        }

        public class GenreItem
        {
            public int ID { get; set; }
            public string DisplayGen { get; set; }
        }

        // НОВЫЙ КЛАСС: для акта поступления
        public class ReceiptItem
        {
            public int ID { get; set; }
            public string ReceiptNumber { get; set; }
            public DateTime ReceiptDate { get; set; }
            public string DisplayReceipt => $"{ReceiptNumber} от {ReceiptDate:dd.MM.yyyy}";
        }

        public BulkAddBooks()
        {
            InitializeComponent();

            // Загружаем данные
            LoadAuthors();
            LoadViewBooks();
            LoadPublishers();
            LoadGenres();
            LoadReceipts(); // НОВЫЙ МЕТОД: загрузка актов поступления

            // Подписываемся на события
            QuantityBox.TextChanged += QuantityBox_TextChanged;

            // Обновляем предпросмотр после загрузки страницы
            this.Loaded += (s, e) =>
            {
                UpdateInventoryPreview();
            };
        }

        private void LoadAuthors()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _authors = new ObservableCollection<AuthorItem>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Autor"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Autor"]);
                            string surname = reader["SurnameAuthor"]?.ToString() ?? "";
                            string name = reader["NameAuthor"]?.ToString() ?? "";
                            string patronymic = reader["PatronymicAuthor"]?.ToString() ?? "";

                            _authors.Add(new AuthorItem
                            {
                                ID = id,
                                DisplayAuthors = $"{surname} {name} {patronymic}".Trim()
                            });
                        }

                        AuthorCombo.ItemsSource = _authors;
                        AuthorCombo.DisplayMemberPath = "DisplayAuthors";
                        AuthorCombo.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadViewBooks()
        {
            try
            {
                const string sql = "SELECT ID_View_Book, View_Book FROM ViewBook ORDER BY View_Book";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _viewBooks = new ObservableCollection<ViewBookItem>();
                        while (reader.Read())
                        {
                            int id = reader["ID_View_Book"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_View_Book"]);
                            string view = reader["View_Book"]?.ToString() ?? "";

                            _viewBooks.Add(new ViewBookItem
                            {
                                ID = id,
                                DisplayViewBook = view
                            });
                        }

                        ViewBookCombo.ItemsSource = _viewBooks;
                        ViewBookCombo.DisplayMemberPath = "DisplayViewBook";
                        ViewBookCombo.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки видов книг: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPublishers()
        {
            try
            {
                const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _publishers = new ObservableCollection<PublisherItem>();
                        while (reader.Read())
                        {
                            int id = reader["IDPublishingHouse"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDPublishingHouse"]);
                            string title = reader["TitlePublishingHouse"]?.ToString() ?? "";

                            _publishers.Add(new PublisherItem
                            {
                                ID = id,
                                DisplayPH = title
                            });
                        }

                        PublisherCombo.ItemsSource = _publishers;
                        PublisherCombo.DisplayMemberPath = "DisplayPH";
                        PublisherCombo.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGenres()
        {
            try
            {
                const string sql = "SELECT ID_Gener, TitleGener FROM GenersBook ORDER BY TitleGener";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _genres = new ObservableCollection<GenreItem>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Gener"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Gener"]);
                            string title = reader["TitleGener"]?.ToString() ?? "";

                            _genres.Add(new GenreItem
                            {
                                ID = id,
                                DisplayGen = title
                            });
                        }

                        GenreCombo.ItemsSource = _genres;
                        GenreCombo.DisplayMemberPath = "DisplayGen";
                        GenreCombo.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // НОВЫЙ МЕТОД: загрузка актов поступления
        private void LoadReceipts()
        {
            try
            {
                const string sql = "SELECT IDReceipt, ReceiptNumber, ReceiptDate FROM ReceiptBooks ORDER BY ReceiptNumber DESC";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _receipts = new ObservableCollection<ReceiptItem>();
                        while (reader.Read())
                        {
                            int id = reader["IDReceipt"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDReceipt"]);
                            string receiptNumber = reader["ReceiptNumber"]?.ToString() ?? "";
                            DateTime receiptDate = reader["ReceiptDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ReceiptDate"])
                                : DateTime.Now;

                            _receipts.Add(new ReceiptItem
                            {
                                ID = id,
                                ReceiptNumber = receiptNumber,
                                ReceiptDate = receiptDate
                            });
                        }

                        ReceiptCombo.ItemsSource = _receipts;
                        ReceiptCombo.DisplayMemberPath = "DisplayReceipt";
                        ReceiptCombo.SelectedValuePath = "ID";

                        // Выбираем первый акт по умолчанию (если есть)
                        if (_receipts.Count > 0)
                        {
                            ReceiptCombo.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки актов поступления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateInventoryPreview()
        {
            try
            {
                // Проверяем, что элементы управления инициализированы
                if (QuantityBox == null || InventoryPreviewText == null)
                    return;

                int quantity = 1;

                // Безопасно парсим количество
                if (!string.IsNullOrWhiteSpace(QuantityBox.Text))
                {
                    int.TryParse(QuantityBox.Text, out quantity);
                }

                if (quantity < 1) quantity = 1;

                int nextNumber = GetNextInventoryNumber();
                var previewNumbers = new System.Text.StringBuilder();

                for (int i = 0; i < Math.Min(quantity, 5); i++)
                {
                    if (i > 0) previewNumbers.Append(", ");
                    previewNumbers.Append($"{nextNumber + i:D6}");
                }

                if (quantity > 5)
                {
                    previewNumbers.Append($" ... и ещё {quantity - 5}");
                }

                InventoryPreviewText.Text = previewNumbers.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateInventoryPreview: {ex.Message}");
                if (InventoryPreviewText != null)
                    InventoryPreviewText.Text = "Ошибка генерации номеров";
            }
        }

        private int GetNextInventoryNumber()
        {
            const string sql = @"
                SELECT ISNULL(MAX(CAST(InventaryNumber AS INT)), 0)
                FROM Books 
                WHERE ISNUMERIC(InventaryNumber) = 1";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    int maxNum = (int)cmd.ExecuteScalar();
                    return maxNum + 1;
                }
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("Введите название книги.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleBox.Focus();
                return false;
            }

            if (AuthorCombo.SelectedItem == null)
            {
                MessageBox.Show("Выберите автора.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AuthorCombo.Focus();
                return false;
            }

            if (ViewBookCombo.SelectedItem == null)
            {
                MessageBox.Show("Выберите вид книги.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ViewBookCombo.Focus();
                return false;
            }

            if (!int.TryParse(YearBox.Text, out int year) || year < 1000 || year > DateTime.Now.Year)
            {
                MessageBox.Show($"Введите корректный год (1000-{DateTime.Now.Year}).", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                YearBox.Focus();
                return false;
            }

            if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity < 1 || quantity > 100)
            {
                MessageBox.Show("Введите корректное количество экземпляров (1-100).", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                QuantityBox.Focus();
                return false;
            }

            // НОВАЯ ПРОВЕРКА: акт поступления (необязательно, но рекомендуется)
            if (ReceiptCombo.SelectedItem == null)
            {
                var result = MessageBox.Show("Акт поступления не выбран. Продолжить без привязки к акту?",
                    "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    ReceiptCombo.Focus();
                    return false;
                }
            }

            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                int quantity = int.Parse(QuantityBox.Text);

                string title = TitleBox.Text.Trim();
                int authorId = (int)AuthorCombo.SelectedValue;
                int viewBookId = (int)ViewBookCombo.SelectedValue;
                int? publisherId = PublisherCombo.SelectedValue as int?;
                int? genreId = GenreCombo.SelectedValue as int?;
                int year = int.Parse(YearBox.Text);

                // НОВОЕ: получаем ID акта поступления
                int? receiptId = ReceiptCombo.SelectedValue as int?;

                decimal price = 0;
                if (!string.IsNullOrWhiteSpace(PriceBox.Text))
                {
                    decimal.TryParse(PriceBox.Text.Replace(',', '.'), out price);
                }

                int nextNumber = GetNextInventoryNumber();
                int successCount = 0;
                int statusId = GetDefaultStatusId();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            for (int i = 0; i < quantity; i++)
                            {
                                string invNumber = $"{nextNumber + i:D6}";

                                // ОБНОВЛЕННЫЙ ЗАПРОС: добавлен IDReceip
                                string query = @"
                                    INSERT INTO Books (
                                        InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
                                        Gener, StatusBook, ID_View_Book, Price, TotalCopies, AvailableCopies, IDReceip
                                    ) VALUES (
                                        @Inv, @Title, @Author, @Pub, @Year,
                                        @Genre, @Status, @View, @Price, 1, 1, @ReceiptId
                                    )";

                                using (var cmd = new SqlCommand(query, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Inv", invNumber);
                                    cmd.Parameters.AddWithValue("@Title", title);
                                    cmd.Parameters.AddWithValue("@Author", authorId);
                                    cmd.Parameters.AddWithValue("@Pub", publisherId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Year", year);
                                    cmd.Parameters.AddWithValue("@Genre", genreId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Status", statusId);
                                    cmd.Parameters.AddWithValue("@View", viewBookId);
                                    cmd.Parameters.AddWithValue("@Price", price);
                                    cmd.Parameters.AddWithValue("@ReceiptId", receiptId ?? (object)DBNull.Value);

                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show($"Успешно добавлено {successCount} экземпляров книг!\n" +
                                           $"Инвентарные номера: {nextNumber:D6} - {nextNumber + quantity - 1:D6}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

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
            catch (SqlException ex)
            {
                if (ex.Number == 2627) // Нарушение уникальности
                {
                    MessageBox.Show("Произошла ошибка при генерации инвентарных номеров. Возможно, в базе есть нечисловые инвентарные номера.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка базы данных: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetDefaultStatusId()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 1 ID_Status FROM StatusBook WHERE Title_Status LIKE '%в наличии%'";
                using (var cmd = new SqlCommand(query, conn))
                {
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 1;
                }
            }
        }

        private void ClearForm()
        {
            TitleBox.Clear();
            AuthorCombo.SelectedIndex = -1;
            ViewBookCombo.SelectedIndex = -1;
            PublisherCombo.SelectedIndex = -1;
            GenreCombo.SelectedIndex = -1;
            YearBox.Clear();
            PriceBox.Clear();
            QuantityBox.Text = "1";
            ReceiptCombo.SelectedIndex = -1; // НОВОЕ: очистка акта поступления
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            UpdateInventoryPreview();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void PriceValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем цифры и запятую
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',')
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void PriceBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Форматирование цены при необходимости
        }

        private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInventoryPreview();
        }

        // НОВЫЙ ОБРАБОТЧИК: изменение выбора акта поступления
        private void ReceiptCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику при необходимости
        }

        // НОВЫЙ МЕТОД: добавление нового акта поступления
        private void AddReceipt_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdmissionBook());
        }

        // Методы для открытия страниц добавления справочников
        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddAuthor());
        }

        private void AddViewBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddViewBook());
        }

        private void AddPublisher_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddPublishingHouse());
        }

        private void AddGenre_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddGener());
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAuthors();
            LoadViewBooks();
            LoadPublishers();
            LoadGenres();
            LoadReceipts(); // НОВОЕ: обновление актов при загрузке
        }
    }
}