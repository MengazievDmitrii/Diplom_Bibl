using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
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
        }

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