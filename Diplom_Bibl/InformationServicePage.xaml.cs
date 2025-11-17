using System;
using System.Data.SqlClient; // Или Microsoft.Data.SqlClient
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Для TextCompositionEventArgs, KeyEventArgs

namespace Diplom_Bibl
{
    /// <summary>
    /// Логика взаимодействия для InformationServicePage.xaml
    /// </summary>
    public partial class InformationServicePage : Page
    {
        // --- Поля ---
        private string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=DiplomBibl; Integrated Security=true;"; // ЗАМЕНИТЬ!
        private int? currentRecordId = null; // Переменная для хранения ID текущей загруженной записи

        public InformationServicePage()
        {
            InitializeComponent();
            // Устанавливаем SelectedDate равным текущей дате системы В КОНСТРУКТОРЕ
            // Это произойдёт до загрузки элемента управления
            DatePicker.SelectedDate = DateTime.Today;
        }

        public class InfoRecord
        {
            public int IdRecord { get; set; } // Primary Key
            public DateTime DateRecord { get; set; }
            public int DeliveryBook { get; set; }
            public int DeliveryCertificate { get; set; }
            public int EventsCount { get; set; } // Добавлено, если используется

            // Конструктор (необязательно, но полезно)
            public InfoRecord() { }

            public InfoRecord(int id, DateTime date, int books, int refs, int events) // events по умолчанию 0
            {
                IdRecord = id;
                DateRecord = date;
                DeliveryBook = books;
                DeliveryCertificate = refs;
                EventsCount = events; // Если events_count не нужен для редактирования, можно не включать
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9]+$");
        }

        // Разрешить Backspace, Delete и т.д.
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        // --- Обработчик изменения даты ---
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Событие SelectedDateChanged срабатывает и при программной установке SelectedDate
            // Но в нашем случае, в конструкторе мы устанавливаем дату ДО подключения обработчика.
            // Если обработчик подключён в XAML, он может сработать при установке SelectedDate в конструкторе.
            // Однако, если мы установим SelectedDate в конструкторе ПОСЛЕ InitializeComponent(),
            // то событие сработает. Чтобы избежать этого, можно проверить e.RemovedItems.Count.
            // Но проще установить дату в конструкторе ДО InitializeComponent, что невозможно из XAML.
            // Поэтому, если DatePicker_Loaded не вызывает SelectedDateChanged напрямую,
            // то это событие будет срабатывать только при пользовательском выборе.
            // Однако, если SelectedDate изменяется программно где-то ещё, это событие тоже сработает.
            // В текущем контексте, если DatePicker_Loaded просто устанавливал дату и вызывал SelectedDateChanged,
            // и этот вызов происходил до инициализации, то проблем быть не должно.
            // Но если дата устанавливается программно после, это может вызвать лишние сообщения.
            // ВАЖНО: Убедитесь, что в DatePicker_Loaded НЕ вызывается LoadFirstRecordForDate.

            DateTime? selectedDate = DatePicker.SelectedDate;

            // Загружаем запись ТОЛЬКО если дата выбрана (а не сброшена)
            if (selectedDate.HasValue)
            {
                LoadFirstRecordForDate(selectedDate.Value.Date);
            }
            else
            {
                // Если дата снята, очищаем поля
                ClearInputFields();
                currentRecordId = null;
                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                SaveNewRecordButton.Visibility = Visibility.Visible;
                SaveChangesButton.Visibility = Visibility.Collapsed;
                DeleteButton.Visibility = Visibility.Collapsed;
                // Не показываем сообщение, если дата снята пользователем
            }
        }

        // --- Метод загрузки первой записи по дате ---
        private void LoadFirstRecordForDate(DateTime date)
        {
            // Изменил запрос, чтобы использовать EventsCount, если он есть в таблице
            // Если столбца events_count нет, уберите его из SELECT и FROM
            string query = @"
                SELECT TOP 1 id_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record = @Date
                ORDER BY id_record"; // Сортировка по ID, если нужно выбрать первую из нескольких

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", date);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read()) // Если запись найдена
                            {
                                currentRecordId = (int)reader["id_record"];
                                IdRecordLabel.Content = currentRecordId.ToString(); // Отображаем ID (если нужно, можно оставить скрытым)

                                // Загружаем значения в поля
                                BooksCountBox.Text = reader["delivery_book"].ToString();
                                ReferencesCountBox.Text = reader["delivery_certificate"].ToString();
                                // Загружаем events_count, если он есть в таблице
                                if (reader["events_count"] != DBNull.Value)
                                {
                                    EventsCountBox.Text = reader["events_count"].ToString();
                                }
                                else
                                {
                                    EventsCountBox.Text = "0"; // или оставить пустым
                                }

                                // --- Показываем кнопки изменения/удаления, скрываем "Сохранить новую" ---
                                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                                SaveChangesButton.Visibility = Visibility.Visible;
                                DeleteButton.Visibility = Visibility.Visible;
                            }
                            else // Если запись не найдена
                            {
                                ClearInputFields();
                                currentRecordId = null;
                                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                                // --- ПОКАЗЫВАЕМ СООБЩЕНИЕ ТОЛЬКО ЕСЛИ ЗАПИСЬ НЕ НАЙДЕНА ---
                                // Это сообщение теперь появляется только при выборе даты пользователем (если запись отсутствует)
                                //MessageBox.Show("Запись за выбранную дату не найдена. Вы можете сохранить новую.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Ошибка базы данных при загрузке: {sqlEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearInputFields(); // Очищаем, если ошибка
                currentRecordId = null;
                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                SaveNewRecordButton.Visibility = Visibility.Visible;
                SaveChangesButton.Visibility = Visibility.Collapsed;
                DeleteButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearInputFields(); // Очищаем, если ошибка
                currentRecordId = null;
                // --- Скрываем кнопки изменения/удаления, показываем "Сохранить новую" ---
                SaveNewRecordButton.Visibility = Visibility.Visible;
                SaveChangesButton.Visibility = Visibility.Collapsed;
                DeleteButton.Visibility = Visibility.Collapsed;
            }
        }

