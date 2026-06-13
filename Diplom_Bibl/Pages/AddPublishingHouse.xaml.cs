using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddPublishingHouse.xaml
    /// </summary>
    public partial class AddPublishingHouse : Page
    {
        private string connectionString = DatabaseHelper.ConnectionString;
        private int? _currentPublishingHouseId = null;
        private System.Collections.ObjectModel.ObservableCollection<object> _allPublishingHouses;

        public AddPublishingHouse()
        {
            InitializeComponent();
            PublishingHouses();

            // Подписка на события для маски ввода
            TitlePublishingHouseTb.PreviewTextInput += TitleField_PreviewTextInput;
            TitlePublishingHouseTb.PreviewKeyDown += TitleField_PreviewKeyDown;
        }

        /// <summary>
        /// Загрузка списка издательств из БД
        /// </summary>
        private void PublishingHouses()
        {
            try
            {
                const string query = @"
                    SELECT IDPublishingHouse, TitlePublishingHouse
                    FROM PublishingHouse 
                    ORDER BY TitlePublishingHouse";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            items.Add(new
                            {
                                IDPublishingHouse = reader["IDPublishingHouse"] is int id ? id : 0,
                                TitlePublishingHouse = reader["TitlePublishingHouse"]?.ToString() ?? ""
                            });
                        }

                        _allPublishingHouses = items;
                        ApplyPublishingHousesFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Фильтрация списка издательств
        /// </summary>
        private void ApplyPublishingHousesFilter(string searchTerm)
        {
            if (_allPublishingHouses == null)
            {
                LvPublishingHouse.ItemsSource = null;
                return;
            }

            var filtered = _allPublishingHouses
                .Where(item =>
                {
                    var title = item.GetType().GetProperty("TitlePublishingHouse")?.GetValue(item)?.ToString() ?? "";
                    return string.IsNullOrEmpty(searchTerm)
                        || title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            LvPublishingHouse.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }

        /// <summary>
        /// Проверка существования издательства в БД
        /// </summary>
        private async Task<bool> IsPublishingHouseExistsAsync(string title, int? excludeId = null)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM PublishingHouse WHERE TitlePublishingHouse = @Title";

                if (excludeId.HasValue)
                {
                    query += " AND IDPublishingHouse != @ExcludeId";
                }

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);

                        if (excludeId.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                        }

                        int count = (int)await cmd.ExecuteScalarAsync();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки существования: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверка, есть ли у издательства книги в библиотеке
        /// </summary>
        private async Task<bool> CheckPublishingHouseHasBooksAsync(int publishingHouseId)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM Books WHERE PublishingHouse = @PublishingHouseId";
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PublishingHouseId", publishingHouseId);
                        int count = (int)await cmd.ExecuteScalarAsync();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки книг издательства: {ex.Message}");
                return false;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PublishingHouses();
        }

        private void ClearInputFields()
        {
            TitlePublishingHouseTb.Clear();
            PublishingHouseIDLabel.Content = "";
        }

        /// <summary>
        /// Сохранение нового издательства (с проверкой на дубликат)
        /// </summary>
        private async void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitlePublishingHouseTb.Text?.Trim();

            if (string.IsNullOrEmpty(title))
            {
                CustomMessageBox.Show("Пожалуйста, введите название издательства.", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка на существование
            bool exists = await IsPublishingHouseExistsAsync(title);
            if (exists)
            {
                CustomMessageBox.Show($"Издательство \"{title}\" уже существует в базе данных.\n\n" +
                    "Пожалуйста, проверьте введённые данные.",
                    "Дубликат записи", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string query = @"
                INSERT INTO PublishingHouse (TitlePublishingHouse)
                VALUES (@Title)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Title", title);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            CustomMessageBox.Show($"Издательство \"{title}\" успешно сохранено.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            _currentPublishingHouseId = null;
                            ClearInputFields();
                            PublishingHouses();

                            // Скрываем кнопки редактирования
                            SaveNewRecordButton.Visibility = Visibility.Visible;
                            SaveChangesButton.Visibility = Visibility.Collapsed;
                            DeleteButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            CustomMessageBox.Show("Данные не были сохранены. Проверьте введённые значения.",
                                "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                CustomMessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Произошла ошибка: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Сохранение изменений существующего издательства (с проверкой на дубликат)
        /// </summary>
        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPublishingHouseId.HasValue)
            {
                CustomMessageBox.Show("Нет загруженной записи для сохранения. Выберите издательство.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string title = TitlePublishingHouseTb.Text?.Trim();

            if (string.IsNullOrEmpty(title))
            {
                CustomMessageBox.Show("Пожалуйста, введите название издательства.", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка на существование (исключая текущую запись)
            bool exists = await IsPublishingHouseExistsAsync(title, _currentPublishingHouseId.Value);
            if (exists)
            {
                CustomMessageBox.Show($"Издательство \"{title}\" уже существует в базе данных.\n\n" +
                    "Пожалуйста, проверьте введённые данные.",
                    "Дубликат записи", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string updateQuery = @"
                UPDATE PublishingHouse 
                SET TitlePublishingHouse = @Title
                WHERE IDPublishingHouse = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Title", title);
                        command.Parameters.AddWithValue("@Id", _currentPublishingHouseId.Value);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            CustomMessageBox.Show("Запись успешно обновлена.", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            PublishingHouses();
                            ClearInputFields();
                            _currentPublishingHouseId = null;

                            SaveNewRecordButton.Visibility = Visibility.Visible;
                            SaveChangesButton.Visibility = Visibility.Collapsed;
                            DeleteButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.",
                                "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearInputFields();
                            _currentPublishingHouseId = null;
                            SaveNewRecordButton.Visibility = Visibility.Visible;
                            SaveChangesButton.Visibility = Visibility.Collapsed;
                            DeleteButton.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                CustomMessageBox.Show($"Ошибка базы данных при сохранении: {sqlEx.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Произошла ошибка при сохранении: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Удаление издательства (с проверкой на связанные записи)
        /// </summary>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPublishingHouseId.HasValue)
            {
                CustomMessageBox.Show("Нет загруженной записи для удаления. Выберите издательство.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверяем, есть ли книги у этого издательства
            bool hasBooks = await CheckPublishingHouseHasBooksAsync(_currentPublishingHouseId.Value);
            if (hasBooks)
            {
                CustomMessageBox.Show("Невозможно удалить издательство, так как на него ссылаются книги.\n\n" +
                    "Сначала удалите или переназначьте книги этого издательства.",
                    "Невозможно удалить", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить выбранное издательство?",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM PublishingHouse WHERE IDPublishingHouse = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentPublishingHouseId.Value);
                            int rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                CustomMessageBox.Show("Запись успешно удалена.", "Успешно",
                                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                                ClearInputFields();
                                _currentPublishingHouseId = null;
                                PublishingHouses();

                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                CustomMessageBox.Show("Запись не была удалена. Возможно, она уже была удалена.",
                                    "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            }
                        }
                    }
                }
                catch (SqlException sqlEx)
                {
                    CustomMessageBox.Show($"Ошибка базы данных при удалении: {sqlEx.Message}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Произошла ошибка при удалении: {ex.Message}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        /// <summary>
        /// Двойной клик по списку - загрузка издательства для редактирования
        /// </summary>
        private void LvPublishingHouse_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = LvPublishingHouse.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic publishingHouse = item;
                int publishingHouseId = publishingHouse.IDPublishingHouse;
                string title = publishingHouse.TitlePublishingHouse;

                TitlePublishingHouseTb.Text = title;
                _currentPublishingHouseId = publishingHouseId;

                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки издательства: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            if (term == "Поиск по названию..." || term == "")
                term = "";
            ApplyPublishingHousesFilter(term);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        // ========== ВАЛИДАЦИЯ ВВОДА НАЗВАНИЯ ==========

        private void TitleField_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем буквы, цифры, пробелы, дефисы, точки, запятые, скобки
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-ЯёЁ0-9\s\-\.\,\(\)\""""]+$");
        }

        private void TitleField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!Regex.IsMatch(text, @"^[a-zA-Zа-яА-ЯёЁ0-9\s\-\.\,\(\)\""""]+$"))
                    {
                        e.Handled = true;
                        CustomMessageBox.Show("Название может содержать буквы, цифры, пробелы, дефисы, точки, запятые и скобки.",
                            "Недопустимые символы", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}