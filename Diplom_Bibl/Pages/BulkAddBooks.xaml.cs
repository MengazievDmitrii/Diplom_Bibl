using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public partial class BulkAddBooks : Page
    {
        // ========== ПОЛЯ КЛАССА ==========

        // Строка подключения к базе данных
        private readonly string _connectionString = DatabaseHelper.ConnectionString;

        // ID статуса "В наличии" по умолчанию
        private int _defaultStatusId = 1;

        // Текущий максимальный инвентарный номер в БД
        private int _currentMaxInventoryNumber = 0;

        // Коллекции для заполнения комбобоксов
        private ObservableCollection<AuthorItem> _authors;
        private ObservableCollection<EditionTypeItem> _editionTypes;
        private ObservableCollection<PublisherItem> _publishers;
        private ObservableCollection<GenreItem> _genres;
        private ObservableCollection<ReceiptItem> _receipts;
        private ObservableCollection<CategoryItem> _categories;
        private ObservableCollection<FormatItem> _formats;
        private ObservableCollection<StatusItem> _statuses;
        private ObservableCollection<UserItem> _responsibleUsers;
        private ObservableCollection<SupplierItem> _suppliers;
        private const int MIN_YEAR = 1450;
        private const int MAX_YEAR = 2100;

        // ========== ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ДЛЯ КОМБОБОКСОВ ==========

        /// <summary>
        /// Класс для отображения авторов в комбобоксе
        /// </summary>
        public class AuthorItem
        {
            public int ID { get; set; }
            public string DisplayAuthors { get; set; }
        }

        /// <summary>
        /// Класс для отображения издательств в комбобоксе
        /// </summary>
        public class PublisherItem
        {
            public int ID { get; set; }
            public string DisplayPH { get; set; }
        }

        /// <summary>
        /// Класс для отображения жанров в комбобоксе
        /// </summary>
        public class GenreItem
        {
            public int ID { get; set; }
            public string DisplayGen { get; set; }
        }

        /// <summary>
        /// Класс для отображения актов поступления в комбобоксе
        /// </summary>
        public class ReceiptItem
        {
            public int ID { get; set; }
            public string ReceiptNumber { get; set; }
            public DateTime ReceiptDate { get; set; }
            public string DisplayReceipt => $"{ReceiptNumber} от {ReceiptDate:dd.MM.yyyy}";
        }

        /// <summary>
        /// Класс для отображения категорий книг в комбобоксе
        /// </summary>
        public class CategoryItem
        {
            public int ID { get; set; }
            public string CategoryName { get; set; }
        }

        /// <summary>
        /// Класс для отображения видов изданий в комбобоксе
        /// </summary>
        public class EditionTypeItem
        {
            public int ID { get; set; }
            public string EditionTypeName { get; set; }
        }

        /// <summary>
        /// Класс для отображения форматов изданий в комбобоксе
        /// </summary>
        public class FormatItem
        {
            public int ID { get; set; }
            public string FormatName { get; set; }
        }

        /// <summary>
        /// Класс для отображения статусов книг в комбобоксе
        /// </summary>
        public class StatusItem
        {
            public int ID { get; set; }
            public string Title_Status { get; set; }
            public string DisplayStatus => Title_Status;
        }

        /// <summary>
        /// Класс для отображения пользователей (ответственных лиц) в комбобоксе
        /// </summary>
        public class UserItem
        {
            public int ID_User { get; set; }
            public string FullName { get; set; } = "";
        }

        /// <summary>
        /// Класс для отображения поставщиков в комбобоксе
        /// </summary>
        public class SupplierItem
        {
            public int ID_Supplier { get; set; }
            public string SupplierName { get; set; } = "";
        }

        // ========== КОНСТРУКТОР ==========

        public BulkAddBooks()
        {
            InitializeComponent();
            LoadData();

            // Подписка на события для обновления предпросмотра
            QuantityBox.TextChanged += QuantityBox_TextChanged;
            StartInventoryBox.TextChanged += StartInventoryBox_TextChanged;

            // ПОДПИСКА НА СОБЫТИЕ ПОТЕРИ ФОКУСА ДЛЯ ПОЛЯ "ГОД"
            YearBox.LostFocus += YearBox_LostFocus;

            // Дополнительные подписки для валидации ввода года
            YearBox.PreviewTextInput += YearBox_PreviewTextInput;
            DataObject.AddPastingHandler(YearBox, YearBox_Pasting);

            // Обновление предпросмотра при загрузке страницы
            this.Loaded += (s, e) => UpdateInventoryPreview();
        }

        // ========== ЗАГРУЗКА ДАННЫХ ==========

        /// <summary>
        /// Загрузка всех справочных данных
        /// </summary>
        private async void LoadData()
        {
            await LoadAuthors();
            await LoadEditionTypes();
            await LoadPublishers();
            await LoadGenres();
            await LoadReceipts();
            await LoadCategories();
            await LoadFormats();
            await LoadStatuses();
            await LoadMaxInventoryNumber();
            await LoadResponsibleUsers();      // Загрузка ответственных лиц для создания акта
            await LoadSuppliersForNewReceipt(); // Загрузка поставщиков для создания акта
        }

        /// <summary>
        /// Загрузка максимального инвентарного номера из БД
        /// </summary>
        private async System.Threading.Tasks.Task LoadMaxInventoryNumber()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                const string sql = "SELECT ISNULL(MAX(CAST(InventaryNumber AS INT)), 0) FROM Books WHERE ISNUMERIC(InventaryNumber) = 1";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        _currentMaxInventoryNumber = (int)cmd.ExecuteScalar();
                    }
                }
            });
        }

        /// <summary>
        /// Загрузка списка авторов
        /// </summary>
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

        /// <summary>
        /// Загрузка видов изданий (EditionTypes)
        /// </summary>
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

        /// <summary>
        /// Загрузка издательств
        /// </summary>
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

        /// <summary>
        /// Загрузка жанров
        /// </summary>
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

        /// <summary>
        /// Загрузка актов поступления
        /// </summary>
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

        /// <summary>
        /// Загрузка категорий книг
        /// </summary>
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

        /// <summary>
        /// Загрузка форматов изданий
        /// </summary>
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

        /// <summary>
        /// Загрузка статусов книг (исключая списанные и утерянные)
        /// </summary>
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

        /// <summary>
        /// Загрузка ответственных лиц для создания нового акта
        /// </summary>
        private async System.Threading.Tasks.Task LoadResponsibleUsers()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = @"
                        SELECT u.ID_User, u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS FullName 
                        FROM Users u
                        INNER JOIN UserRolesAssignments ura ON ura.ID_User = u.ID_User
                        INNER JOIN UserRoles ur ON ur.ID_Role = ura.ID_Role
                        WHERE ur.RoleCode = 'LIBRARIAN'
                        ORDER BY u.Surname, u.Name";

                    var items = new ObservableCollection<UserItem>();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new UserItem
                                {
                                    ID_User = Convert.ToInt32(reader["ID_User"]),
                                    FullName = reader["FullName"].ToString()
                                });
                            }
                        }
                    }
                    Dispatcher.Invoke(() =>
                    {
                        _responsibleUsers = items;
                        NewReceiptResponsibleCombo.ItemsSource = _responsibleUsers;
                        NewReceiptResponsibleCombo.DisplayMemberPath = "FullName";
                        NewReceiptResponsibleCombo.SelectedValuePath = "ID_User";
                    });
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки ответственных лиц: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Загрузка поставщиков для создания нового акта
        /// </summary>
        private async System.Threading.Tasks.Task LoadSuppliersForNewReceipt()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    const string sql = "SELECT ID_Supplier, SupplierName FROM Suppliers ORDER BY SupplierName";
                    var items = new ObservableCollection<SupplierItem>();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new SupplierItem
                                {
                                    ID_Supplier = Convert.ToInt32(reader["ID_Supplier"]),
                                    SupplierName = reader["SupplierName"].ToString()
                                });
                            }
                        }
                    }
                    Dispatcher.Invoke(() =>
                    {
                        _suppliers = items;
                        NewReceiptSupplierCombo.ItemsSource = _suppliers;
                        NewReceiptSupplierCombo.DisplayMemberPath = "SupplierName";
                        NewReceiptSupplierCombo.SelectedValuePath = "ID_Supplier";
                    });
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки поставщиков: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // ========== ВАЛИДАЦИЯ ВВОДА ==========

        /// <summary>
        /// Валидация для цифровых полей - только цифры
        /// </summary>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        /// <summary>
        /// Валидация для инвентарного номера - только цифры
        /// </summary>
        private void InventoryNumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            e.Handled = !char.IsDigit(e.Text, 0);

            // Ограничение длины 6 символов
            if (!e.Handled && textBox != null && textBox.Text.Length >= 6)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Валидация для цены - цифры и запятая
        /// </summary>
        private void PriceValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox?.Text ?? "";
            string newText = currentText + e.Text;

            foreach (char c in e.Text)
            {
                // Разрешаем только цифры и запятую
                if (!char.IsDigit(c) && c != ',')
                {
                    e.Handled = true;
                    return;
                }

                // Запрещаем вторую запятую
                if (c == ',' && currentText.Contains(','))
                {
                    e.Handled = true;
                    return;
                }

                // Максимум 2 знака после запятой
                if (currentText.Contains(','))
                {
                    string[] parts = newText.Split(',');
                    if (parts.Length > 1 && parts[1].Length > 2)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Запрет вставки нецифровых символов
        /// </summary>
        private void NumberValidationPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, @"^\d+$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Запрет вставки некорректной цены
        /// </summary>
        private void PriceValidationPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, @"^\d+([,]\d{0,2})?$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        /// <summary>
        /// Валидация ввода для поля "Год" - только цифры
        /// </summary>
        private void YearBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры
            e.Handled = !char.IsDigit(e.Text, 0);
        }
        /// <summary>
        /// Запрет вставки нецифровых символов в поле "Год"
        /// </summary>
        private void YearBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, @"^\d+$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Обработка потери фокуса для поля "Год" с валидацией диапазона
        /// </summary>
        private void YearBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text.Trim();

            // Если поле пустое - не выполняем валидацию
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Проверяем, что введены только цифры
            if (!int.TryParse(text, out int year))
            {
                textBox.Clear();
                CustomMessageBox.Show($"Введите корректный год (только цифры).\nДопустимый диапазон: {MIN_YEAR}-{MAX_YEAR}",
                    "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                textBox.Focus();
                return;
            }

            int currentYear = DateTime.Now.Year;
            string errorMessage = null;

            // Проверка на минимальный год
            if (year < MIN_YEAR)
            {
                errorMessage = $"Год издания не может быть меньше {MIN_YEAR}.";
            }
            // Проверка на максимальный год
            else if (year > MAX_YEAR)
            {
                errorMessage = $"Год издания не может быть больше {MAX_YEAR}.";
            }
            // Проверка на будущий год (не может быть больше текущего)
            else if (year > currentYear)
            {
                errorMessage = $"Год издания не может быть больше текущего ({currentYear}).";
            }

            if (errorMessage != null)
            {
                textBox.Clear();
                CustomMessageBox.Show($"{errorMessage}\nДопустимый диапазон: {MIN_YEAR}-{currentYear}",
                    "Некорректный год", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Focus();
            }
        }

        /// <summary>
        /// Обработка потери фокуса для поля "Количество"
        /// </summary>
        private void QuantityBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                textBox.Text = "1";
                return;
            }

            if (int.TryParse(text, out int quantity))
            {
                if (quantity < 1)
                {
                    textBox.Text = "1";
                    CustomMessageBox.Show("Количество экземпляров должно быть не менее 1.",
                        "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
                else if (quantity > 100)
                {
                    textBox.Text = "100";
                    CustomMessageBox.Show("Количество экземпляров не может превышать 100.",
                        "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
            }
            else
            {
                textBox.Text = "1";
                CustomMessageBox.Show("Введите корректное количество (только цифры).",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            UpdateInventoryPreview();
        }

        /// <summary>
        /// Обработка потери фокуса для поля "Начальный инвентарный номер"
        /// </summary>
        private void StartInventoryBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                UpdateInventoryPreview();
                return;
            }

            if (int.TryParse(text, out int number))
            {
                if (number < 1)
                {
                    textBox.Text = "000001";
                }
                else if (number > 999999)
                {
                    textBox.Text = "999999";
                    CustomMessageBox.Show("Инвентарный номер не может превышать 999999.",
                        "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
                else
                {
                    textBox.Text = number.ToString("D6");
                }
                textBox.CaretIndex = textBox.Text.Length;
            }
            else if (!string.IsNullOrEmpty(text))
            {
                textBox.Clear();
                CustomMessageBox.Show("Введите корректный инвентарный номер (только цифры).",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }

            UpdateInventoryPreview();
        }

        // ========== ПРЕДПРОСМОТР ИНВЕНТАРНЫХ НОМЕРОВ ==========

        /// <summary>
        /// Обновление предпросмотра инвентарных номеров
        /// </summary>
        private void UpdateInventoryPreview()
        {
            try
            {
                if (QuantityBox == null || InventoryPreviewText == null) return;

                int quantity = 1;
                if (!string.IsNullOrWhiteSpace(QuantityBox.Text))
                    int.TryParse(QuantityBox.Text, out quantity);
                if (quantity < 1) quantity = 1;
                if (quantity > 100) quantity = 100;

                int startNumber = GetCurrentStartInventoryNumber();
                var previewNumbers = new System.Text.StringBuilder();

                for (int i = 0; i < Math.Min(quantity, 10); i++)
                {
                    if (i > 0) previewNumbers.Append(", ");
                    previewNumbers.Append($"{startNumber + i:D6}");
                }

                if (quantity > 10) previewNumbers.Append($" ... и ещё {quantity - 10}");

                InventoryPreviewText.Text = previewNumbers.ToString();

                // Проверка на конфликт с существующими номерами
                CheckInventoryNumberConflict(startNumber, quantity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateInventoryPreview: {ex.Message}");
                if (InventoryPreviewText != null)
                    InventoryPreviewText.Text = "Ошибка генерации номеров";
            }
        }

        /// <summary>
        /// Получение начального инвентарного номера (ручной ввод или автогенерация)
        /// </summary>
        private int GetCurrentStartInventoryNumber()
        {
            // Если пользователь ввел номер - используем его
            if (!string.IsNullOrWhiteSpace(StartInventoryBox.Text))
            {
                if (int.TryParse(StartInventoryBox.Text, out int userNumber) && userNumber > 0)
                {
                    return userNumber;
                }
            }

            // Иначе используем следующий номер от максимального в БД
            return _currentMaxInventoryNumber + 1;
        }

        /// <summary>
        /// Проверка конфликта инвентарных номеров с существующими в БД
        /// </summary>
        private async void CheckInventoryNumberConflict(int startNumber, int quantity)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var existingNumbers = new System.Collections.Generic.List<int>();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        for (int i = 0; i < quantity; i++)
                        {
                            int checkNumber = startNumber + i;
                            string sql = "SELECT COUNT(*) FROM Books WHERE InventaryNumber = @Num";
                            using (var cmd = new SqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@Num", checkNumber.ToString("D6"));
                                int count = (int)cmd.ExecuteScalar();
                                if (count > 0)
                                    existingNumbers.Add(checkNumber);
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (existingNumbers.Any())
                        {
                            string conflictMsg = $"⚠️ Внимание! Следующие инвентарные номера уже существуют:\n" +
                                                $"{string.Join(", ", existingNumbers.Select(n => n.ToString("D6")))}\n\n" +
                                                "Измените начальный номер или количество экземпляров.";
                            InventoryPreviewText.Text = conflictMsg;
                            InventoryPreviewText.Foreground = System.Windows.Media.Brushes.Red;
                            SaveButton.IsEnabled = false;
                        }
                        else
                        {
                            InventoryPreviewText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryColor");
                            SaveButton.IsEnabled = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки конфликтов: {ex.Message}");
                }
            });
        }

        // ========== ВАЛИДАЦИЯ ФОРМЫ ==========

        /// <summary>
        /// Проверка корректности заполнения формы
        /// </summary>
        private bool ValidateForm()
        {
            // Проверка названия книги
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                CustomMessageBox.Show("Введите название книги.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TitleBox.Focus();
                return false;
            }

            // Проверка выбора автора
            if (AuthorCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите автора.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                AuthorCombo.Focus();
                return false;
            }

            // Проверка выбора вида издания
            if (EditionTypeCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите вид издания.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                EditionTypeCombo.Focus();
                return false;
            }

            // Проверка года издания
            if (!int.TryParse(YearBox.Text, out int year) || year < 1000 || year > DateTime.Now.Year)
            {
                CustomMessageBox.Show($"Введите корректный год (1000-{DateTime.Now.Year}).", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                YearBox.Focus();
                return false;
            }

            // Проверка выбора акта поступления
            if (ReceiptCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите акт поступления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                ReceiptCombo.Focus();
                return false;
            }

            // Проверка количества экземпляров
            if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity < 1 || quantity > 100)
            {
                CustomMessageBox.Show("Введите корректное количество экземпляров (1-100).", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                QuantityBox.Focus();
                return false;
            }

            // Проверка начального инвентарного номера
            int startNumber = GetCurrentStartInventoryNumber();
            if (startNumber < 1 || startNumber > 999999)
            {
                CustomMessageBox.Show("Инвентарный номер должен быть в диапазоне 1-999999.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                StartInventoryBox.Focus();
                return false;
            }

            // Проверка последнего номера (не должен превышать 999999)
            int lastNumber = startNumber + quantity - 1;
            if (lastNumber > 999999)
            {
                CustomMessageBox.Show($"Последний инвентарный номер ({lastNumber:D6}) превышает допустимый лимит 999999.\n" +
                                      $"Уменьшите количество экземпляров или измените начальный номер.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // ========== УПРАВЛЕНИЕ АКТАМИ ПОСТУПЛЕНИЯ ==========

        /// <summary>
        /// Создание нового акта поступления
        /// </summary>
        private async void CreateNewReceipt_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем выбор ответственного лица
            if (NewReceiptResponsibleCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите ответственное лицо.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                int responsibleId = (int)NewReceiptResponsibleCombo.SelectedValue;
                int? supplierId = NewReceiptSupplierCombo.SelectedValue as int?;
                string comment = NewReceiptCommentBox.Text?.Trim();

                // Получаем следующий ID для генерации номера
                int nextId = 1;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT ISNULL(MAX(IDReceipt), 0) + 1 FROM ReceiptBooks", conn))
                    {
                        nextId = (int)cmd.ExecuteScalar();
                    }
                }

                string receiptNumber = $"ПН-{nextId:D4}";
                DateTime receiptDate = DateTime.Now;

                // Проверяем наличие колонки SupplierID
                bool hasSupplierColumn = false;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                          WHERE TABLE_NAME = 'ReceiptBooks' AND COLUMN_NAME = 'SupplierID'", conn))
                    {
                        hasSupplierColumn = (int)cmd.ExecuteScalar() > 0;
                    }
                }

                string insertQuery;
                if (hasSupplierColumn)
                {
                    insertQuery = @"
                        INSERT INTO ReceiptBooks (ReceiptNumber, ReceiptDate, ID_ResponsibleUser, SupplierID, Comment)
                        VALUES (@Number, @Date, @ResponsibleID, @SupplierID, @Comment);
                        SELECT SCOPE_IDENTITY();";
                }
                else
                {
                    insertQuery = @"
                        INSERT INTO ReceiptBooks (ReceiptNumber, ReceiptDate, ID_ResponsibleUser, Comment)
                        VALUES (@Number, @Date, @ResponsibleID, @Comment);
                        SELECT SCOPE_IDENTITY();";
                }

                int newReceiptId;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Number", receiptNumber);
                        cmd.Parameters.AddWithValue("@Date", receiptDate);
                        cmd.Parameters.AddWithValue("@ResponsibleID", responsibleId);
                        if (hasSupplierColumn)
                            cmd.Parameters.AddWithValue("@SupplierID", supplierId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);

                        newReceiptId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }

                CustomMessageBox.Show($"Акт поступления создан!\n\nНомер: {receiptNumber}\nДата: {receiptDate:dd.MM.yyyy HH:mm}",
                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                // Очищаем поля формы создания акта
                NewReceiptResponsibleCombo.SelectedIndex = -1;
                NewReceiptSupplierCombo.SelectedIndex = -1;
                NewReceiptCommentBox.Clear();

                // Обновляем список актов в основном комбобоксе
                await LoadReceipts();

                // Выбираем созданный акт в основном комбобоксе
                if (_receipts != null)
                {
                    var newReceipt = _receipts.FirstOrDefault(r => r.ID == newReceiptId);
                    if (newReceipt != null)
                    {
                        ReceiptCombo.SelectedItem = newReceipt;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка создания акта: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновление списка актов поступления
        /// </summary>
        private async void RefreshReceipts_Click(object sender, RoutedEventArgs e)
        {
            await LoadReceipts();
            CustomMessageBox.Show("Список актов обновлён.", "Информация",
                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Формирование акта поступления (PDF)
        /// </summary>
        private void GenerateReceiptAct()
        {
            if (ReceiptCombo.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите акт поступления для формирования документа.",
                    "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                // Получаем ID выбранного акта
                int receiptId = (int)ReceiptCombo.SelectedValue;

                // Получаем номер акта для отображения в сообщении
                string receiptNumber = "";
                if (ReceiptCombo.SelectedItem is ReceiptItem receiptItem)
                {
                    receiptNumber = receiptItem.ReceiptNumber;
                }

                var result = CustomMessageBox.Show(
                    $"Сформировать акт поступления \"{receiptNumber}\"?\n\n" +
                    "Будут включены все книги, привязанные к этому акту.",
                    "Формирование акта",
                    CustomMessageBoxButton.YesNo,
                    CustomMessageBoxImage.Question);

                if (result != CustomMessageBoxResult.Yes) return;

                // Генерируем PDF
                byte[] pdfBytes = ReceiptActPdfService.GenerateReceiptActPdf(receiptId);

                // Сохраняем на рабочий стол
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"Акт_поступления_{receiptNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                string fullPath = System.IO.Path.Combine(desktop, fileName);
                System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                CustomMessageBox.Show($"Акт поступления успешно сформирован!\n\n" +
                                      $"Файл сохранён:\n{fullPath}",
                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка формирования акта: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // ========== СОХРАНЕНИЕ ДАННЫХ ==========

        /// <summary>
        /// Сохранение книг в базу данных
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                int quantity = int.Parse(QuantityBox.Text);
                int startNumber = GetCurrentStartInventoryNumber();
                int receiptId = (int)ReceiptCombo.SelectedValue;

                string title = TitleBox.Text.Trim();
                int authorId = (int)AuthorCombo.SelectedValue;
                int editionTypeId = (int)EditionTypeCombo.SelectedValue;
                int? publisherId = PublisherCombo.SelectedValue as int?;
                int? genreId = GenreCombo.SelectedValue as int?;
                int? categoryId = CategoryCombo.SelectedValue as int?;
                int? formatId = FormatCombo.SelectedValue as int?;
                int? statusId = StatusCombo.SelectedValue as int? ?? _defaultStatusId;
                int year = int.Parse(YearBox.Text);
                string annotation = AnnotationBox.Text?.Trim();

                decimal price = 0;
                if (!string.IsNullOrWhiteSpace(PriceBox.Text))
                    decimal.TryParse(PriceBox.Text.Replace(',', '.'), out price);

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
                                int currentNumber = startNumber + i;
                                string invNumber = currentNumber.ToString("D6");

                                string query = @"
                                    INSERT INTO Books (
                                        InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
                                        Gener, StatusBook, Anotation, IDReceip, Price, TotalCopies, AvailableCopies, CopiesInUse,
                                        CategoryID, EditionTypeID, FormatID
                                    ) VALUES (
                                        @Inv, @Title, @Author, @Pub, @Year,
                                        @Genre, @Status, @Annot, @Receipt, @Price, 1, 1, 0,
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
                                    cmd.Parameters.AddWithValue("@Receipt", receiptId);
                                    cmd.Parameters.AddWithValue("@Price", price);
                                    cmd.Parameters.AddWithValue("@CategoryID", categoryId ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@EditionTypeID", editionTypeId);
                                    cmd.Parameters.AddWithValue("@FormatID", formatId ?? (object)DBNull.Value);

                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }

                            transaction.Commit();

                            // Обновляем максимальный номер для следующего раза
                            _currentMaxInventoryNumber = startNumber + quantity - 1;

                            // Предлагаем сформировать акт поступления
                            var actResult = CustomMessageBox.Show(
                                $"Успешно добавлено {successCount} экземпляров книг!\n" +
                                $"Инвентарные номера: {startNumber:D6} - {startNumber + quantity - 1:D6}\n\n" +
                                "Сформировать акт поступления?",
                                "Успех", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

                            if (actResult == CustomMessageBoxResult.Yes)
                            {
                                GenerateReceiptAct();
                            }

                            ClearForm();
                        }
                        catch (SqlException ex) when (ex.Number == 2627)
                        {
                            transaction.Rollback();
                            CustomMessageBox.Show("Ошибка: один из инвентарных номеров уже существует.\n" +
                                                  "Проверьте начальный номер и попробуйте снова.",
                                "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
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
                CustomMessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // ========== ОЧИСТКА ФОРМЫ ==========

        /// <summary>
        /// Очистка всех полей формы
        /// </summary>
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
            StartInventoryBox.Clear();

            if (ReceiptCombo.Items.Count > 0)
                ReceiptCombo.SelectedIndex = 0;

            // Обновляем максимальный номер для корректной генерации
            _ = LoadMaxInventoryNumber();
            UpdateInventoryPreview();
        }

        /// <summary>
        /// Обработчик кнопки очистки
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        /// <summary>
        /// Обработчик кнопки формирования акта
        /// </summary>
        private void GenerateActButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateReceiptAct();
        }

        // ========== ОБРАБОТЧИКИ СОБЫТИЙ ПОЛЕЙ ==========

        private void PriceBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInventoryPreview();
        }

        private void StartInventoryBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ограничиваем длину 6 символами
            if (StartInventoryBox.Text.Length > 6)
            {
                StartInventoryBox.Text = StartInventoryBox.Text.Substring(0, 6);
                StartInventoryBox.CaretIndex = StartInventoryBox.Text.Length;
            }
            UpdateInventoryPreview();
        }

        private void ReceiptCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // ========== КНОПКИ ДОБАВЛЕНИЯ СПРАВОЧНИКОВ ==========

        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddAuthor());
        }

        private void AddEditionType_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddEditionType());
        }

        private void AddPublisher_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddPublishingHouse());
        }

        private void AddGenre_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddGener());
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddBookCategory());
        }

        private void AddFormat_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddPublicationFormat());
        }

        private void AddStatus_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddStatusBook());
        }

        /// <summary>
        /// Добавление ответственного пользователя (открывает страницу AddUser)
        /// </summary>
        private void AddResponsibleUser_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddUser());
        }

        /// <summary>
        /// Добавление поставщика (открывает страницу AddSupplier)
        /// </summary>
        private void AddSupplier_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddSupplier());
        }

        /// <summary>
        /// Загрузка страницы
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}