using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Логика взаимодействия для AddAuthor.xaml
    /// </summary>
    public partial class AddAuthor : Page
    {
        private string connectionString = DatabaseHelper.ConnectionString;
        private int? _currentAuthorId = null;
        private System.Collections.ObjectModel.ObservableCollection<object> _allAuthors;

        public AddAuthor()
        {
            InitializeComponent();
            LoadAuthors();

            // Маска ввода ФИО
            SurnameAuthorTb.PreviewTextInput += NameField_PreviewTextInput;
            NameAuthorTb.PreviewTextInput += NameField_PreviewTextInput;
            PatronymicAuthorTb.PreviewTextInput += NameField_PreviewTextInput;
            SurnameAuthorTb.PreviewKeyDown += NameField_PreviewKeyDown;
            NameAuthorTb.PreviewKeyDown += NameField_PreviewKeyDown;
            PatronymicAuthorTb.PreviewKeyDown += NameField_PreviewKeyDown;
        }

        /// <summary>
        /// Загрузка списка авторов из БД
        /// </summary>
        private void LoadAuthors()
        {
            try
            {
                const string query = @"
                    SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor
                    FROM Authors 
                    ORDER BY SurnameAuthor";

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
                                ID_Autor = reader["ID_Autor"] is int id ? id : 0,
                                SurnameAuthor = reader["SurnameAuthor"]?.ToString() ?? "",
                                NameAuthor = reader["NameAuthor"]?.ToString() ?? "",
                                PatronymicAuthor = reader["PatronymicAuthor"]?.ToString() ?? ""
                            });
                        }

                        _allAuthors = items;
                        ApplyAuthorsFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Фильтрация списка авторов
        /// </summary>
        private void ApplyAuthorsFilter(string searchTerm)
        {
            if (_allAuthors == null)
            {
                LvAuthors.ItemsSource = null;
                return;
            }

            var filtered = _allAuthors
                .Where(item =>
                {
                    var surname = item.GetType().GetProperty("SurnameAuthor")?.GetValue(item)?.ToString() ?? "";
                    var name = item.GetType().GetProperty("NameAuthor")?.GetValue(item)?.ToString() ?? "";
                    var patronymic = item.GetType().GetProperty("PatronymicAuthor")?.GetValue(item)?.ToString() ?? "";

                    return string.IsNullOrEmpty(searchTerm)
                        || surname.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || patronymic.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            LvAuthors.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }

        /// <summary>
        /// Проверка существования автора в БД
        /// </summary>
        private async Task<bool> IsAuthorExistsAsync(string surname, string name, string patronymic, int? excludeId = null)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) FROM Authors 
                    WHERE SurnameAuthor = @Surname 
                      AND NameAuthor = @Name 
                      AND (PatronymicAuthor = @Patronymic OR (PatronymicAuthor IS NULL AND @Patronymic IS NULL))";

                if (excludeId.HasValue)
                {
                    query += " AND ID_Autor != @ExcludeId";
                }

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Surname", surname);
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Patronymic", string.IsNullOrEmpty(patronymic) ? (object)DBNull.Value : patronymic);

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAuthors();
        }

        private void ClearInputFields()
        {
            SurnameAuthorTb.Clear();
            NameAuthorTb.Clear();
            PatronymicAuthorTb.Clear();
            AuthorIDLabel.Content = "";
        }

        /// <summary>
        /// Сохранение нового автора (с проверкой на дубликат)
        /// </summary>
        private async void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            string surname = SurnameAuthorTb.Text?.Trim();
            string name = NameAuthorTb.Text?.Trim();
            string patronymic = PatronymicAuthorTb.Text?.Trim();

            // Проверка обязательных полей
            if (string.IsNullOrEmpty(surname) || string.IsNullOrEmpty(name))
            {
                CustomMessageBox.Show("Пожалуйста, введите фамилию и имя автора.", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка на существование
            bool exists = await IsAuthorExistsAsync(surname, name, patronymic);
            if (exists)
            {
                string fullName = $"{surname} {name} {(string.IsNullOrEmpty(patronymic) ? "" : patronymic)}";
                CustomMessageBox.Show($"Автор \"{fullName}\" уже существует в базе данных.\n\n" +
                    "Пожалуйста, проверьте введённые данные.",
                    "Дубликат записи", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string query = @"
                INSERT INTO Authors (SurnameAuthor, NameAuthor, PatronymicAuthor)
                VALUES (@Surname, @Name, @Patronymic)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Surname", surname);
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Patronymic", string.IsNullOrEmpty(patronymic) ? (object)DBNull.Value : patronymic);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            CustomMessageBox.Show($"Автор {surname} {name} {patronymic} успешно сохранён.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            _currentAuthorId = null;
                            ClearInputFields();
                            LoadAuthors();

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
        /// Сохранение изменений существующего автора (с проверкой на дубликат)
        /// </summary>
        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentAuthorId.HasValue)
            {
                CustomMessageBox.Show("Нет загруженной записи для сохранения. Выберите автора.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string surname = SurnameAuthorTb.Text?.Trim();
            string name = NameAuthorTb.Text?.Trim();
            string patronymic = PatronymicAuthorTb.Text?.Trim();

            if (string.IsNullOrEmpty(surname) || string.IsNullOrEmpty(name))
            {
                CustomMessageBox.Show("Пожалуйста, введите фамилию и имя автора.", "Ошибка ввода",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка на существование (исключая текущую запись)
            bool exists = await IsAuthorExistsAsync(surname, name, patronymic, _currentAuthorId.Value);
            if (exists)
            {
                string fullName = $"{surname} {name} {(string.IsNullOrEmpty(patronymic) ? "" : patronymic)}";
                CustomMessageBox.Show($"Автор \"{fullName}\" уже существует в базе данных.\n\n" +
                    "Пожалуйста, проверьте введённые данные.",
                    "Дубликат записи", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string updateQuery = @"
                UPDATE Authors 
                SET SurnameAuthor = @Surname, 
                    NameAuthor = @Name, 
                    PatronymicAuthor = @Patronymic
                WHERE ID_Autor = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Surname", surname);
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Patronymic", string.IsNullOrEmpty(patronymic) ? (object)DBNull.Value : patronymic);
                        command.Parameters.AddWithValue("@Id", _currentAuthorId.Value);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            CustomMessageBox.Show("Запись успешно обновлена.", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            LoadAuthors();
                            ClearInputFields();
                            _currentAuthorId = null;

                            SaveNewRecordButton.Visibility = Visibility.Visible;
                            SaveChangesButton.Visibility = Visibility.Collapsed;
                            DeleteButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.",
                                "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearInputFields();
                            _currentAuthorId = null;
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
        /// Удаление автора (с проверкой на связанные записи)
        /// </summary>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentAuthorId.HasValue)
            {
                CustomMessageBox.Show("Нет загруженной записи для удаления. Выберите автора.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверяем, есть ли книги у этого автора
            bool hasBooks = await CheckAuthorHasBooksAsync(_currentAuthorId.Value);
            if (hasBooks)
            {
                CustomMessageBox.Show("Невозможно удалить автора, так как на него ссылаются книги.\n\n" +
                    "Сначала удалите или переназначьте книги этого автора.",
                    "Невозможно удалить", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить выбранного автора?",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM Authors WHERE ID_Autor = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentAuthorId.Value);
                            int rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                CustomMessageBox.Show("Запись успешно удалена.", "Успешно",
                                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                                ClearInputFields();
                                _currentAuthorId = null;
                                LoadAuthors();

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

        /// <summary>
        /// Проверка, есть ли у автора книги в библиотеке
        /// </summary>
        private async Task<bool> CheckAuthorHasBooksAsync(int authorId)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM Books WHERE Author = @AuthorId";
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@AuthorId", authorId);
                        int count = (int)await cmd.ExecuteScalarAsync();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки книг автора: {ex.Message}");
                return false;
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        /// <summary>
        /// Двойной клик по списку - загрузка автора для редактирования
        /// </summary>
        private void LvAuthors_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = LvAuthors.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic author = item;
                int authorId = author.ID_Autor;
                string surname = author.SurnameAuthor;
                string name = author.NameAuthor;
                string patronymic = author.PatronymicAuthor;

                SurnameAuthorTb.Text = surname;
                NameAuthorTb.Text = name;
                PatronymicAuthorTb.Text = patronymic;

                _currentAuthorId = authorId;

                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки автора: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            if (term == "Поиск по фамилии имени отчеству..." || term == "")
                term = "";
            ApplyAuthorsFilter(term);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        // ========== ВАЛИДАЦИЯ ВВОДА ФИО ==========

        private void NameField_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-ЯёЁ\s\-']$");
        }

        private void NameField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!Regex.IsMatch(text, @"^[a-zA-Zа-яА-ЯёЁ\s\-']+$"))
                    {
                        e.Handled = true;
                        CustomMessageBox.Show("Поле ФИО может содержать только буквы, пробелы и дефисы.",
                            "Недопустимые символы", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}