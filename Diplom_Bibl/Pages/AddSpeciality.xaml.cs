using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddSpeciality.xaml
    /// </summary>
    public partial class AddSpeciality : Page
    {
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
        private int? _currentSpecialityId = null;
        private ObservableCollection<SpecialityItem> _allSpecialities;

        // Флаг для отслеживания состояния плейсхолдера
        private bool _isPlaceholderActive = true;

        // Класс для представления специальности
        public class SpecialityItem
        {
            public int SpecialityID { get; set; }
            public string SpecialityCode { get; set; }
            public string NameSpeciality { get; set; }

            // Для удобства отображения в ListBox можно добавить свойство Display
            public string DisplayText => $"{SpecialityCode} - {NameSpeciality}";
        }

        public AddSpeciality()
        {
            InitializeComponent();

            // Устанавливаем плейсхолдер для поиска
            SetSearchPlaceholder();

            // Подписываемся на событие PreviewTextInput для очистки плейсхолдера при вводе
            SearchBox.PreviewTextInput += SearchBox_PreviewTextInput;
            SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
        }

        private void SetSearchPlaceholder()
        {
            SearchBox.Text = "Поиск по коду или названию специальности...";
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

        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Очищаем плейсхолдер при начале ввода
            ClearPlaceholder();
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Очищаем плейсхолдер при нажатии любой клавиши (кроме управляющих)
            if (_isPlaceholderActive)
            {
                // Исключаем управляющие клавиши
                if (e.Key != Key.Tab &&
                    e.Key != Key.Enter &&
                    e.Key != Key.Escape &&
                    e.Key != Key.LeftShift &&
                    e.Key != Key.RightShift &&
                    e.Key != Key.LeftCtrl &&
                    e.Key != Key.RightCtrl &&
                    e.Key != Key.LeftAlt &&
                    e.Key != Key.RightAlt &&
                    e.Key != Key.CapsLock &&
                    e.Key != Key.NumLock &&
                    e.Key != Key.Scroll &&
                    e.Key != Key.Home &&
                    e.Key != Key.End &&
                    e.Key != Key.Insert &&
                    e.Key != Key.Delete &&
                    e.Key != Key.Left &&
                    e.Key != Key.Right &&
                    e.Key != Key.Up &&
                    e.Key != Key.Down &&
                    e.Key != Key.PageUp &&
                    e.Key != Key.PageDown)
                {
                    ClearPlaceholder();
                }
            }
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
                        var items = new ObservableCollection<SpecialityItem>();

                        while (reader.Read())
                        {
                            items.Add(new SpecialityItem
                            {
                                SpecialityID = reader["SpecialityID"] != DBNull.Value ? Convert.ToInt32(reader["SpecialityID"]) : 0,
                                SpecialityCode = reader["SpecialityCode"]?.ToString() ?? "",
                                NameSpeciality = reader["NameSpeciality"]?.ToString() ?? ""
                            });
                        }

                        _allSpecialities = items;
                        ApplySpecialityFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки специальностей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplySpecialityFilter(string searchTerm)
        {
            if (_allSpecialities == null)
            {
                LvSpecialities.ItemsSource = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                // Если поисковый запрос пуст, показываем все
                LvSpecialities.ItemsSource = _allSpecialities;
                LvSpecialities.DisplayMemberPath = "DisplayText";
            }
            else
            {
                // Фильтруем по коду специальности ИЛИ по названию специальности
                var filtered = _allSpecialities
                    .Where(item =>
                        (item.SpecialityCode?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.NameSpeciality?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();

                LvSpecialities.ItemsSource = new ObservableCollection<SpecialityItem>(filtered);
                LvSpecialities.DisplayMemberPath = "DisplayText";
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecialities();
        }

        private void ClearInputFields()
        {
            CodeSpeciality.Clear();
            NameSpeciality.Clear();
            SpecialityIDLabel.Content = "";
            _currentSpecialityId = null;
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверка ввода
            string codespeciality = CodeSpeciality.Text?.Trim();
            string namespeciality = NameSpeciality.Text?.Trim();

            if (string.IsNullOrEmpty(codespeciality) || string.IsNullOrEmpty(namespeciality))
            {
                 CustomMessageBox.Show("Пожалуйста, введите код и название специальности.",
                                "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
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
                        command.Parameters.AddWithValue("@SpecialityCodeV", codespeciality);
                        command.Parameters.AddWithValue("@NameSpecialityV", namespeciality);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show($"Специальность '{codespeciality} {namespeciality}' успешно сохранена.",
                                            "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearInputFields();
                            LoadSpecialities();

                            // Переключаем кнопки
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
                if (sqlEx.Number == 2627) // Нарушение уникальности
                {
                     CustomMessageBox.Show($"Специальность с кодом '{codespeciality}' уже существует.",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                else
                {
                     CustomMessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Произошла ошибка: {ex.Message}",
                                "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSpecialityId.HasValue)
            {
                 CustomMessageBox.Show("Нет загруженной записи для сохранения. Выберите специальность.",
                               "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string codespeciality = CodeSpeciality.Text?.Trim();
            string namespeciality = NameSpeciality.Text?.Trim();

            if (string.IsNullOrEmpty(codespeciality) || string.IsNullOrEmpty(namespeciality))
            {
                 CustomMessageBox.Show("Пожалуйста, введите код и название специальности.",
                                "Ошибка ввода", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

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
                        command.Parameters.AddWithValue("@Id", _currentSpecialityId.Value);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                             CustomMessageBox.Show("Запись успешно обновлена.", "Успешно",
                                          CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            LoadSpecialities();

                            // Переключаем кнопки обратно на режим добавления
                            SaveNewRecordButton.Visibility = Visibility.Visible;
                            SaveChangesButton.Visibility = Visibility.Collapsed;
                            DeleteButton.Visibility = Visibility.Collapsed;

                            ClearInputFields();
                        }
                        else
                        {
                             CustomMessageBox.Show("Запись не была обновлена. Возможно, она была удалена.",
                                          "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearInputFields();
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Number == 2627) // Нарушение уникальности
                {
                     CustomMessageBox.Show($"Специальность с кодом '{codespeciality}' уже существует.",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                else
                {
                     CustomMessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Произошла ошибка: {ex.Message}",
                                "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSpecialityId.HasValue)
            {
                 CustomMessageBox.Show("Нет загруженной записи для удаления. Выберите специальность.",
                               "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result =  CustomMessageBox.Show("Вы уверены, что хотите удалить выбранную специальность?",
                                         "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
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
                                 CustomMessageBox.Show("Запись успешно удалена.", "Успешно",
                                              CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                                ClearInputFields();
                                LoadSpecialities();

                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                 CustomMessageBox.Show("Запись не была удалена. Возможно, она уже удалена.",
                                              "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            }
                        }
                    }
                }
                catch (SqlException sqlEx)
                {
                    if (sqlEx.Number == 547) // Нарушение внешнего ключа
                    {
                         CustomMessageBox.Show("Невозможно удалить специальность, так как на неё ссылаются другие записи.",
                                      "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                    else
                    {
                         CustomMessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
                                      "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                     CustomMessageBox.Show($"Произошла ошибка: {ex.Message}",
                                  "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.GoBack();
        }

        private void LvSpecialities_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvSpecialities.SelectedItem is SpecialityItem selectedSpeciality)
            {
                try
                {
                    CodeSpeciality.Text = selectedSpeciality.SpecialityCode;
                    NameSpeciality.Text = selectedSpeciality.NameSpeciality;
                    _currentSpecialityId = selectedSpeciality.SpecialityID;

                    // Переключаем кнопки
                    SaveNewRecordButton.Visibility = Visibility.Collapsed;
                    SaveChangesButton.Visibility = Visibility.Visible;
                    DeleteButton.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                     CustomMessageBox.Show($"Ошибка загрузки специальности: {ex.Message}",
                                  "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Если активен плейсхолдер, не выполняем поиск
            if (_isPlaceholderActive)
            {
                return;
            }

            string searchText = SearchBox.Text;
            ApplySpecialityFilter(searchText);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Очищаем плейсхолдер при получении фокуса
            ClearPlaceholder();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Если поле пустое при потере фокуса, возвращаем плейсхолдер
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SetSearchPlaceholder();

                // При возврате плейсхолдера показываем все записи
                ApplySpecialityFilter("");
            }
            else
            {
                // Если в поле есть текст, но плейсхолдер не активен, оставляем как есть
                // Ничего не делаем
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // При нажатии Escape очищаем поиск и возвращаем плейсхолдер
                SetSearchPlaceholder();
                ApplySpecialityFilter("");
                LvSpecialities.Focus();
                e.Handled = true; // Предотвращаем дальнейшую обработку
            }
        }
    }
}