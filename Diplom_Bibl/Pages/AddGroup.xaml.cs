using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddGroup.xaml
    /// </summary>
    public partial class AddGroup : Page
    {
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;"; // ЗАМЕНИТЬ!
        private int? _currentGroupId = null; // Переменная для хранения ID текущей загруженной записи
        private System.Collections.ObjectModel.ObservableCollection<object> _allGroups;
        public AddGroup()
        {
            InitializeComponent();
            LoadGroups();
            LoadSpecialities();
        }

       
            private void LoadGroups()
        {
            try
            {
                const string query = @"
            SELECT 
                g.GroupID,
                g.GroupCode,
                g.SpecialityID,
                s.SpecialityCode AS SpecCode,
                s.NameSpeciality AS SpecName
            FROM Groups g
            LEFT JOIN Speciality s ON g.SpecialityID = s.SpecialityID
            ORDER BY g.GroupCode;";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var groups = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            groups.Add(new
                            {
                                GroupID = reader["GroupID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["GroupID"]),
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                SpecialityID = reader["SpecialityID"] is int sid ? sid : (int?)null,

                                // Отображаем специальность: если есть — "Код — Название", иначе "(не указана)"
                                SpecialityDisplay = reader["SpecCode"] != DBNull.Value
                                    ? $"{reader["SpecCode"]} — {reader["SpecName"]}"
                                    : "(специальность не указана)"
                            });
                        }

                        // Привяжите к вашему ListView (например, LvGroups)
                        LvGroups.ItemsSource = groups;
                        _allGroups = groups; // ← сохраняем ПОЛНЫЙ список
                        ApplyGroupFilter(""); // ← сразу показываем все
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                LvGroups.ItemsSource = null;
            }
        }
        private void ApplyGroupFilter(string searchTerm)
        {
            if (_allGroups == null)
            {
                LvGroups.ItemsSource = null;
                return;
            }

            var filtered = _allGroups
                .Where(g =>
                {
                    // Получаем GroupCode через рефлексию (т.к. анонимный тип)
                    var code = g.GetType().GetProperty("GroupCode")?.GetValue(g)?.ToString() ?? "";
                    return string.IsNullOrEmpty(searchTerm)
                        || code.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            LvGroups.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }

        private void LoadSpecialities()
        {
            try
            {
                const string query = @"
                    SELECT SpecialityID, SpecialityCode, NameSpeciality 
                    FROM Speciality 
                    ORDER BY SpecialityCode";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<dynamic>();

                        while (reader.Read())
                        {
                            // 🔹 Безопасное чтение ID — даже если SpecialityID — строка с числом
                            int id = reader["SpecialityID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["SpecialityID"]);

                            string code = reader["SpecialityCode"]?.ToString() ?? "";
                            string name = reader["NameSpeciality"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = id,
                                Display = $"{code} — {name}"
                            });
                        }

                        NameSpecialityCB.ItemsSource = items;
                        NameSpecialityCB.DisplayMemberPath = "Display";
                        NameSpecialityCB.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки специальностей:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NameSpecialityCB.ItemsSource = null;
            }
        }

        private void ClearFields()
        {
            CodeGroup.Clear();
            NameSpecialityCB.SelectedIndex = -1;
            _currentGroupId = null;

            SaveNewRecordButton.Visibility = Visibility.Visible;
            SaveChangesButton.Visibility = Visibility.Collapsed;
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            //GroupIDLabel.Content = _currentGroupId.Value;
            string groupCode = CodeGroup.Text?.Trim();
            if (string.IsNullOrEmpty(groupCode))
            {
                MessageBox.Show("Введите код группы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🔹 ФИКС: получаем и проверяем SelectedValue отдельно
            object selectedValue = NameSpecialityCB.SelectedValue;
            if (selectedValue is int specialityId)
            {
                const string insertQuery = @"
        INSERT INTO Groups (GroupCode, SpecialityID) 
        VALUES (@GroupCode, @SpecialityID);";

                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@GroupCode", groupCode);
                            command.Parameters.AddWithValue("@SpecialityID", specialityId); // ← Теперь OK

                            int rows = command.ExecuteNonQuery();
                            if (rows > 0)
                            {
                                MessageBox.Show($"Группа «{groupCode}» успешно добавлена.",
                                    "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                                ClearFields();
                                LoadGroups();
                            }
                            else
                            {
                                MessageBox.Show("Не удалось добавить группу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 547)
                {
                    MessageBox.Show("Группа с таким кодом уже существует или нарушено ограничение целостности.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении группы:\n{ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите специальность.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // --- Обработчик сохранения изменений существующей записи ---
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {

            if (!_currentGroupId.HasValue)
            {
                MessageBox.Show("Нет выбранной группы для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string groupCode = CodeGroup.Text?.Trim();
            if (string.IsNullOrEmpty(groupCode))
            {
                MessageBox.Show("Введите код группы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка уникальности (исключая текущую группу)
            bool groupExists = false;
            string checkQuery = "SELECT COUNT(*) FROM Groups WHERE GroupCode = @GroupCode AND GroupID != @GroupId";

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(checkQuery, connection))
                {
                    command.Parameters.AddWithValue("@GroupCode", groupCode);
                    command.Parameters.AddWithValue("@GroupId", _currentGroupId.HasValue ? _currentGroupId.Value : 0);

                    int count = (int)command.ExecuteScalar();
                    groupExists = count > 0;
                }
            }

            if (groupExists)
            {
                MessageBox.Show("Группа с таким кодом уже существует.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем ID специальности (может быть null)
            int? specialityId = NameSpecialityCB.SelectedValue as int?;

            const string updateQuery = @"
        UPDATE Groups 
        SET GroupCode = @GroupCode, SpecialityID = @SpecialityID 
        WHERE GroupID = @GroupID;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.Add("@GroupCode", SqlDbType.NVarChar, 20).Value = groupCode;
                        command.Parameters.Add("@SpecialityID", SqlDbType.Int).Value =
                            specialityId ?? (object)DBNull.Value;
                        command.Parameters.Add("@GroupID", SqlDbType.SmallInt).Value = _currentGroupId.Value;

                        int rows = command.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            MessageBox.Show("Группа успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.None);
                            ClearFields();
                            LoadGroups();
                        }
                        else
                        {
                            MessageBox.Show("Группа не найдена (возможно, удалена).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

       
        // 5. УДАЛЕНИЕ ГРУППЫ
       
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {

            if (!_currentGroupId.HasValue)
            {
                MessageBox.Show("Нет выбранной группы для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить эту группу?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            const string deleteQuery = "DELETE FROM Groups WHERE GroupID = @GroupID;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@GroupID", _currentGroupId.Value);
                        int rows = command.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            MessageBox.Show("Группа удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearFields();
                            LoadGroups();
                        }
                        else
                        {
                            MessageBox.Show("Группа уже удалена.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show("Невозможно удалить группу: на неё ссылаются другие записи (например, студенты).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

       
        // 6. ЗАГРУЗКА ГРУППЫ ПО ДВОЙНОМУ КЛИКУ (предполагается, что у вас есть LvGroups)
       
        // ⚠️ У вас сейчас LvSpecialities_MouseDoubleClick — но редактируем мы ГРУППЫ!
        // Если вы хотите редактировать ГРУППУ по двойному клику — переименуйте обработчик и привяжите к LvGroups.
        private void LvGroups_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Пример: если у вас ListView с ItemsSource = List<GroupPreview>
            // и GroupPreview = { GroupID, GroupCode, SpecialityID }
            var item = (sender as ListView)?.SelectedItem;
            if (item == null) return;

            // Если вы привязали через анонимный тип или простой класс — читаем свойства:
            // Здесь предположим, что вы используете dynamic или простой класс с полями
            dynamic groupItem = item;

            try
            {
                _currentGroupId = (int)groupItem.GroupID;
                CodeGroup.Text = groupItem.GroupCode?.ToString() ?? "";
                NameSpecialityCB.SelectedValue = (int?)groupItem.SpecialityID ?? -1;

                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке группы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.GoBack();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Пропускаем placeholder
            string term = SearchBox.Text;
            if (term == "Поиск по коду группы...") term = "";
            ApplyGroupFilter(term);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Gray;
            }
        }
    }
    
}


