using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class AddSupplier : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<SupplierDisplayItem> _allSuppliers;
        private int? _currentSupplierId = null;
        private bool _isPlaceholderActive = true;

        public class SupplierDisplayItem
        {
            public int ID_Supplier { get; set; }
            public string SupplierName { get; set; } = "";
            public string INN { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Email { get; set; } = "";
            public string Address { get; set; } = "";
            public string Comment { get; set; } = "";
        }

        public AddSupplier()
        {
            InitializeComponent();
            LoadSuppliers();
            SetSearchPlaceholder();
            SubscribeValidationEvents();
        }

        private void SubscribeValidationEvents()
        {
            // Маска для ИНН - только цифры
            TbINN.PreviewTextInput += TbINN_PreviewTextInput;
            TbINN.TextChanged += TbINN_TextChanged;
            TbINN.LostFocus += TbINN_LostFocus;

            // Маска для Email - только английские буквы, цифры и специальные символы
            TbEmail.PreviewTextInput += TbEmail_PreviewTextInput;
            TbEmail.TextChanged += TbEmail_TextChanged;
            TbEmail.LostFocus += TbEmail_LostFocus;

            // Валидация названия поставщика
            TbSupplierName.LostFocus += TbSupplierName_LostFocus;
            TbSupplierName.PreviewKeyDown += TbSupplierName_PreviewKeyDown;

            // Валидация адреса
            TbAddress.LostFocus += TbAddress_LostFocus;
        }

        // ==================== МАСКА ДЛЯ НАЗВАНИЯ ПОСТАВЩИКА ====================
        private void TbSupplierName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Разрешаем вставку из буфера, но будем проверять при потере фокуса
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                return; // Разрешаем вставку

            // Остальные клавиши не блокируем
        }

        private void TbSupplierName_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // Проверяем допустимые символы
            if (!Regex.IsMatch(text, @"^[a-zA-Zа-яА-ЯёЁ0-9\s\-.,'`()]+$"))
            {
                CustomMessageBox.Show("Название поставщика содержит недопустимые символы.\n" +
                    "Разрешены: буквы (русские/английские), цифры, пробелы, дефисы, точки, запятые, кавычки, скобки.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Focus();
            }
        }

        // ==================== МАСКА ДЛЯ АДРЕСА ====================
        private void TbAddress_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string address = textBox.Text.Trim();
            if (string.IsNullOrEmpty(address)) return;

            // Проверяем допустимые символы
            if (!Regex.IsMatch(address, @"^[a-zA-Zа-яА-ЯёЁ0-9\s\-.,/\\'`()]+$"))
            {
                CustomMessageBox.Show("Адрес содержит недопустимые символы.\n" +
                    "Разрешены: буквы (русские/английские), цифры, пробелы, дефисы, точки, запятые, слеши.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Focus();
            }
        }

        // ==================== МАСКА ВВОДА ДЛЯ ИНН ====================
        private void TbINN_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void TbINN_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Ограничиваем длину ИНН до 12 символов
            if (textBox.Text.Length > 12)
            {
                textBox.Text = textBox.Text.Substring(0, 12);
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void TbINN_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string inn = textBox.Text.Trim();
            if (string.IsNullOrEmpty(inn)) return;

            // Проверка - только цифры
            if (!Regex.IsMatch(inn, @"^\d+$"))
            {
                CustomMessageBox.Show("ИНН может содержать только цифры.", "Некорректный ИНН",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Clear();
                textBox.Focus();
                return;
            }

            // Проверка длины ИНН (должен быть 10 или 12 цифр)
            if (inn.Length != 10 && inn.Length != 12)
            {
                CustomMessageBox.Show("ИНН должен содержать 10 или 12 цифр.", "Некорректный ИНН",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Clear();
                textBox.Focus();
                return;
            }
        }

        // ==================== МАСКА ВВОДА ДЛЯ EMAIL ====================
        private void TbEmail_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только английские буквы, цифры и специальные символы для email
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z0-9@._\-+=]+$");
        }

        private void TbEmail_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Ограничиваем длину email до 100 символов
            if (textBox.Text.Length > 100)
            {
                textBox.Text = textBox.Text.Substring(0, 100);
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void TbEmail_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string email = textBox.Text.Trim();
            if (string.IsNullOrEmpty(email)) return;

            // Дополнительная проверка на недопустимые символы
            if (!Regex.IsMatch(email, @"^[a-zA-Z0-9@._\-+=]+$"))
            {
                CustomMessageBox.Show("Email может содержать только:\n" +
                    "- английские буквы a-z, A-Z\n" +
                    "- цифры 0-9\n" +
                    "- символы @ . _ - + =",
                    "Недопустимые символы", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Clear();
                textBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                CustomMessageBox.Show("Введите корректный адрес электронной почты.\n" +
                    "Пример: name@domain.ru",
                    "Некорректный Email",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                textBox.Clear();
                textBox.Focus();
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Проверка формата email
                string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ==================== ОСТАЛЬНЫЕ МЕТОДЫ ====================

        private void SetSearchPlaceholder()
        {
            SearchBox.Text = "Поиск по названию...";
            SearchBox.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
        }

        private void ClearPlaceholder()
        {
            if (_isPlaceholderActive)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void LoadSuppliers()
        {
            try
            {
                const string query = @"
                    SELECT ID_Supplier, SupplierName, ISNULL(INN, '') AS INN,
                           ISNULL(Phone, '') AS Phone, ISNULL(Email, '') AS Email,
                           ISNULL(Address, '') AS Address, ISNULL(Comment, '') AS Comment
                    FROM Suppliers
                    ORDER BY SupplierName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<SupplierDisplayItem>();

                        while (reader.Read())
                        {
                            items.Add(new SupplierDisplayItem
                            {
                                ID_Supplier = Convert.ToInt32(reader["ID_Supplier"]),
                                SupplierName = reader["SupplierName"].ToString(),
                                INN = reader["INN"].ToString(),
                                Phone = reader["Phone"].ToString(),
                                Email = reader["Email"].ToString(),
                                Address = reader["Address"].ToString(),
                                Comment = reader["Comment"].ToString()
                            });
                        }

                        _allSuppliers = items;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки поставщиков: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplyFilter(string searchTerm)
        {
            if (_allSuppliers == null) return;

            var filtered = _allSuppliers.Where(s =>
                string.IsNullOrEmpty(searchTerm) ||
                s.SupplierName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            LvSuppliers.ItemsSource = filtered;
        }

        private void ClearForm()
        {
            TbSupplierName.Clear();
            TbINN.Clear();
            TbPhone.Clear();
            TbEmail.Clear();
            TbAddress.Clear();
            TbComment.Clear();
            _currentSupplierId = null;

            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbSupplierName.Text))
            {
                CustomMessageBox.Show("Введите название поставщика.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbSupplierName.Focus();
                return false;
            }

            // Валидация email (если введен)
            string email = TbEmail.Text.Trim();
            if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
            {
                CustomMessageBox.Show("Введите корректный адрес электронной почты.\nПример: name@domain.ru",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbEmail.Focus();
                return false;
            }

            // Валидация ИНН (если введен)
            string inn = TbINN.Text.Trim();
            if (!string.IsNullOrEmpty(inn))
            {
                if (!Regex.IsMatch(inn, @"^\d+$"))
                {
                    CustomMessageBox.Show("ИНН может содержать только цифры.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    TbINN.Focus();
                    return false;
                }
                if (inn.Length != 10 && inn.Length != 12)
                {
                    CustomMessageBox.Show("ИНН должен содержать 10 или 12 цифр.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    TbINN.Focus();
                    return false;
                }
            }

            return true;
        }

        private bool IsSupplierNameUnique(string name, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM Suppliers WHERE SupplierName = @Name";
            if (excludeId.HasValue)
                query += " AND ID_Supplier != @ExcludeId";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name.Trim());
                    if (excludeId.HasValue)
                        cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            string supplierName = TbSupplierName.Text.Trim();

            if (!IsSupplierNameUnique(supplierName))
            {
                CustomMessageBox.Show("Поставщик с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    INSERT INTO Suppliers (SupplierName, INN, Phone, Email, Address, Comment)
                    VALUES (@Name, @INN, @Phone, @Email, @Address, @Comment)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", supplierName);
                        cmd.Parameters.AddWithValue("@INN", string.IsNullOrWhiteSpace(TbINN.Text) ? (object)DBNull.Value : TbINN.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(TbPhone.Text) ? (object)DBNull.Value : TbPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(TbEmail.Text) ? (object)DBNull.Value : TbEmail.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(TbAddress.Text) ? (object)DBNull.Value : TbAddress.Text.Trim());
                        cmd.Parameters.AddWithValue("@Comment", string.IsNullOrWhiteSpace(TbComment.Text) ? (object)DBNull.Value : TbComment.Text.Trim());

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Поставщик успешно добавлен.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSuppliers();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSupplierId.HasValue)
            {
                CustomMessageBox.Show("Выберите поставщика для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string supplierName = TbSupplierName.Text.Trim();

            if (!IsSupplierNameUnique(supplierName, _currentSupplierId))
            {
                CustomMessageBox.Show("Поставщик с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    UPDATE Suppliers 
                    SET SupplierName = @Name, INN = @INN, Phone = @Phone, 
                        Email = @Email, Address = @Address, Comment = @Comment
                    WHERE ID_Supplier = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", supplierName);
                        cmd.Parameters.AddWithValue("@INN", string.IsNullOrWhiteSpace(TbINN.Text) ? (object)DBNull.Value : TbINN.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(TbPhone.Text) ? (object)DBNull.Value : TbPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(TbEmail.Text) ? (object)DBNull.Value : TbEmail.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(TbAddress.Text) ? (object)DBNull.Value : TbAddress.Text.Trim());
                        cmd.Parameters.AddWithValue("@Comment", string.IsNullOrWhiteSpace(TbComment.Text) ? (object)DBNull.Value : TbComment.Text.Trim());
                        cmd.Parameters.AddWithValue("@Id", _currentSupplierId.Value);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Данные поставщика обновлены.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSuppliers();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSupplierId.HasValue)
            {
                CustomMessageBox.Show("Выберите поставщика для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка, используется ли поставщик в накладных
            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ReceiptBooks WHERE SupplierID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", _currentSupplierId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить поставщика: он используется в приходных накладных.", "Удаление запрещено",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить этого поставщика?", "Подтверждение удаления",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM Suppliers WHERE ID_Supplier = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentSupplierId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Поставщик удалён.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSuppliers();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LvSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvSuppliers.SelectedItem is SupplierDisplayItem selected)
            {
                _currentSupplierId = selected.ID_Supplier;
                TbSupplierName.Text = selected.SupplierName;
                TbINN.Text = selected.INN;
                TbPhone.Text = selected.Phone;
                TbEmail.Text = selected.Email;
                TbAddress.Text = selected.Address;
                TbComment.Text = selected.Comment;

                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearPlaceholder();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SetSearchPlaceholder();
                ApplyFilter("");
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPlaceholderActive) return;
            ApplyFilter(SearchBox.Text.Trim());
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSuppliers();
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}