using Diplom_Bibl.Controls;
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
    public partial class AddEditionType : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<EditionTypeItem> _allEditionType;
        private int?_currentEditionTypeId = null;

        public class EditionTypeItem
        {
            public int ID_EditionType { get; set; }
            public string EditionTypeName { get; set; } = "";
            public string Description { get; set; } = "";
 
        }

        public AddEditionType()
        {
            InitializeComponent();
            EditionType();
        }

        private void EditionType()
        {
            try
            {
                const string query = @"
                    SELECT ID_EditionType, EditionTypeName, ISNULL(Description, '') AS Description
                    FROM EditionTypes
                    ORDER BY EditionTypeName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<EditionTypeItem>();
                        while (reader.Read())
                        {
                            items.Add(new EditionTypeItem
                            {
                                ID_EditionType = Convert.ToInt32(reader["ID_EditionType"]),
                                EditionTypeName = reader["EditionTypeName"].ToString(),
                                Description = reader["Description"].ToString()
                            });
                        }
                        _allEditionType = items;
                        DgEditionType.ItemsSource = _allEditionType;
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
            TbEditionTypeName.Clear();
            TbDescription.Clear();
           _currentEditionTypeId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbEditionTypeName.Text))
            {
                CustomMessageBox.Show("Введите название типа пособия.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private bool IsNameUnique(string name, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM EditionTypes WHERE EditionTypeName = @Name";
            if (excludeId.HasValue) query += " AND ID_EditionType != @ExcludeId";

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

            string name = TbEditionTypeName.Text.Trim();
            if (!IsNameUnique(name))
            {
                CustomMessageBox.Show("Тип пособия с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

          

            try
            {
                const string query = @"
                    INSERT INTO EditionTypes (EditionTypeName, Description)
                    VALUES (@Name, @Description)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", TbDescription.Text?.Trim() ?? (object)DBNull.Value);
                      
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Тип пособия добавлен.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        EditionType();
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
            if (!_currentEditionTypeId.HasValue)
            {
                CustomMessageBox.Show("Выберите тип пособия для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string name = TbEditionTypeName.Text.Trim();
            if (!IsNameUnique(name,_currentEditionTypeId))
            {
                CustomMessageBox.Show("Тип пособия с таким названием уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

        

            try
            {
                const string query = @"
                    UPDATE BookCategories 
                    SET EditionTypeName = @Name, Description = @Description
                    WHERE ID_EditionType = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Description", TbDescription.Text?.Trim() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id",_currentEditionTypeId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Тип пособия обновлен.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        EditionType();
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
            if (!_currentEditionTypeId.HasValue)
            {
                CustomMessageBox.Show("Выберите тип пособия для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка использования
            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Books WHERE EditionTypeID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id",_currentEditionTypeId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить: тип пособия используется в книгах.", "Удаление запрещено",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Удалить тип пособия?", "Подтверждение",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM EditionTypes WHERE ID_EditionType = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id",_currentEditionTypeId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Тип пособия удален.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        EditionType();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DgEditionType_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgEditionType.SelectedItem is EditionTypeItem selected)
            {
               _currentEditionTypeId = selected.ID_EditionType;
                TbEditionTypeName.Text = selected.EditionTypeName;
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
            EditionType();
        }
    }
}