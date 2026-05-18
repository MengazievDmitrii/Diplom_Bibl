using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
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
        string _connectionString = DatabaseHelper.ConnectionString;
        private int _defaultStatusId = 1;

        // Коллекции для комбобоксов
        private ObservableCollection<AuthorItem> _authors;
        private ObservableCollection<EditionTypeItem> _editionTypes;  // Заменяет ViewBook
        private ObservableCollection<PublisherItem> _publishers;
        private ObservableCollection<GenreItem> _genres;
        private ObservableCollection<ReceiptItem> _receipts;
        private ObservableCollection<CategoryItem> _categories;
        private ObservableCollection<FormatItem> _formats;
        private ObservableCollection<StatusItem> _statuses;

        public class AuthorItem
        {
            public int ID { get; set; }
            public string DisplayAuthors { get; set; }
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

        public class ReceiptItem
        {
            public int ID { get; set; }
            public string ReceiptNumber { get; set; }
            public DateTime ReceiptDate { get; set; }
            public string DisplayReceipt => $"{ReceiptNumber} от {ReceiptDate:dd.MM.yyyy}";
        }

        public class CategoryItem
        {
            public int ID { get; set; }
            public string CategoryName { get; set; }
            public string DisplayCategory => CategoryName;
        }

        // Вид издания (EditionTypes)
        public class EditionTypeItem
        {
            public int ID { get; set; }
            public string EditionTypeName { get; set; }
            public string EditionTypeNameDisplay => EditionTypeName;
        }

        public class FormatItem
        {
            public int ID { get; set; }
            public string FormatName { get; set; }
            public string DisplayFormat => FormatName;
        }

        public class StatusItem
        {
            public int ID { get; set; }
            public string Title_Status { get; set; }
            public string DisplayStatus => Title_Status;
        }

        public BulkAddBooks()
        {
            InitializeComponent();
            LoadData();
            QuantityBox.TextChanged += QuantityBox_TextChanged;
            this.Loaded += (s, e) => UpdateInventoryPreview();
        }

        private async void LoadData()
        {
            await LoadAuthors();
            await LoadEditionTypes();    // Загружаем виды изданий вместо ViewBook
            await LoadPublishers();
            await LoadGenres();
            await LoadReceipts();
            await LoadCategories();
            await LoadFormats();
            await LoadStatuses();
        }

        private async System.Threading.Tasks.Task LoadAuthors()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<AuthorItem>();
                            while (reader.Read())
                            {
                                int id = reader["ID_Autor"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Autor"]);
                                string surname = reader["SurnameAuthor"]?.ToString() ?? "";
                                string name = reader["NameAuthor"]?.ToString() ?? "";
                                string patronymic = reader["PatronymicAuthor"]?.ToString() ?? "";

                                items.Add(new AuthorItem
                                {
                                    ID = id,
                                    DisplayAuthors = $"{surname} {name} {patronymic}".Trim()
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _authors = items;
                                AuthorCombo.ItemsSource = _authors;
                                AuthorCombo.DisplayMemberPath = "DisplayAuthors";
                                AuthorCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // Загрузка видов изданий (EditionTypes) - заменяет ViewBook
        private async System.Threading.Tasks.Task LoadEditionTypes()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_EditionType, EditionTypeName FROM EditionTypes ORDER BY EditionTypeName";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<EditionTypeItem>();
                            while (reader.Read())
                            {
                                items.Add(new EditionTypeItem
                                {
                                    ID = reader["ID_EditionType"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_EditionType"]),
                                    EditionTypeName = reader["EditionTypeName"]?.ToString() ?? ""
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _editionTypes = items;
                                EditionTypeCombo.ItemsSource = _editionTypes;
                                EditionTypeCombo.DisplayMemberPath = "EditionTypeName";
                                EditionTypeCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки видов изданий: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadPublishers()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<PublisherItem>();
                            while (reader.Read())
                            {
                                int id = reader["IDPublishingHouse"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDPublishingHouse"]);
                                string title = reader["TitlePublishingHouse"]?.ToString() ?? "";

                                items.Add(new PublisherItem
                                {
                                    ID = id,
                                    DisplayPH = title
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _publishers = items;
                                PublisherCombo.ItemsSource = _publishers;
                                PublisherCombo.DisplayMemberPath = "DisplayPH";
                                PublisherCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadGenres()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Gener, TitleGener FROM GenersBook ORDER BY TitleGener";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<GenreItem>();
                            while (reader.Read())
                            {
                                int id = reader["ID_Gener"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Gener"]);
                                string title = reader["TitleGener"]?.ToString() ?? "";

                                items.Add(new GenreItem
                                {
                                    ID = id,
                                    DisplayGen = title
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _genres = items;
                                GenreCombo.ItemsSource = _genres;
                                GenreCombo.DisplayMemberPath = "DisplayGen";
                                GenreCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadReceipts()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT IDReceipt, ReceiptNumber, ReceiptDate FROM ReceiptBooks ORDER BY ReceiptNumber DESC";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<ReceiptItem>();
                            while (reader.Read())
                            {
                                int id = reader["IDReceipt"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IDReceipt"]);
                                string receiptNumber = reader["ReceiptNumber"]?.ToString() ?? "";
                                DateTime receiptDate = reader["ReceiptDate"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["ReceiptDate"])
                                    : DateTime.Now;

                                items.Add(new ReceiptItem
                                {
                                    ID = id,
                                    ReceiptNumber = receiptNumber,
                                    ReceiptDate = receiptDate
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _receipts = items;
                                ReceiptCombo.ItemsSource = _receipts;
                                ReceiptCombo.DisplayMemberPath = "DisplayReceipt";
                                ReceiptCombo.SelectedValuePath = "ID";

                                if (_receipts.Count > 0) ReceiptCombo.SelectedIndex = 0;
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки актов поступления: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadCategories()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Category, CategoryName FROM BookCategories ORDER BY CategoryName";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<CategoryItem>();
                            while (reader.Read())
                            {
                                items.Add(new CategoryItem
                                {
                                    ID = reader["ID_Category"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Category"]),
                                    CategoryName = reader["CategoryName"]?.ToString() ?? ""
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _categories = items;
                                CategoryCombo.ItemsSource = _categories;
                                CategoryCombo.DisplayMemberPath = "CategoryName";
                                CategoryCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки категорий: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadFormats()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Format, FormatName FROM PublicationFormats ORDER BY FormatName";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<FormatItem>();
                            while (reader.Read())
                            {
                                items.Add(new FormatItem
                                {
                                    ID = reader["ID_Format"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Format"]),
                                    FormatName = reader["FormatName"]?.ToString() ?? ""
                                });
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _formats = items;
                                FormatCombo.ItemsSource = _formats;
                                FormatCombo.DisplayMemberPath = "FormatName";
                                FormatCombo.SelectedValuePath = "ID";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки форматов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadStatuses()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Status, Title_Status FROM StatusBook WHERE Title_Status NOT LIKE '%Списана%' AND Title_Status NOT LIKE '%Утрачен%' ORDER BY Title_Status";
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var items = new ObservableCollection<StatusItem>();
                            while (reader.Read())
                            {
                                int id = reader["ID_Status"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ID_Status"]);
                                string title = reader["Title_Status"].ToString();
                                items.Add(new StatusItem { ID = id, Title_Status = title });
                                if (title.Contains("В наличии")) _defaultStatusId = id;
                            }
                            Dispatcher.Invoke(() =>
                            {
                                _statuses = items;
                                StatusCombo.ItemsSource = _statuses;
                                StatusCombo.DisplayMemberPath = "DisplayStatus";
                                StatusCombo.SelectedValuePath = "ID";

                                if (items.Count > 0) StatusCombo.SelectedValue = _defaultStatusId;
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки статусов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void UpdateInventoryPreview()
        {
            try
            {
                if (QuantityBox == null || InventoryPreviewText == null) return;

                int quantity = 1;
                if (!string.IsNullOrWhiteSpace(QuantityBox.Text)) int.TryParse(QuantityBox.Text, out quantity);
                if (quantity < 1) quantity = 1;

                int nextNumber = GetNextInventoryNumber();
                var previewNumbers = new System.Text.StringBuilder();

                for (int i = 0; i < Math.Min(quantity, 5); i++)
                {
                    if (i > 0) previewNumbers.Append(", ");
                    previewNumbers.Append($"{nextNumber + i:D6}");
                }

                if (quantity > 5) previewNumbers.Append($" ... и ещё {quantity - 5}");
                InventoryPreviewText.Text = previewNumbers.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateInventoryPreview: {ex.Message}");
                if (InventoryPreviewText != null) InventoryPreviewText.Text = "Ошибка генерации номеров";
            }
        }

        private int GetNextInventoryNumber()
        {
            const string sql = "SELECT ISNULL(MAX(CAST(InventaryNumber AS INT)), 0) FROM Books WHERE ISNUMERIC(InventaryNumber) = 1";
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    return (int)cmd.ExecuteScalar() + 1;
                }
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                CustomMessageBox.Show("Введите название книги.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TitleBox.Focus();
                return false;
            }

            if (AuthorCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите автора.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                AuthorCombo.Focus();
                return false;
            }

            if (EditionTypeCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите вид издания.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                EditionTypeCombo.Focus();
                return false;
            }

            if (!int.TryParse(YearBox.Text, out int year) || year < 1000 || year > DateTime.Now.Year)
            {
                CustomMessageBox.Show($"Введите корректный год (1000-{DateTime.Now.Year}).", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                YearBox.Focus();
                return false;
            }

            if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity < 1 || quantity > 100)
            {
                CustomMessageBox.Show("Введите корректное количество экземпляров (1-100).", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                QuantityBox.Focus();
                return false;
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
                int editionTypeId = (int)EditionTypeCombo.SelectedValue;
                int? publisherId = PublisherCombo.SelectedValue as int?;
                int? genreId = GenreCombo.SelectedValue as int?;
                int? categoryId = CategoryCombo.SelectedValue as int?;
                int? formatId = FormatCombo.SelectedValue as int?;
                int? statusId = StatusCombo.SelectedValue as int? ?? _defaultStatusId;
                int? receiptId = ReceiptCombo.SelectedValue as int?;
                int year = int.Parse(YearBox.Text);
                string annotation = AnnotationBox.Text?.Trim();

                decimal price = 0;
                if (!string.IsNullOrWhiteSpace(PriceBox.Text))
                    decimal.TryParse(PriceBox.Text.Replace(',', '.'), out price);

                int nextNumber = GetNextInventoryNumber();
                int successCount = 0;

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

                                string query = @"
                                    INSERT INTO Books (
                                        InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
                                        Gener, StatusBook, Anotation, IDReceip, Price, TotalCopies, AvailableCopies,
                                        CategoryID, EditionTypeID, FormatID
                                    ) VALUES (
                                        @Inv, @Title, @Author, @Pub, @Year,
                                        @Genre, @Status, @Annot, @Receipt, @Price, 1, 1,
                                        @CategoryID, @EditionTypeID, @FormatID
                                    )";

                                using (var cmd = new SqlCommand(query, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Inv", invNumber);
                                    cmd.Parameters.AddWithValue("@Title", title);
                                    cmd.Parameters.AddWithValue("@Author", authorId);
                                    cmd.Parameters.AddWithValue("@Pub", publisherId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Year", year);
                                    cmd.Parameters.AddWithValue("@Genre", genreId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Status", statusId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Annot", string.IsNullOrWhiteSpace(annotation) ? (object)DBNull.Value : annotation);
                                    cmd.Parameters.AddWithValue("@Receipt", receiptId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Price", price);
                                    cmd.Parameters.AddWithValue("@CategoryID", categoryId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@EditionTypeID", editionTypeId);
                                    cmd.Parameters.AddWithValue("@FormatID", formatId ?? (object)DBNull.Value);

                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }

                            transaction.Commit();

                            CustomMessageBox.Show($"Успешно добавлено {successCount} экземпляров книг!\n" +
                                          $"Инвентарные номера: {nextNumber:D6} - {nextNumber + quantity - 1:D6}",
                                "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

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
            catch (SqlException ex) when (ex.Number == 2627)
            {
                CustomMessageBox.Show("Произошла ошибка при генерации инвентарных номеров. Возможно, в базе есть нечисловые инвентарные номера.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            TitleBox.Clear();
            AuthorCombo.SelectedIndex = -1;
            EditionTypeCombo.SelectedIndex = -1;
            PublisherCombo.SelectedIndex = -1;
            GenreCombo.SelectedIndex = -1;
            CategoryCombo.SelectedIndex = -1;
            FormatCombo.SelectedIndex = -1;
            StatusCombo.SelectedValue = _defaultStatusId;
            YearBox.Clear();
            PriceBox.Clear();
            AnnotationBox.Clear();
            QuantityBox.Text = "1";
            ReceiptCombo.SelectedIndex = -1;
            UpdateInventoryPreview();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void PriceValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',')
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void PriceBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInventoryPreview();
        }

        private void ReceiptCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // Кнопки добавления справочников
        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddAuthor());
        }

        private void AddEditionType_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddEditionType());
        }

        private void AddPublisher_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddPublishingHouse());
        }

        private void AddGenre_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddGener());
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddBookCategory());
        }

        private void AddFormat_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddPublicationFormat());
        }

        private void AddStatus_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddStatusBook());
        }

        private void AddReceipt_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdmissionBook());
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}