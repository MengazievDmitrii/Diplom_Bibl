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
        private string connectionString = DatabaseHelper.ConnectionString; // ЗАМЕНИТЬ!
        private int? _currentAuthorId = null; // Переменная для хранения ID текущей загруженной записи
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
        private void LoadAuthors()
        {
            try
            {
                const string query = @"
            SELECT ID_Autor, SurnameAuthor,  NameAuthor, PatronymicAuthor
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

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        _allAuthors = items;

                        // 🔹 Применяем фильтр (сначала — пустой, т.е. показываем все)
                        ApplyAuthorsFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
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
                    // Получаем свойства анонимного объекта
                    var surname = item.GetType().GetProperty("SurmameAuthor")?.GetValue(item)?.ToString() ?? "";
                    var name = item.GetType().GetProperty("NameAuthor")?.GetValue(item)?.ToString() ?? "";
                    var patronymic = item.GetType().GetProperty("PatronymicAuthor")?.GetValue(item)?.ToString() ?? "";


                    // Ищем в коде ИЛИ названии (по желанию — можно только по коду)
                    return string.IsNullOrEmpty(searchTerm)
                      || surname.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0|| patronymic.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // 🔹 ВАЖНО: присваиваем НОВУЮ коллекцию!
            LvAuthors.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
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
            AuthorIDLabel.Content = ""; // или Visibility = Collapsed, если нужно скрыть
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {

            // Проверка ввода (аналогично вашему SaveButton_Click)
            string surname = SurnameAuthorTb.Text?.Trim(); // Trim() — на случай пробелов
            string name = NameAuthorTb.Text?.Trim(); // Trim() — на случай пробелов
            string patronymic = PatronymicAuthorTb.Text?.Trim(); // Trim() — на случай пробелов
          


            if (string.IsNullOrEmpty(surname)||string.IsNullOrEmpty(surname)|| string.IsNullOrEmpty(patronymic))
            {
                
                CustomMessageBox.Show("Пожалуйста, введите корректные значения.", "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }



            string query = @"
                INSERT INTO Authors (SurnameAuthor, NameAuthor, PatronymicAuthor)
                VALUES (@SurnameAuthorT,@NameAuthorT,@PatronymicAuthorT)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Добавляем параметры, чтобы избежать SQL-инъекций
                        command.Parameters.AddWithValue("@SurnameAuthorT", surname); 
                        command.Parameters.AddWithValue("@NameAuthorT", name); 
                        command.Parameters.AddWithValue("@PatronymicAuthorT", patronymic); 


                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show($"Автор {surname} {name} {patronymic} успешно сохранен в базу данных:\n",
                                            "Успешно",CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
                            // или просто сбросить currentRecordId, если не планируете сразу редактировать
                            _currentAuthorId = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
                            // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
                            LoadAuthors();
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
                // Обработка ошибок SQL Server (например, нарушение ограничений, проблемы с подключением)
                 CustomMessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                                "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // Обработка любых других ошибок
                  CustomMessageBox.Show($"Произошла ошибка: {ex.Message}",
                                "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // --- Обработчик сохранения изменений существующей записи ---
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentAuthorId.HasValue)
            {
                  CustomMessageBox.Show("Нет загруженной записи для сохранения. Выберите специальность.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }


            // Проверка ввода ()

            string surname = SurnameAuthorTb.Text?.Trim();
            string name = NameAuthorTb.Text?.Trim();
            string patronymic = PatronymicAuthorTb.Text?.Trim();

            if (string.IsNullOrEmpty(surname)||string.IsNullOrEmpty(name)||string.IsNullOrEmpty(patronymic))
            {
                  CustomMessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }


            // Обновляем также date_record, если пользователь может изменить дату
            // Если дата не должна изменяться, уберите date_record из UPDATE
            string updateQuery = @"
                UPDATE Authors 
                SET SurnameAuthor = @Surname, NameAuthor=@Name, PatronymicAuthor=@Patronymic
                WHERE ID_Autor = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Surname", surname);
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Patronymic", patronymic);


                        command.Parameters.AddWithValue("@Id", _currentAuthorId.Value); // Используем ID текущей записи

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show("Запись успешно обновлена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            // После обновления можно перезагрузить запись, чтобы убедиться, что отображаются актуальные данные
                            // LoadFirstRecordForDate(DateTime.Parse(date));
                            LoadAuthors();
                        }
                        else
                        {
                             CustomMessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.", "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearInputFields();
                            _currentAuthorId = null;
                            // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
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

        // --- Обработчик удаления записи ---
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentAuthorId.HasValue)
            {
                 CustomMessageBox.Show("Нет загруженной записи для удаления. Выберите специальность.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result =  CustomMessageBox.Show("Вы уверены, что хотите удалить выбранную запись?",
                                         "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM Authors WHERE ID_Autor = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentAuthorId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                 CustomMessageBox.Show("Запись успешно удалена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                                ClearInputFields(); // Очищаем поля после удаления
                                _currentAuthorId = null;
                                LoadAuthors();
                                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                 CustomMessageBox.Show("Запись не была удалена. Возможно, она уже была удалена другим процессом.", "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
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
            // Если пользователь нажал "Нет", ничего не делаем.
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.GoBack();
        }


        private void LvSpecialities_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

                // Например, открываем диалог редактирования специальности
                // Или просто заполняем поля на странице:

                SurnameAuthorTb.Text = surname;
                NameAuthorTb.Text = name;
                PatronymicAuthorTb.Text = patronymic;

                _currentAuthorId = authorId; // ← если у вас есть такая переменная

                //Переключаем кнопки(если нужно)
                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки автора: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            // Если используете placeholder:
            if (term == "Поиск по фамилии имени отчеству..." || term == "") term = "";

            ApplyAuthorsFilter(term); // ← или ApplyGroupFilter(term)
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Gray;
            }
        }
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
                        CustomMessageBox.Show("Поле ФИО может содержать только буквы, пробелы и дефисы.", "Недопустимые символы",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}

    
