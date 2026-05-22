using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Diplom_Bibl.Helpers;


namespace Diplom_Bibl.Pages
{
    public partial class AddBookCategory : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<CategoryItem> _allCategories;
        private int? _currentCategoryId = null;

        public class CategoryItem
        {
            public int ID_Category { get; set; }
            public string CategoryName { get; set; } = "";
            public string Description { get; set; } = "";
          
        }

        public AddBookCategory()
        {
            InitializeComponent();
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                const string query = @"
                    SELECT ID_Category, CategoryName, ISNULL(Description, '') AS Description
                    FROM BookCategories
                    ORDER BY CategoryName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<CategoryItem>();
                        while (reader.Read())
                        {
                            items.Add(new CategoryItem
                            {
                                ID_Category = Convert.ToInt32(reader["ID_Category"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                Description = reader["Description"].ToString()
                            });
                        }
                        _allCategories = items;
                        DgCategories.ItemsSource = _allCategories;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            TbCategoryName.Clear();
            TbDescription.Clear();
            _currentCategoryId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbCategoryName.Text))
            {
                CustomMessageBox.Show("Введите название категории.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private bool IsNameUnique(string name, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM BookCategories WHERE CategoryName = @Name";
            if (excludeId.HasValue) query += " AND ID_Category != @ExcludeId";

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

        private async void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            string name = TbCategoryName.Text.Trim();
            if (!IsNameUnique(name))
            {
                CustomMessageBox.Show("Категория с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

           

            try
            {
                const string query = @"
                    INSERT INTO BookCategories (CategoryName, Description)
                    VALUES (@Name, @Description)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", TbDescription.Text?.Trim() ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Категория добавлена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadCategories();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentCategoryId.HasValue)
            {
                CustomMessageBox.Show("Выберите категорию для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string name = TbCategoryName.Text.Trim();
            if (!IsNameUnique(name, _currentCategoryId))
            {
                CustomMessageBox.Show("Категория с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    UPDATE BookCategories 
                    SET CategoryName = @Name, Description = @Description
                    WHERE ID_Category = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", TbDescription.Text?.Trim() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", _currentCategoryId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Категория обновлена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadCategories();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentCategoryId.HasValue)
            {
                CustomMessageBox.Show("Выберите категорию для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка использования
            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Books WHERE CategoryID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", _currentCategoryId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить: категория используется в книгах.", "Удаление запрещено",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Удалить категорию?", "Подтверждение",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM BookCategories WHERE ID_Category = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentCategoryId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Категория удалена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadCategories();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DgCategories_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgCategories.SelectedItem is CategoryItem selected)
            {
                _currentCategoryId = selected.ID_Category;
                TbCategoryName.Text = selected.CategoryName;
                TbDescription.Text = selected.Description;
                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
        }
    }
}