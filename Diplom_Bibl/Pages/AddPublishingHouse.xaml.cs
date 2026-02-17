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

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddPublishingHouse.xaml
    /// </summary>
    public partial class AddPublishingHouse : Page
    {
        DiplomBiblEntities db = new DiplomBiblEntities();
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;"; // ЗАМЕНИТЬ!
        private int? _currentPublishingHouseId = null; // Переменная для хранения ID текущей загруженной записи
        private System.Collections.ObjectModel.ObservableCollection<object> _allPublishingHouses;
        public AddPublishingHouse()
        {
            InitializeComponent();
            PublishingHouses();
        }
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

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        _allPublishingHouses = items;

                        // 🔹 Применяем фильтр (сначала — пустой, т.е. показываем все)
                        ApplyPublishingHousesFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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
                    // Получаем свойства анонимного объекта
                    var title = item.GetType().GetProperty("TitlePublishingHouse")?.GetValue(item)?.ToString() ?? "";


                    // Ищем в коде ИЛИ названии (по желанию — можно только по коду)
                    return string.IsNullOrEmpty(searchTerm)
                      || title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // 🔹 ВАЖНО: присваиваем НОВУЮ коллекцию!
            LvPublishingHouse.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PublishingHouses();
        }
        private void ClearInputFields()
        {

            TitlePublishingHouseTb.Clear();
            PublishingHouseIDLabel.Content = ""; // или Visibility = Collapsed, если нужно скрыть
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {

            // Проверка ввода (аналогично вашему SaveButton_Click)
            string title = TitlePublishingHouseTb.Text?.Trim(); // Trim() — на случай пробелов




            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }



            string query = @"
                INSERT INTO PublishingHouse (TitlePublishingHouse)
                VALUES (@TitlePublishingHouseT)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Добавляем параметры, чтобы избежать SQL-инъекций
                        command.Parameters.AddWithValue("@TitlePublishingHouseT", title);


                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show($"Издательство {title} успешно сохранено в базу данных:\n",
                                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
                            // или просто сбросить currentRecordId, если не планируете сразу редактировать
                            _currentPublishingHouseId = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
                            // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
                            PublishingHouses();
                        }
                        else
                        {
                            MessageBox.Show("Данные не были сохранены. Проверьте введённые значения.",
                                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Обработка ошибок SQL Server (например, нарушение ограничений, проблемы с подключением)
                MessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // Обработка любых других ошибок
                MessageBox.Show($"Произошла ошибка: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Обработчик сохранения изменений существующей записи ---
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPublishingHouseId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для сохранения. Выберите специальность.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            // Проверка ввода ()

            string title = TitlePublishingHouseTb.Text?.Trim();
          

            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            // Обновляем также date_record, если пользователь может изменить дату
            // Если дата не должна изменяться, уберите date_record из UPDATE
            string updateQuery = @"
                UPDATE PublishingHouse 
                SET TitlePublishingHouse = @TitlePublishingHouse
                WHERE IDPublishingHouse = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@TitlePublishingHouse", title);
                        command.Parameters.AddWithValue("@Id", _currentPublishingHouseId.Value); // Используем ID текущей записи

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После обновления можно перезагрузить запись, чтобы убедиться, что отображаются актуальные данные
                            // LoadFirstRecordForDate(DateTime.Parse(date));
                            PublishingHouses();
                        }
                        else
                        {
                            MessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearInputFields();
                            _currentPublishingHouseId = null;
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
                MessageBox.Show($"Ошибка базы данных при сохранении: {sqlEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при сохранении: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Обработчик удаления записи ---
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPublishingHouseId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для удаления. Выберите специальность.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result = MessageBox.Show("Вы уверены, что хотите удалить выбранную запись?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM PublishingHouse WHERE IDPublishingHouse = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentPublishingHouseId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Запись успешно удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                                ClearInputFields(); // Очищаем поля после удаления
                                _currentPublishingHouseId = null;
                                PublishingHouses();
                                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                MessageBox.Show("Запись не была удалена. Возможно, она уже была удалена другим процессом.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
                catch (SqlException sqlEx)
                {
                    MessageBox.Show($"Ошибка базы данных при удалении: {sqlEx.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Произошла ошибка при удалении: {ex.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var item = LvPublishingHouse.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic publishing_house = item;
                int publishing_house_id = publishing_house.IDPublishingHouse;
                string title = publishing_house.TitlePublishingHouse;
                

                // Например, открываем диалог редактирования
                // Или просто заполняем поля на странице:

                TitlePublishingHouseTb.Text = title;
               

                _currentPublishingHouseId = publishing_house_id; // ← если у вас есть такая переменная

                //Переключаем кнопки(если нужно)
                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки автора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            // Если используете placeholder:
            if (term == "Поиск по названию..." || term == "") term = "";

            ApplyPublishingHousesFilter(term); // ← или ApplyGroupFilter(term)
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

