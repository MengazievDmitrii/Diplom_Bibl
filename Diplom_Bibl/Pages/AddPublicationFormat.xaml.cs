using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
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
    public partial class AddPublicationFormat : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<FormatDisplayItem> _allFormats;
        private int? _currentFormatId = null;
        private bool _isPlaceholderActive = true;

        public class FormatDisplayItem
        {
            public int ID_Format { get; set; }
            public string FormatName { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public AddPublicationFormat()
        {
            InitializeComponent();
            LoadFormats();
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

        private void LoadFormats()
        {
            try
            {
                const string query = @"
                    SELECT ID_Format, FormatName, ISNULL(Description, '') AS Description
                    FROM PublicationFormats
                    ORDER BY FormatName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<FormatDisplayItem>();
                        while (reader.Read())
                        {
                            items.Add(new FormatDisplayItem
                            {
                                ID_Format = Convert.ToInt32(reader["ID_Format"]),
                                FormatName = reader["FormatName"].ToString(),
                                Description = reader["Description"].ToString()
                            });
                        }
                        _allFormats = items;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки форматов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplyFilter(string searchTerm)
        {
            if (_allFormats == null) return;

            var filtered = _allFormats.Where(f =>
                string.IsNullOrEmpty(searchTerm) ||
                f.FormatName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            DgFormats.ItemsSource = filtered;
        }

        private void ClearForm()
        {
            TbFormatName.Clear();
            TbDescription.Clear();
            _currentFormatId = null;

            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbFormatName.Text))
            {
                CustomMessageBox.Show("Введите название формата.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbFormatName.Focus();
                return false;
            }
            return true;
        }

        private bool IsNameUnique(string name, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM PublicationFormats WHERE FormatName = @Name";
            if (excludeId.HasValue) query += " AND ID_Format != @ExcludeId";

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

            string name = TbFormatName.Text.Trim();

            if (!IsNameUnique(name))
            {
                CustomMessageBox.Show("Формат с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    INSERT INTO PublicationFormats (FormatName, Description)
                    VALUES (@Name, @Description)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.ExecuteNonQuery();

                        CustomMessageBox.Show("Формат издания добавлен.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFormats();
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
            if (!_currentFormatId.HasValue)
            {
                CustomMessageBox.Show("Выберите формат для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string name = TbFormatName.Text.Trim();

            if (!IsNameUnique(name, _currentFormatId))
            {
                CustomMessageBox.Show("Формат с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    UPDATE PublicationFormats 
                    SET FormatName = @Name, Description = @Description
                    WHERE ID_Format = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@Id", _currentFormatId.Value);
                        cmd.ExecuteNonQuery();

                        CustomMessageBox.Show("Формат обновлён.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFormats();
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
            if (!_currentFormatId.HasValue)
            {
                CustomMessageBox.Show("Выберите формат для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка, используется ли формат в книгах
            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Books WHERE FormatID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", _currentFormatId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить формат: он используется в книгах.",
                    "Удаление запрещено", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить этот формат?",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM PublicationFormats WHERE ID_Format = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentFormatId.Value);
                        cmd.ExecuteNonQuery();

                        CustomMessageBox.Show("Формат удалён.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFormats();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DgFormats_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgFormats.SelectedItem is FormatDisplayItem selected)
            {
                _currentFormatId = selected.ID_Format;
                TbFormatName.Text = selected.FormatName;
                TbDescription.Text = selected.Description;

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
            LoadFormats();
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}