        // --- Метод очистки полей ввода ---
        private void ClearInputFields()
        {
            // DatePicker.SelectedDate оставляем как есть
            BooksCountBox.Text = "0"; // или Clear()
            ReferencesCountBox.Text = "0"; // или Clear()
            EventsCountBox.Text = "0"; // или Clear()
            IdRecordLabel.Content = ""; // или Visibility = Collapsed, если нужно скрыть
        }

        // --- Обработчик сохранения новой записи ---
        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверка ввода (аналогично вашему SaveButton_Click)
            if (!int.TryParse(BooksCountBox.Text, out int books) ||
                !int.TryParse(ReferencesCountBox.Text, out int refs) ||
                !int.TryParse(EventsCountBox.Text, out int events))
            {
                MessageBox.Show("Пожалуйста, введите корректные числовые значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var date = DatePicker.SelectedDate?.ToString("yyyy-MM-dd"); // Формат даты для SQL (ГГГГ-ММ-ДД)

            if (date == null)
            {
                MessageBox.Show("Пожалуйста, выберите дату.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string query = @"
                INSERT INTO InformationServiseTb (date_record, delivery_book, delivery_certificate, events_count)
                VALUES (@Date, @Books, @Refs, @Events)";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Добавляем параметры, чтобы избежать SQL-инъекций
                        command.Parameters.AddWithValue("@Date", date); // date уже в формате "ГГГГ-ММ-ДД"
                        command.Parameters.AddWithValue("@Books", books);
                        command.Parameters.AddWithValue("@Refs", refs);
                        command.Parameters.AddWithValue("@Events", events);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show($"Данные за {date} успешно сохранены в базу данных:\n" +
                                            $"📚 Книг: {books} экз.\n" +
                                            $"📑 Справок: {refs} шт.\n" +
                                            $"👥 Мероприятий: {events} чел.",
                                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
                            // или просто сбросить currentRecordId, если не планируете сразу редактировать
                            currentRecordId = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
                            // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
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
            if (!currentRecordId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для сохранения. Выберите дату.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка ввода (аналогично вашему SaveButton_Click)
            if (!int.TryParse(BooksCountBox.Text, out int books) ||
                !int.TryParse(ReferencesCountBox.Text, out int refs) ||
                !int.TryParse(EventsCountBox.Text, out int events))
            {
                MessageBox.Show("Пожалуйста, введите корректные числовые значения.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var date = DatePicker.SelectedDate?.ToString("yyyy-MM-dd"); // Формат даты для SQL (ГГГГ-ММ-ДД)

            if (date == null)
            {
                MessageBox.Show("Пожалуйста, выберите дату.",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Обновляем также date_record, если пользователь может изменить дату
            // Если дата не должна изменяться, уберите date_record из UPDATE
            string updateQuery = @"
                UPDATE InformationServiseTb 
                SET date_record = @Date, delivery_book = @Books, delivery_certificate = @Refs, events_count = @Events 
                WHERE id_record = @Id";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Date", date);
                        command.Parameters.AddWithValue("@Books", books);
                        command.Parameters.AddWithValue("@Refs", refs);
                        command.Parameters.AddWithValue("@Events", events);
                        command.Parameters.AddWithValue("@Id", currentRecordId.Value); // Используем ID текущей записи

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            // После обновления можно перезагрузить запись, чтобы убедиться, что отображаются актуальные данные
                            // LoadFirstRecordForDate(DateTime.Parse(date));
                        }
                        else
                        {
                            MessageBox.Show("Запись не была обновлена. Возможно, она была удалена другим процессом.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearInputFields();
                            currentRecordId = null;
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
            if (!currentRecordId.HasValue)
            {
                MessageBox.Show("Нет загруженной записи для удаления. Выберите дату.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Подтверждение удаления
            var result = MessageBox.Show("Вы уверены, что хотите удалить выбранную запись?",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string deleteQuery = "DELETE FROM InformationServiseTb WHERE id_record = @Id";

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", currentRecordId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Запись успешно удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                                ClearInputFields(); // Очищаем поля после удаления
                                currentRecordId = null;
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

        // --- УБРАНО: DatePicker_Loaded ---
        // private void DatePicker_Loaded(object sender, RoutedEventArgs e)
        // {
        //     // Устанавливаем SelectedDate равным текущей дате системы
        //     DatePicker.SelectedDate = DateTime.Today;
        //     // УБРАНО: Не вызываем LoadFirstRecordForDate здесь!
        //     // Это приводило к лишнему вызову и сообщению при загрузке страницы.
        // }
    }
}