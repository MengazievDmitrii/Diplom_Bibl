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
    public partial class AddSourceType : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<SourceTypeDisplayItem> _allSourceTypes;
        private int? _currentSourceTypeId = null;
        private bool _isPlaceholderActive = true;

        public class SourceTypeDisplayItem
        {
            public int ID_SourceType { get; set; }
            public string SourceTypeName { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsActive { get; set; }
        }

        public AddSourceType()
        {
            InitializeComponent();
            LoadSourceTypes();
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

        private void LoadSourceTypes()
        {
            try
            {
                const string query = @"
                    SELECT ID_SourceType, SourceTypeName, ISNULL(Description, '') AS Description, IsActive
                    FROM SourceTypes
                    ORDER BY SourceTypeName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<SourceTypeDisplayItem>();

                        while (reader.Read())
                        {
                            items.Add(new SourceTypeDisplayItem
                            {
                                ID_SourceType = Convert.ToInt32(reader["ID_SourceType"]),
                                SourceTypeName = reader["SourceTypeName"].ToString(),
                                Description = reader["Description"].ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }

                        _allSourceTypes = items;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplyFilter(string searchTerm)
        {
            if (_allSourceTypes == null) return;

            var filtered = _allSourceTypes.Where(s =>
                string.IsNullOrEmpty(searchTerm) ||
                s.SourceTypeName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            LvSourceTypes.ItemsSource = filtered;
        }

        private void ClearForm()
        {
            TbSourceTypeName.Clear();
            TbDescription.Clear();
            ChkIsActive.IsChecked = true;
            _currentSourceTypeId = null;

            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbSourceTypeName.Text))
            {
                CustomMessageBox.Show("Введите название типа источника.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbSourceTypeName.Focus();
                return false;
            }
            return true;
        }

        private bool IsNameUnique(string name, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM SourceTypes WHERE SourceTypeName = @Name";
            if (excludeId.HasValue) query += " AND ID_SourceType != @ExcludeId";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name.Trim());
                    if (excludeId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            string name = TbSourceTypeName.Text.Trim();

            if (!IsNameUnique(name))
            {
                CustomMessageBox.Show("Тип источника с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    INSERT INTO SourceTypes (SourceTypeName, Description, IsActive)
                    VALUES (@Name, @Description, @IsActive)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@IsActive", ChkIsActive.IsChecked == true);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Тип источника добавлен.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSourceTypes();
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
            if (!_currentSourceTypeId.HasValue)
            {
                CustomMessageBox.Show("Выберите запись для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string name = TbSourceTypeName.Text.Trim();

            if (!IsNameUnique(name, _currentSourceTypeId))
            {
                CustomMessageBox.Show("Тип источника с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    UPDATE SourceTypes 
                    SET SourceTypeName = @Name, Description = @Description, IsActive = @IsActive
                    WHERE ID_SourceType = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@IsActive", ChkIsActive.IsChecked == true);
                        cmd.Parameters.AddWithValue("@Id", _currentSourceTypeId.Value);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Данные обновлены.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSourceTypes();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSourceTypeId.HasValue)
            {
                CustomMessageBox.Show("Выберите запись для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ReceiptBooks WHERE SourceTypeID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", _currentSourceTypeId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить: тип источника используется в приходных накладных.\nРекомендуется деактивировать запись.",
                    "Удаление запрещено", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить этот тип источника?",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM SourceTypes WHERE ID_SourceType = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentSourceTypeId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Запись удалена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadSourceTypes();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LvSourceTypes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvSourceTypes.SelectedItem is SourceTypeDisplayItem selected)
            {
                _currentSourceTypeId = selected.ID_SourceType;
                TbSourceTypeName.Text = selected.SourceTypeName;
                TbDescription.Text = selected.Description;
                ChkIsActive.IsChecked = selected.IsActive;

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
            LoadSourceTypes();
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}