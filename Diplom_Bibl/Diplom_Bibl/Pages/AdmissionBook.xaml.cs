using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public partial class AdmissionBook : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private int? _currentReceiptId = null;
        private ObservableCollection<dynamic> _allReceipts;

        private class UserItem
        {
            public int ID_User { get; set; }
            public string FullName { get; set; } = "";
        }

        private class SupplierItem
        {
            public int ID_Supplier { get; set; }
            public string SupplierName { get; set; } = "";
        }

        public AdmissionBook()
        {
            InitializeComponent();
            LoadResponsibleUsers();
            LoadSuppliers();
            LoadReceipts();
            PrepareNewReceipt();
        }

        private string GenerateReceiptNumber(int nextId) => $"ПН-{nextId:D4}";

        private void LoadResponsibleUsers()
        {
            try
            {
                const string sql = @"
                    select u.ID_User, Surname + ' ' + Name + ISNULL(' ' + Patronymic, '') AS FullName from Users u
inner join UserRolesAssignments ura on ura.ID_User=u.ID_User
inner join UserRoles ur on ur.ID_Role=ura.ID_Role
where ur.RoleCode= 'LIBRARIAN'
ORDER BY Surname, Name;";

                var users = new ObservableCollection<UserItem>();
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new UserItem
                            {
                                ID_User = Convert.ToInt32(reader["ID_User"]),
                                FullName = reader["FullName"].ToString()
                            });
                        }
                    }
                }
                CbResponsible.ItemsSource = users;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки ответственных лиц: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                CbResponsible.ItemsSource = new ObservableCollection<UserItem>();
            }
        }

        private void LoadSuppliers()
        {
            try
            {
                // Проверяем существование таблицы Suppliers
                bool tableExists = false;
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Suppliers'", conn))
                    {
                        tableExists = (int)cmd.ExecuteScalar() > 0;
                    }
                }

                if (!tableExists)
                {
                    System.Diagnostics.Debug.WriteLine("Таблица Suppliers не существует");
                    CbSupplier.ItemsSource = new ObservableCollection<SupplierItem>();
                    CbSupplier.IsEnabled = false;
                    return;
                }

                const string sql = "SELECT ID_Supplier, SupplierName FROM Suppliers ORDER BY SupplierName";
                var suppliers = new ObservableCollection<SupplierItem>();
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            suppliers.Add(new SupplierItem
                            {
                                ID_Supplier = Convert.ToInt32(reader["ID_Supplier"]),
                                SupplierName = reader["SupplierName"].ToString()
                            });
                        }
                    }
                }
                CbSupplier.ItemsSource = suppliers;
                CbSupplier.DisplayMemberPath = "SupplierName";
                CbSupplier.SelectedValuePath = "ID_Supplier";
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки поставщиков: {ex.Message}\n\nТаблица может отсутствовать в базе данных.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                CbSupplier.ItemsSource = new ObservableCollection<SupplierItem>();
                CbSupplier.IsEnabled = false;
            }
        }

        private void PrepareNewReceipt()
        {
            int nextId = 1;
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT ISNULL(MAX(IDReceipt), 0) + 1 FROM ReceiptBooks", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int maxId))
                            nextId = maxId;
                    }
                }
            }
            catch { }

            TbReceiptNumber.Text = GenerateReceiptNumber(nextId);
            TbReceiptDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            if (CbResponsible.Items.Count > 0) CbResponsible.SelectedIndex = 0;
            if (CbSupplier.ItemsSource != null && CbSupplier.Items.Count > 0) CbSupplier.SelectedIndex = 0;

            TbComment.Clear();

            _currentReceiptId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private void LoadReceipts()
        {
            try
            {
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

                string query;
                if (hasSupplierColumn)
                {
                    query = @"
                        SELECT r.IDReceipt, r.ReceiptNumber, r.ReceiptDate, 
                               u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS ResponsiblePerson,
                               ISNULL(s.SupplierName, '') AS SupplierName,
                               r.Comment
                        FROM ReceiptBooks r
                        LEFT JOIN Users u ON r.ID_ResponsibleUser = u.ID_User
                        LEFT JOIN Suppliers s ON r.SupplierID = s.ID_Supplier
                        ORDER BY r.ReceiptDate DESC";
                }
                else
                {
                    query = @"
                        SELECT r.IDReceipt, r.ReceiptNumber, r.ReceiptDate, 
                               u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS ResponsiblePerson,
                               '' AS SupplierName,
                               r.Comment
                        FROM ReceiptBooks r
                        LEFT JOIN Users u ON r.ID_ResponsibleUser = u.ID_User
                        ORDER BY r.ReceiptDate DESC";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var list = new ObservableCollection<dynamic>();
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                IDReceipt = reader["IDReceipt"],
                                ReceiptNumber = reader["ReceiptNumber"].ToString(),
                                ReceiptDate = Convert.ToDateTime(reader["ReceiptDate"]),
                                ResponsiblePerson = reader["ResponsiblePerson"].ToString(),
                                SupplierName = reader["SupplierName"].ToString(),
                                Comment = reader["Comment"]?.ToString() ?? ""
                            });
                        }
                        _allReceipts = list;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                _allReceipts = new ObservableCollection<dynamic>();
                LvReceipts.ItemsSource = _allReceipts;
            }
        }

        private void ApplyFilter(string term)
        {
            if (_allReceipts == null) return;

            var filtered = _allReceipts.Where(r =>
                string.IsNullOrEmpty(term) ||
                r.ReceiptNumber.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.ResponsiblePerson.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.SupplierName.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.Comment.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            LvReceipts.ItemsSource = filtered;
        }

        private bool ValidateForm()
        {
            if (CbResponsible.SelectedValue == null)
            {
                CustomMessageBox.Show("Выберите ответственное лицо.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                int responsibleId = (int)CbResponsible.SelectedValue;
                int? supplierId = CbSupplier.SelectedValue as int?;
                string comment = TbComment.Text?.Trim();

                string number = TbReceiptNumber.Text;
                DateTime date = DateTime.Now;

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
                        VALUES (@Number, @Date, @ResponsibleID, @SupplierID, @Comment);";
                }
                else
                {
                    insertQuery = @"
                        INSERT INTO ReceiptBooks (ReceiptNumber, ReceiptDate, ID_ResponsibleUser, Comment)
                        VALUES (@Number, @Date, @ResponsibleID, @Comment);";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Number", number);
                        cmd.Parameters.AddWithValue("@Date", date);
                        cmd.Parameters.AddWithValue("@ResponsibleID", responsibleId);
                        if (hasSupplierColumn)
                            cmd.Parameters.AddWithValue("@SupplierID", supplierId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Накладная успешно создана.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        LoadReceipts();
                        PrepareNewReceipt();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReceiptId.HasValue)
            {
                CustomMessageBox.Show("Нет выбранной записи.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            try
            {
                int responsibleId = (int)CbResponsible.SelectedValue;
                int? supplierId = CbSupplier.SelectedValue as int?;
                string comment = TbComment.Text?.Trim();

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

                string updateQuery;
                if (hasSupplierColumn)
                {
                    updateQuery = @"
                        UPDATE ReceiptBooks 
                        SET ID_ResponsibleUser = @ResponsibleID, 
                            SupplierID = @SupplierID, 
                            Comment = @Comment
                        WHERE IDReceipt = @Id;";
                }
                else
                {
                    updateQuery = @"
                        UPDATE ReceiptBooks 
                        SET ID_ResponsibleUser = @ResponsibleID, 
                            Comment = @Comment
                        WHERE IDReceipt = @Id;";
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ResponsibleID", responsibleId);
                        if (hasSupplierColumn)
                            cmd.Parameters.AddWithValue("@SupplierID", supplierId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", _currentReceiptId.Value);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            CustomMessageBox.Show("Изменения сохранены.", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            LoadReceipts();
                            PrepareNewReceipt();
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись не найдена.", "Ошибка",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReceiptId.HasValue) return;

            var result = CustomMessageBox.Show("Удалить накладную?", "Подтверждение",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);
            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM ReceiptBooks WHERE IDReceipt = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentReceiptId.Value);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            CustomMessageBox.Show("Накладная удалена.", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            LoadReceipts();
                            PrepareNewReceipt();
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись уже удалена.", "Внимание",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LvReceipts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvReceipts.SelectedItem == null) return;

            try
            {
                dynamic item = LvReceipts.SelectedItem;
                _currentReceiptId = item.IDReceipt;
                TbReceiptNumber.Text = item.ReceiptNumber;
                TbReceiptDate.Text = ((DateTime)item.ReceiptDate).ToString("dd.MM.yyyy HH:mm");

                // Установка ответственного лица
                CbResponsible.SelectedValue = GetUserIdByName(item.ResponsiblePerson);

                // Установка поставщика
                string supplierName = item.SupplierName?.ToString() ?? "";
                if (!string.IsNullOrEmpty(supplierName))
                {
                    CbSupplier.SelectedValue = GetSupplierIdByName(supplierName);
                }
                else
                {
                    CbSupplier.SelectedIndex = 0;
                }

                TbComment.Text = item.Comment;

                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки записи: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private int? GetUserIdByName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            try
            {
                const string sql = @"
                    SELECT TOP 1 ID_User FROM Users 
                    WHERE Surname + ' ' + Name + ISNULL(' ' + Patronymic, '') = @FullName";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", fullName);
                        var result = cmd.ExecuteScalar();
                        return result == null ? null : (int?)Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private int? GetSupplierIdByName(string supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName)) return null;
            try
            {
                const string sql = "SELECT TOP 1 ID_Supplier FROM Suppliers WHERE SupplierName = @SupplierName";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SupplierName", supplierName);
                        var result = cmd.ExecuteScalar();
                        return result == null ? null : (int?)Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchBox.Text.Trim());
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new AddUser());
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new AddSupplier());
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadResponsibleUsers();
            LoadSuppliers();
            LoadReceipts();
        }
    }
}