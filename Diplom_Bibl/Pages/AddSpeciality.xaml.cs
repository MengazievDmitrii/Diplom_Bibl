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
    /// Логика взаимодействия для AddSpeciality.xaml
    /// </summary>
    public partial class AddSpeciality : Page
    {
        DiplomBiblEntities db = new DiplomBiblEntities();
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;"; // ЗАМЕНИТЬ!
        private int? _currentSpecialityId = null; // Переменная для хранения ID текущей загруженной записи
        private System.Collections.ObjectModel.ObservableCollection<object> _allSpecialities;
        public AddSpeciality()
        {
            InitializeComponent();
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
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            items.Add(new
                            {
                                SpecialityID = reader["SpecialityID"] is int id ? id : 0,
                                SpecialityCode = reader["SpecialityCode"]?.ToString() ?? "",
                                NameSpeciality = reader["NameSpeciality"]?.ToString() ?? ""
                            });
                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        _allSpecialities = items;

                        // 🔹 Применяем фильтр (сначала — пустой, т.е. показываем все)
                        ApplySpecialityFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ApplySpecialityFilter(string searchTerm)
        {
            if (_allSpecialities == null)
            {
                LvSpecialities.ItemsSource = null;
                return;
            }

            var filtered = _allSpecialities
                .Where(item =>
                {
                    // Получаем свойства анонимного объекта
                    var code = item.GetType().GetProperty("SpecialityCode")?.GetValue(item)?.ToString() ?? "";
                    var name = item.GetType().GetProperty("NameSpeciality")?.GetValue(item)?.ToString() ?? "";

                    // Ищем в коде ИЛИ названии (по желанию — можно только по коду)
                    return string.IsNullOrEmpty(searchTerm)
                        || code.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // 🔹 ВАЖНО: присваиваем НОВУЮ коллекцию!
            LvSpecialities.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecialities();
        }
        private void ClearInputFields()
        {
            // DatePicker.SelectedDate оставляем как есть
            CodeSpeciality.Clear();
            NameSpeciality.Clear();
            SpecialityIDLabel.Content = ""; // или Visibility = Collapsed, если нужно скрыть
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {

            // Проверка ввода (аналогично вашему SaveButton_Click)
            string codespeciality = CodeSpeciality.Text?.Trim(); // Trim() — на случай пробелов
            string namespeciality = NameSpeciality.Text?.Trim();

            if (string.IsNullOrEmpty(codespeciality) || string.IsNullOrEmpty(namespeciality))
            {
                MessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

         

            string query = @"
                INSERT INTO Speciality (SpecialityCode, NameSpeciality)
                VALUES (@SpecialityCodeV, @NameSpecialityV)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Добавляем параметры, чтобы избежать SQL-инъекций
                        command.Parameters.AddWithValue("@SpecialityCodeV", codespeciality); // date уже в формате "ГГГГ-ММ-ДД"
                        command.Parameters.AddWithValue("@NameSpecialityV", namespeciality);
                        

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show($"Специальность {codespeciality +" " + namespeciality } успешно сохранены в базу данных:\n",
                                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
                            // или просто сбросить currentRecordId, если не планируете сразу редактировать
                            _currentSpecialityId = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
                            // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
                            LoadSpecialities();
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
            if (!_currentSpecialityId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для сохранения. Выберите специальность.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

           
            // Проверка ввода ()
            string codespeciality = CodeSpeciality.Text?.Trim(); // Trim() — на случай пробелов
            string namespeciality = NameSpeciality.Text?.Trim();

            if (string.IsNullOrEmpty(codespeciality) || string.IsNullOrEmpty(namespeciality))
            {
                MessageBox.Show("Пожалуйста, введите корректные значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

          
            // Обновляем также date_record, если пользователь может изменить дату
            // Если дата не должна изменяться, уберите date_record из UPDATE
            string updateQuery = @"
                UPDATE Speciality 
                SET SpecialityCode = @SpecialityCode, NameSpeciality = @NameSpeciality
                WHERE SpecialityID = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@SpecialityCode", codespeciality);
                        command.Parameters.AddWithValue("@NameSpeciality", namespeciality);
                        
                        command.Parameters.AddWithValue("@Id", _currentSpecialityId.Value); // Используем ID текущей записи

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После обновления можно перезагрузить запись, чтобы убедиться, что отображаются актуальные данные
                            // LoadFirstRecordForDate(DateTime.Parse(date));
                            LoadSpecialities();
                        }
                        else
                        {
                            MessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearInputFields();
                            _currentSpecialityId = null;
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
            if (!_currentSpecialityId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для удаления. Выберите специальность.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result = MessageBox.Show("Вы уверены, что хотите удалить выбранную запись?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM Speciality WHERE SpecialityID = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentSpecialityId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Запись успешно удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                                ClearInputFields(); // Очищаем поля после удаления
                                _currentSpecialityId = null;
                                LoadSpecialities();
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
            var item = LvSpecialities.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic spec = item;
                int specId = spec.SpecialityID;
                string code = spec.SpecialityCode;
                string name = spec.NameSpeciality;

                // Например, открываем диалог редактирования специальности
                // Или просто заполняем поля на странице:
                CodeSpeciality.Text = code;
                NameSpeciality.Text = name;

                _currentSpecialityId = specId; // ← если у вас есть такая переменная

                //Переключаем кнопки(если нужно)
                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки специальности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            // Если используете placeholder:
            if (term == "Поиск по коду..." || term == "") term = "";

            ApplySpecialityFilter(term); // ← или ApplyGroupFilter(term)
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
