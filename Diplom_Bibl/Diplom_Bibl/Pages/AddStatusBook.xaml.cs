using System;
using System.Collections.Generic;
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
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddStatusBook.xaml
    /// </summary>
    public partial class AddStatusBook : Page
    {
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;"; // ЗАМЕНИТЬ!
        private int? _currentStatusIDBook = null; // Переменная для хранения ID текущей загруженной записи
        private System.Collections.ObjectModel.ObservableCollection<object> _allStatusBooks;
        public AddStatusBook()
        {
            InitializeComponent();
            LoadStatusBook();
        }
        private void LoadStatusBook()
        {
            try
            {
                const string query = @"
            SELECT ID_Status, Title_Status 
            FROM StatusBook 
            ORDER BY Title_Status";

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
                                ID_Status = reader["ID_Status"] is int id ? id : 0,
                                Title_Status = reader["Title_Status"]?.ToString() ?? ""

                            });
                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        _allStatusBooks = items;

                        // 🔹 Применяем фильтр (сначала — пустой, т.е. показываем все)
                        ApplyGenersFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void ApplyGenersFilter(string searchTerm)
        {
            if (_allStatusBooks == null)
            {
                LvStatusBook.ItemsSource = null;
                return;
            }

            var filtered = _allStatusBooks
                .Where(item =>
                {
                    // Получаем свойства анонимного объекта
                    var name = item.GetType().GetProperty("Title_Status")?.GetValue(item)?.ToString() ?? "";


                    // Ищем в коде ИЛИ названии (по желанию — можно только по коду)
                    return string.IsNullOrEmpty(searchTerm)
                      || name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // 🔹 ВАЖНО: присваиваем НОВУЮ коллекцию!
            LvStatusBook.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatusBook();
        }
        private void ClearInputFields()
        {

            TitleStatus.Clear();
            StatusIDLabel.Content = ""; // или Visibility = Collapsed, если нужно скрыть
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {

            // Проверка ввода (аналогично вашему SaveButton_Click)
            string titlestatus = TitleStatus.Text?.Trim(); // Trim() — на случай пробелов


            if (string.IsNullOrEmpty(titlestatus))
            {
                 CustomMessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }



            string query = @"
                INSERT INTO StatusBook (Title_Status)
                VALUES (@Title_Status)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Добавляем параметры, чтобы избежать SQL-инъекций
                        command.Parameters.AddWithValue("@Title_Status", titlestatus); // date уже в формате "ГГГГ-ММ-ДД"


                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show($"Статус {titlestatus} успешно сохранен в базу данных:\n",
                                            "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
                            // или просто сбросить currentRecordId, если не планируете сразу редактировать
                            _currentStatusIDBook = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
                            // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
                            LoadStatusBook();
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
            if (!_currentStatusIDBook.HasValue)
            {
                 CustomMessageBox.Show("Нет загруженной записи для сохранения. Выберите специальность.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }


            // Проверка ввода ()

            string titlestatus = TitleStatus.Text?.Trim();

            if (string.IsNullOrEmpty(titlestatus))
            {
                 CustomMessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }


            // Обновляем также date_record, если пользователь может изменить дату
            // Если дата не должна изменяться, уберите date_record из UPDATE
            string updateQuery = @"
                UPDATE StatusBook 
                SET Title_Status = @Title_Status
                WHERE ID_Status = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Title_Status", titlestatus);


                        command.Parameters.AddWithValue("@Id", _currentStatusIDBook.Value); // Используем ID текущей записи

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show("Запись успешно обновлена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            // После обновления можно перезагрузить запись, чтобы убедиться, что отображаются актуальные данные
                            // LoadFirstRecordForDate(DateTime.Parse(date));
                            LoadStatusBook();
                        }
                        else
                        {
                             CustomMessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.", "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearInputFields();
                            _currentStatusIDBook = null;
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
            if (!_currentStatusIDBook.HasValue)
            {
                 CustomMessageBox.Show("Нет загруженной записи для удаления. Выберите статус.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result =  CustomMessageBox.Show("Вы уверены, что хотите удалить выбранную запись?",
                                         "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM StatusBook WHERE ID_Status = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentStatusIDBook.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                 CustomMessageBox.Show("Запись успешно удалена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                                ClearInputFields(); // Очищаем поля после удаления
                                _currentStatusIDBook = null;
                                LoadStatusBook();
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
            var item = LvStatusBook.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic status = item;
                int statusId = status.ID_Status;
                string title = status.Title_Status;

                // Например, открываем диалог редактирования специальности
                // Или просто заполняем поля на странице:

                TitleStatus.Text = title;

                _currentStatusIDBook = statusId; // ← если у вас есть такая переменная

                //Переключаем кнопки(если нужно)
                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки статуса: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            // Если используете placeholder:
            if (term == "Поиск по названию..." || term == "") term = "";

            ApplyGenersFilter(term); // ← или ApplyGroupFilter(term)
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

 

