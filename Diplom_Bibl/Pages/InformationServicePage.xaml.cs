using Diplom_Bibl.Controls;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class InformationServicePage : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private BookIssuanceService _issuanceService;

        private ObservableCollection<BookIssuanceService.ReaderInfo> _readers;
        private ObservableCollection<BookIssuanceService.ReaderInfo> _students;
        private ObservableCollection<BookIssuanceService.GroupInfo> _groups;
        private ObservableCollection<BookIssuanceService.TeacherInfo> _teachers;
        private ObservableCollection<BookIssuanceService.BookInfo> _books;
        private ObservableCollection<BookIssuanceService.IssuanceItem> _todayIssues;

        private int? _currentRecordId = null;
        private bool _isPlaceholderActive = true;

        // Выбранные элементы
        private BookIssuanceService.BookInfo _selectedBook;
        private BookIssuanceService.ReaderInfo _selectedReader;
        private BookIssuanceService.GroupInfo _selectedGroup;
        private BookIssuanceService.TeacherInfo _selectedTeacher;
        private BookIssuanceService.ReaderInfo _selectedStudent;

        // ID статусов книг
        private int _statusAvailableId = 1; // "В наличии"
        private int _statusIssuedId = 3;    // "Выдана"

        public InformationServicePage()
        {
            InitializeComponent();

            _issuanceService = new BookIssuanceService(_connectionString);

            // Инициализируем коллекции
            _readers = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _students = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _groups = new ObservableCollection<BookIssuanceService.GroupInfo>();
            _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            _books = new ObservableCollection<BookIssuanceService.BookInfo>();
            _todayIssues = new ObservableCollection<BookIssuanceService.IssuanceItem>();

            DatePicker.SelectedDate = DateTime.Today;

            // Загружаем данные
            LoadTodayIssues();
            LoadReaders();
            LoadGroups();
            LoadTeachers();
            LoadBooks();
            InitializeStats();
            LoadStatusIds(); // Загружаем ID статусов

            // Подписываемся на события
            this.Loaded += (s, e) => UpdateInventoryPreview();
        }

        private void LoadStatusIds()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Получаем ID статуса "В наличии"
                    string availableQuery = "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%'";
                    using (var cmd = new SqlCommand(availableQuery, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                            _statusAvailableId = Convert.ToInt32(result);
                    }

                    // Получаем ID статуса "Выдана"
                    string issuedQuery = "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Выдана%'";
                    using (var cmd = new SqlCommand(issuedQuery, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                            _statusIssuedId = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статусов: {ex.Message}");
            }
        }

        private void LoadTodayStats()
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT id_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record = @Date";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                _currentRecordId = Convert.ToInt32(reader["id_record"]);
                                BooksCountBox.Text = reader["delivery_book"].ToString();
                                ReferencesCountBox.Text = reader["delivery_certificate"].ToString();
                                EventsCountBox.Text = reader["events_count"].ToString();

                                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                                SaveChangesButton.Visibility = Visibility.Visible;
                                DeleteButton.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                _currentRecordId = null;
                                // НЕ ОБНУЛЯЕМ поля, если записи нет!
                                // Оставляем текущие значения
                                // BooksCountBox.Text = "0";
                                // ReferencesCountBox.Text = "0";
                                // EventsCountBox.Text = "0";

                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                        }
                    }

                    UpdateStatsDisplay();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void UpdateStatsDisplay()
        {
            if (BooksCountStat != null)
                BooksCountStat.Text = BooksCountBox.Text;
            if (ReferencesCountStat != null)
                ReferencesCountStat.Text = ReferencesCountBox.Text;
            if (EventsCountStat != null)
                EventsCountStat.Text = EventsCountBox.Text;
            if (IdRecordLabel != null)
                IdRecordLabel.Content = _currentRecordId.HasValue ? $"ID записи: {_currentRecordId}" : "Новая запись";
        }

        private void LoadReaders()
        {
            try
            {
                _readers = _issuanceService.GetReaders();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки читателей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadGroups()
        {
            try
            {
                _groups = _issuanceService.GetGroups();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void LoadTeachers()
        {
            try
            {
                _teachers = _issuanceService.GetTeachers();
                System.Diagnostics.Debug.WriteLine($"Загружено преподавателей: {_teachers.Count}");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки преподавателей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                _teachers = new ObservableCollection<BookIssuanceService.TeacherInfo>();
            }
        }
        private void LoadBooks()
        {
            try
            {
                _books = _issuanceService.GetAvailableBooks();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadTodayIssues()
        {
            try
            {
                _todayIssues = _issuanceService.GetIssuancesByDate(DateTime.Today);

                // Добавляем цвета для просрочки
                foreach (var item in _todayIssues)
                {
                    item.OverdueColor = item.IsOverdue ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }

                IssuesDataGrid.ItemsSource = _todayIssues;

                System.Diagnostics.Debug.WriteLine($"LoadTodayIssues: загружено {_todayIssues.Count} записей");
                foreach (var item in _todayIssues)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {item.IssuanceType}: {item.DisplayBook}");
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки выдач: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void UpdateInventoryPreview()
        {
            try
            {
                if (QuantityBox == null || AvailableQuantityText == null || _selectedBook == null)
                    return;

                AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();

                if (GroupTypeRadio.IsChecked == true)
                {
                    QuantityBox.IsEnabled = true;
                    if (string.IsNullOrWhiteSpace(QuantityBox.Text) || QuantityBox.Text == "0")
                        QuantityBox.Text = "1";
                }
                else
                {
                    QuantityBox.Text = "1";
                    QuantityBox.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateInventoryPreview: {ex.Message}");
            }
        }

        private void IssueType_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ReaderTypeRadio.IsChecked == true)
                {
                    if (ReaderSearchSection != null)
                        ReaderSearchSection.Visibility = Visibility.Visible;

                    if (GroupSearchSection != null)
                        GroupSearchSection.Visibility = Visibility.Collapsed;

                    if (QuantityBox != null)
                    {
                        QuantityBox.Text = "1";
                        QuantityBox.IsEnabled = false;
                    }
                }
                else
                {
                    if (ReaderSearchSection != null)
                        ReaderSearchSection.Visibility = Visibility.Collapsed;

                    if (GroupSearchSection != null)
                        GroupSearchSection.Visibility = Visibility.Visible;

                    if (QuantityBox != null && _selectedBook != null)
                    {
                        QuantityBox.IsEnabled = true;
                        QuantityBox.Text = "1";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в IssueType_Checked: {ex.Message}");
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                LoadTodayStats();
                LoadTodayIssues();
                UpdateStatsDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при изменении даты: {ex.Message}");
            }
        }

        // Поиск читателей
        private void ReaderSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderActive && ReaderSearchBox.Text == "Введите фамилию или номер билета...")
            {
                ReaderSearchBox.Text = "";
                ReaderSearchBox.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void ReaderSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReaderSearchBox.Text))
            {
                ReaderSearchBox.Text = "Введите фамилию или номер билета...";
                ReaderSearchBox.Foreground = Brushes.Gray;
                _isPlaceholderActive = true;
                ReadersListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ReaderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ReadersListBox == null) return;

            string search = ReaderSearchBox.Text.ToLower();
            if (_isPlaceholderActive || string.IsNullOrWhiteSpace(search))
            {
                ReadersListBox.Visibility = Visibility.Collapsed;
                return;
            }

            var filtered = _readers.Where(r =>
                r.LastName.ToLower().Contains(search) ||
                r.FirstName.ToLower().Contains(search) ||
                r.CardNumber.ToLower().Contains(search)).Take(10).ToList();

            if (filtered.Any())
            {
                ReadersListBox.ItemsSource = filtered;
                ReadersListBox.Visibility = Visibility.Visible;
            }
            else
            {
                ReadersListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ReadersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReadersListBox?.SelectedItem is BookIssuanceService.ReaderInfo selected)
            {
                _selectedReader = selected;
                ReaderSearchBox.Text = selected.DisplayInfo;
                ReaderSearchBox.Foreground = Brushes.Black;
                ReadersListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Поиск групп
        private void GroupSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (GroupSearchBox.Text == "Введите код группы...")
            {
                GroupSearchBox.Text = "";
                GroupSearchBox.Foreground = Brushes.Black;
            }
        }

        private void GroupSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupSearchBox.Text))
            {
                GroupSearchBox.Text = "Введите код группы...";
                GroupSearchBox.Foreground = Brushes.Gray;
                GroupsListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void GroupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GroupsListBox == null) return;

            string search = GroupSearchBox.Text.ToLower();
            if (search == "введите код группы..." || string.IsNullOrWhiteSpace(search))
            {
                GroupsListBox.Visibility = Visibility.Collapsed;
                return;
            }

            var filtered = _groups.Where(g => g.GroupCode.ToLower().Contains(search)).Take(10).ToList();

            if (filtered.Any())
            {
                GroupsListBox.ItemsSource = filtered;
                GroupsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                GroupsListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsListBox?.SelectedItem is BookIssuanceService.GroupInfo selected)
            {
                _selectedGroup = selected;
                GroupSearchBox.Text = selected.GroupCode;
                GroupSearchBox.Foreground = Brushes.Black;
                GroupsListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Поиск преподавателей
        private void TeacherSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TeacherSearchBox.Text == "Введите фамилию преподавателя...")
            {
                TeacherSearchBox.Text = "";
                TeacherSearchBox.Foreground = Brushes.Black;
            }
        }

        private void TeacherSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TeacherSearchBox.Text))
            {
                TeacherSearchBox.Text = "Введите фамилию преподавателя...";
                TeacherSearchBox.Foreground = Brushes.Gray;
                TeachersListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void TeacherSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (TeachersListBox == null) return;

                string search = TeacherSearchBox.Text.Trim();

                // Проверяем, не является ли текст плейсхолдером
                if (search == "Введите фамилию преподавателя..." || string.IsNullOrWhiteSpace(search))
                {
                    TeachersListBox.Visibility = Visibility.Collapsed;
                    return;
                }

                // Для отладки
                System.Diagnostics.Debug.WriteLine($"Поиск преподавателя: {search}");

                // Фильтруем существующую коллекцию _teachers (не обращаемся к базе каждый раз)
                var filtered = _teachers.Where(t =>
                    t.LastName.ToLower().Contains(search.ToLower()) ||
                    t.FirstName.ToLower().Contains(search.ToLower())).Take(10).ToList();

                if (filtered.Any())
                {
                    TeachersListBox.ItemsSource = filtered;
                    TeachersListBox.Visibility = Visibility.Visible;
                }
                else
                {
                    TeachersListBox.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в TeacherSearchBox_TextChanged: {ex.Message}");
            }
        }

        private void TeachersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (TeachersListBox?.SelectedItem is BookIssuanceService.TeacherInfo selected)
                {
                    _selectedTeacher = selected;
                    TeacherSearchBox.Text = selected.FullName;
                    TeacherSearchBox.Foreground = Brushes.Black;
                    TeachersListBox.Visibility = Visibility.Collapsed;

                    System.Diagnostics.Debug.WriteLine($"Выбран преподаватель: {selected.FullName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в TeachersListBox_SelectionChanged: {ex.Message}");
            }
        }

        // Поиск студентов
        private void StudentSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (StudentSearchBox.Text == "Введите фамилию или номер билета...")
            {
                StudentSearchBox.Text = "";
                StudentSearchBox.Foreground = Brushes.Black;
            }
        }

        private void StudentSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StudentSearchBox.Text))
            {
                StudentSearchBox.Text = "Введите фамилию или номер билета...";
                StudentSearchBox.Foreground = Brushes.Gray;
                StudentsListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void StudentSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (StudentsListBox == null) return;

            string search = StudentSearchBox.Text.ToLower();
            if (search == "введите фамилию или номер билета..." || string.IsNullOrWhiteSpace(search))
            {
                StudentsListBox.Visibility = Visibility.Collapsed;
                return;
            }

            // Получаем студентов через сервис
            _students = _issuanceService.GetReadersStudent(search);

            if (_students.Any())
            {
                StudentsListBox.ItemsSource = _students;
                StudentsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                StudentsListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void StudentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentsListBox?.SelectedItem is BookIssuanceService.ReaderInfo selected)
            {
                _selectedStudent = selected;
                StudentSearchBox.Text = selected.DisplayInfo;
                StudentSearchBox.Foreground = Brushes.Black;
                StudentsListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Поиск книг
        private void BookSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (BookSearchBox.Text == "Введите название или автора...")
            {
                BookSearchBox.Text = "";
                BookSearchBox.Foreground = Brushes.Black;
            }
        }

        private void BookSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BookSearchBox.Text))
            {
                BookSearchBox.Text = "Введите название или автора...";
                BookSearchBox.Foreground = Brushes.Gray;
                BooksListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void BookSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BooksListBox == null) return;

            string search = BookSearchBox.Text.ToLower();
            if (search == "введите название или автора..." || string.IsNullOrWhiteSpace(search))
            {
                BooksListBox.Visibility = Visibility.Collapsed;
                return;
            }

            var filtered = _books.Where(b =>
                b.TitleBook.ToLower().Contains(search) ||
                b.AuthorName.ToLower().Contains(search)).Take(10).ToList();

            if (filtered.Any())
            {
                BooksListBox.ItemsSource = filtered;
                BooksListBox.Visibility = Visibility.Visible;
            }
            else
            {
                BooksListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void BooksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BooksListBox?.SelectedItem is BookIssuanceService.BookInfo selected)
            {
                _selectedBook = selected;

                // Обновляем отображение выбранной книги
                SelectedBookTitle.Text = selected.TitleBook;
                SelectedBookAuthor.Text = selected.AuthorName;
                SelectedBookPanel.Visibility = Visibility.Visible;

                BookSearchBox.Text = selected.TitleBook;
                BookSearchBox.Foreground = Brushes.Black;
                BooksListBox.Visibility = Visibility.Collapsed;

                // Показываем общее доступное количество
                AvailableQuantityText.Text = selected.AvailableCopies.ToString();
                UpdateInventoryPreview();
            }
        }

        private void ClearSelectedBook_Click(object sender, RoutedEventArgs e)
        {
            _selectedBook = null;
            SelectedBookPanel.Visibility = Visibility.Collapsed;
            BookSearchBox.Text = "Введите название или автора...";
            BookSearchBox.Foreground = Brushes.Gray;
            AvailableQuantityText.Text = "0";
        }

        private void CreateIssueButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (_selectedBook == null)
            {
                CustomMessageBox.Show("Выберите книгу для выдачи.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            int requestedQuantity = 1;
            if (GroupTypeRadio.IsChecked == true)
            {
                if (!int.TryParse(QuantityBox?.Text, out requestedQuantity) || requestedQuantity <= 0)
                {
                    CustomMessageBox.Show("Укажите корректное количество.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }
            }
            System.Diagnostics.Debug.WriteLine($"Выбрана книга: {_selectedBook.TitleBook}, Автор: {_selectedBook.AuthorName}");
            System.Diagnostics.Debug.WriteLine($"Всего доступно: {_selectedBook.AvailableCopies}, Запрошено: {requestedQuantity}");

            // Проверяем доступное количество
            if (requestedQuantity > _selectedBook.AvailableCopies)
            {
                CustomMessageBox.Show($"Недостаточно экземпляров. Доступно: {_selectedBook.AvailableCopies}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (ReaderTypeRadio.IsChecked == true)
            {
                // Выдача читателю
                if (_selectedReader == null)
                {
                    CustomMessageBox.Show("Выберите читателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                CreateIndividualIssuance(requestedQuantity);
            }
            else
            {
                // Выдача группе
                if (_selectedGroup == null)
                {
                    CustomMessageBox.Show("Выберите группу.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                if (_selectedTeacher == null)
                {
                    CustomMessageBox.Show("Выберите преподавателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                if (_selectedStudent == null)
                {
                    CustomMessageBox.Show("Выберите студента.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                CreateGroupIssuance(requestedQuantity);
            }
        }

        private void CreateIndividualIssuance(int quantity)
        {
            try
            {
                // Получаем ID книги
                int bookId = GetAvailableBookIdWithQuantity(_selectedBook.TitleBook, _selectedBook.AuthorName, 1);

                if (bookId == 0)
                {
                    CustomMessageBox.Show("Не найдено доступных экземпляров книги.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                DateTime plannedReturnDate = DateTime.Now.AddDays(14);

                int issuanceId = _issuanceService.CreateIndividualIssuance(
                    bookId,
                    _selectedReader.ReaderID,
                    plannedReturnDate,
                    "Выдача через информационное обслуживание"
                );

                // Обновляем статус книги на "Выдана"
                UpdateBookStatus(bookId, _statusIssuedId);

                // Обновляем статистику в базе данных
                UpdateDailyStats(quantity);

                // Обновляем количество книг локально
                _selectedBook.AvailableCopies -= quantity;
                AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();

                CustomMessageBox.Show($"Индивидуальная выдача успешно создана!\nID выдачи: {issuanceId}\nВыдано книг: {quantity}", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                // Очищаем форму
                ClearSelectedBook_Click(null, null);
                _selectedReader = null;
                ReaderSearchBox.Text = "Введите фамилию или номер билета...";
                ReaderSearchBox.Foreground = Brushes.Gray;

                // Обновляем список выдач и книг
                LoadTodayIssues();
                LoadBooks();
                LoadTodayStats(); // Перезагружаем статистику из базы
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании выдачи: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновляет статус книги
        /// </summary>
        private void UpdateBookStatus(int bookId, int statusId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "UPDATE Books SET StatusBook = @StatusId WHERE IDBook = @BookId";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@StatusId", statusId);
                        cmd.Parameters.AddWithValue("@BookId", bookId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статуса книги: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет статистику за день в базе данных
        /// </summary>
        private void UpdateDailyStats(int additionalBooks)
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Проверяем, есть ли запись за сегодня
                    string checkQuery = "SELECT id_record, delivery_book FROM InformationServiseTb WHERE date_record = @Date";

                    using (var cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Запись существует - обновляем
                                int currentId = Convert.ToInt32(reader["id_record"]);
                                int currentBooks = Convert.ToInt32(reader["delivery_book"]);
                                reader.Close();

                                string updateQuery = @"
                            UPDATE InformationServiseTb 
                            SET delivery_book = @Books 
                            WHERE id_record = @ID";

                                using (var updateCmd = new SqlCommand(updateQuery, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@Books", currentBooks + additionalBooks);
                                    updateCmd.Parameters.AddWithValue("@ID", currentId);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                reader.Close();
                                // Записи нет - создаем новую
                                string insertQuery = @"
                            INSERT INTO InformationServiseTb (date_record, delivery_book, delivery_certificate, events_count) 
                            VALUES (@Date, @Books, 0, 0)";

                                using (var insertCmd = new SqlCommand(insertQuery, conn))
                                {
                                    insertCmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                                    insertCmd.Parameters.AddWithValue("@Books", additionalBooks);
                                    insertCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статистики: {ex.Message}");
            }
        }
        /// <summary>
        /// Получает ID книги, у которой достаточно экземпляров для выдачи
        /// </summary>
        private int GetAvailableBookIdWithQuantity(string title, string author, int requiredQuantity)
        {
            try
            {
                string sql = @"
            SELECT TOP 1 b.IDBook, b.AvailableCopies
            FROM Books b
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE b.TitleBook = @Title 
              AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @Author
              AND b.AvailableCopies >= @RequiredQuantity
            ORDER BY b.AvailableCopies DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@RequiredQuantity", requiredQuantity);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int bookId = Convert.ToInt32(reader["IDBook"]);
                                int available = Convert.ToInt32(reader["AvailableCopies"]);
                                System.Diagnostics.Debug.WriteLine($"Найдена книга ID={bookId} с {available} экземплярами");
                                return bookId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книги: {ex.Message}");
            }

            return 0;
        }

        private void CreateGroupIssuance(int quantity)
        {
            try
            {
                List<int> bookIds = GetAvailableBookIds(_selectedBook.TitleBook, _selectedBook.AuthorName, quantity);

                if (bookIds.Count < quantity)
                {
                    CustomMessageBox.Show($"Недостаточно экземпляров книги.\n" +
                                   $"Требуется: {quantity}, доступно: {bookIds.Count}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                DateTime plannedReturnDate = DateTime.Now.AddDays(30);

                int issuanceId = _issuanceService.CreateGroupIssuanceWithBooks(
                    _selectedGroup.GroupID,
                    _selectedTeacher.TeacherID,
                    _selectedStudent.ReaderID,
                    plannedReturnDate,
                    bookIds,
                    "Урок",
                    "Групповая выдача"
                );

                // Обновляем статус каждой выданной книги на "Выдана"
                foreach (int bookId in bookIds)
                {
                    UpdateBookStatus(bookId, _statusIssuedId);
                }

                // Обновляем статистику в базе данных
                UpdateDailyStats(quantity);

                // Обновляем количество книг локально
                _selectedBook.AvailableCopies -= quantity;
                AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();

                CustomMessageBox.Show($"Групповая выдача успешно создана!\n" +
                               $"ID выдачи: {issuanceId}\n" +
                               $"Выдано книг: {quantity}",
                    "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                // Очищаем форму
                ClearSelectedBook_Click(null, null);
                _selectedGroup = null;
                _selectedTeacher = null;
                _selectedStudent = null;

                GroupSearchBox.Text = "Введите код группы...";
                GroupSearchBox.Foreground = Brushes.Gray;
                TeacherSearchBox.Text = "Введите фамилию преподавателя...";
                TeacherSearchBox.Foreground = Brushes.Gray;
                StudentSearchBox.Text = "Введите фамилию или номер билета...";
                StudentSearchBox.Foreground = Brushes.Gray;

                // Обновляем список выдач и книг
                LoadTodayIssues();
                LoadBooks();
                LoadTodayStats(); // Перезагружаем статистику из базы
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании групповой выдачи: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecord(false);
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecord(true);
        }

        private void SaveRecord(bool isUpdate)
        {
            if (!int.TryParse(BooksCountBox.Text, out int books))
                books = 0;
            if (!int.TryParse(ReferencesCountBox.Text, out int references))
                references = 0;
            if (!int.TryParse(EventsCountBox.Text, out int events))
                events = 0;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    if (isUpdate && _currentRecordId.HasValue)
                    {
                        string query = @"
                            UPDATE InformationServiseTb 
                            SET delivery_book = @Books, 
                                delivery_certificate = @References, 
                                events_count = @Events 
                            WHERE id_record = @ID";

                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Books", books);
                            cmd.Parameters.AddWithValue("@References", references);
                            cmd.Parameters.AddWithValue("@Events", events);
                            cmd.Parameters.AddWithValue("@ID", _currentRecordId.Value);
                            cmd.ExecuteNonQuery();
                        }

                        CustomMessageBox.Show("Запись обновлена!", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    }
                    else
                    {
                        string checkQuery = "SELECT COUNT(*) FROM InformationServiseTb WHERE date_record = @Date";
                        using (var checkCmd = new SqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@Date", DatePicker.SelectedDate ?? DateTime.Today);
                            int count = (int)checkCmd.ExecuteScalar();

                            if (count > 0)
                            {
                                CustomMessageBox.Show("Запись за эту дату уже существует! Используйте редактирование.",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                                return;
                            }
                        }

                        string insertQuery = @"
                            INSERT INTO InformationServiseTb (date_record, delivery_book, delivery_certificate, events_count) 
                            VALUES (@Date, @Books, @References, @Events)";

                        using (var cmd = new SqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Date", DatePicker.SelectedDate ?? DateTime.Today);
                            cmd.Parameters.AddWithValue("@Books", books);
                            cmd.Parameters.AddWithValue("@References", references);
                            cmd.Parameters.AddWithValue("@Events", events);
                            cmd.ExecuteNonQuery();
                        }

                        CustomMessageBox.Show("Запись сохранена!", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    }
                }

                LoadTodayStats();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentRecordId.HasValue)
                return;

            var result = CustomMessageBox.Show("Удалить запись за этот день?", "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        string query = "DELETE FROM InformationServiseTb WHERE id_record = @ID";
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", _currentRecordId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadTodayStats();
                    CustomMessageBox.Show("Запись удалена!", "Успех", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }
        /// <summary>
        /// Получает список ID книг, которые будут выданы (по одному ID на каждый экземпляр)
        /// </summary>
        private List<int> GetAvailableBookIds(string title, string author, int requiredQuantity)
        {
            var bookIds = new List<int>();

            try
            {
                string sql = @"
            SELECT TOP (@RequiredQuantity) b.IDBook
            FROM Books b
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE b.TitleBook = @Title 
              AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @Author
              AND b.AvailableCopies > 0
            ORDER BY b.IDBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Author", author);
                        cmd.Parameters.AddWithValue("@RequiredQuantity", requiredQuantity);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookIds.Add(Convert.ToInt32(reader["IDBook"]));
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Найдено книг: {bookIds.Count} из {requiredQuantity} требуемых");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книг: {ex.Message}");
            }

            return bookIds;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTodayStats();
            LoadTeachers();
        }

        private void ReturnBooksButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ReturnBooksPage());
        }
        private void InitializeStats()
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT id_record, delivery_book, delivery_certificate, events_count 
                FROM InformationServiseTb 
                WHERE date_record = @Date";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                _currentRecordId = Convert.ToInt32(reader["id_record"]);
                                BooksCountBox.Text = reader["delivery_book"].ToString();
                                ReferencesCountBox.Text = reader["delivery_certificate"].ToString();
                                EventsCountBox.Text = reader["events_count"].ToString();

                                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                                SaveChangesButton.Visibility = Visibility.Visible;
                                DeleteButton.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                _currentRecordId = null;
                                // Если записи нет, показываем нули (только при первой загрузке)
                                BooksCountBox.Text = "0";
                                ReferencesCountBox.Text = "0";
                                EventsCountBox.Text = "0";

                                SaveNewRecordButton.Visibility = Visibility.Visible;
                                SaveChangesButton.Visibility = Visibility.Collapsed;
                                DeleteButton.Visibility = Visibility.Collapsed;
                            }
                        }
                    }

                    UpdateStatsDisplay();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
    }
}