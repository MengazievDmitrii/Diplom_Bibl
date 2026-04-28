using Diplom_Bibl.Controls;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class InformationServicePage : Page

    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private int? _pendingUserTypeId = null;
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

        // Данные из заявки для автоматического заполнения
        private int? _pendingRequestId = null;
        private int? _pendingUserId = null;
        private int? _pendingGroupId = null;
        private string _pendingUserName = "";
        private string _pendingGroupCode = "";
        private string _pendingLessonName = "";
        private DateTime? _pendingLessonDate = null;

        // Класс для книг из заявки (с количеством)
        public class RequestBookItem
        {
            public int IDBook { get; set; }
            public string TitleBook { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public int AvailableCopies { get; set; }
        }

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
            LoadStatusIds();

            // Подписываемся на события
            this.Loaded += (s, e) => UpdateInventoryPreview();
        }

        /// <summary>
        /// Устанавливает данные из заявки для автоматического заполнения
        /// </summary>
        public void SetDataFromRequest(int requestId, int userId, int? groupId, string userName, string groupCode, string lessonName, DateTime lessonDate)
        {
            _pendingRequestId = requestId;  // Это важно!
            _pendingUserId = userId;
            _pendingGroupId = groupId;
            _pendingUserName = userName;
            _pendingGroupCode = groupCode;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = null;
        }

        private void LoadStatusIds()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string availableQuery = "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%'";
                    using (var cmd = new SqlCommand(availableQuery, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                            _statusAvailableId = Convert.ToInt32(result);
                    }

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

                foreach (var item in _todayIssues)
                {
                    item.OverdueColor = item.IsOverdue ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }

                IssuesDataGrid.ItemsSource = _todayIssues;
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

                if (search == "Введите фамилию преподавателя..." || string.IsNullOrWhiteSpace(search))
                {
                    TeachersListBox.Visibility = Visibility.Collapsed;
                    return;
                }

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

                SelectedBookTitle.Text = selected.TitleBook;
                SelectedBookAuthor.Text = selected.AuthorName;
                SelectedBookPanel.Visibility = Visibility.Visible;

                BookSearchBox.Text = selected.TitleBook;
                BookSearchBox.Foreground = Brushes.Black;
                BooksListBox.Visibility = Visibility.Collapsed;

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

            if (requestedQuantity > _selectedBook.AvailableCopies)
            {
                CustomMessageBox.Show($"Недостаточно экземпляров. Доступно: {_selectedBook.AvailableCopies}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (ReaderTypeRadio.IsChecked == true)
            {
                if (_selectedReader == null)
                {
                    CustomMessageBox.Show("Выберите читателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }
                CreateIndividualIssuance(requestedQuantity);
            }
            else
            {
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

        private async void CreateIndividualIssuance(int quantity)
        {
            try
            {
                int bookId = GetAvailableIDBookWithQuantity(_selectedBook.TitleBook, _selectedBook.AuthorName, 1);
                if (bookId == 0)
                {
                    CustomMessageBox.Show("Не найдено доступных экземпляров книги.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                DateTime plannedReturnDate = DateTime.Now.AddDays(14);

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Начинаем транзакцию
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем выдачу
                            using (var cmd = new SqlCommand("sp_CreateIndividualIssuance", conn, transaction))
                            {
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@BookId", bookId);
                                cmd.Parameters.AddWithValue("@ReaderId", _selectedReader.ReaderID);
                                cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                                cmd.Parameters.AddWithValue("@Notes", "Выдача через информационное обслуживание");

                                var issuanceIdParam = cmd.Parameters.Add("@IssuanceId", System.Data.SqlDbType.Int);
                                issuanceIdParam.Direction = System.Data.ParameterDirection.Output;

                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. ОБНОВЛЯЕМ СТАТУС КНИГИ НА "ВЫДАНА" (StatusBook = 3)
                            string updateBookStatus = @"
                        UPDATE Books 
                        SET StatusBook = 3,
                            CopiesInUse = CopiesInUse + 1,
                            AvailableCopies = AvailableCopies - 1
                        WHERE IDBook = @BookId";

                            using (var cmd = new SqlCommand(updateBookStatus, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BookId", bookId);
                                await cmd.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"Статус книги ID={bookId} обновлен на 'Выдан'");
                            }

                            // 3. Обновляем статус заявки на "Выдана"
                            if (_pendingRequestId.HasValue)
                            {
                                string updateRequestStatus = @"
                            UPDATE BookRequests 
                            SET StatusID = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Выдана'), 
                                IssuedDate = GETDATE() 
                            WHERE ID_Request = @RequestId";

                                using (var cmd = new SqlCommand(updateRequestStatus, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestId", _pendingRequestId.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // 4. Обновляем статус элементов заявки на "Выдана"
                                string updateItemsStatus = @"
                            UPDATE RequestItems 
                            SET StatusID = (SELECT ID_Status FROM RequestItemStatuses WHERE StatusName = 'Выдана'),
                                IssuedQuantity = Quantity
                            WHERE RequestID = @RequestId";

                                using (var cmd = new SqlCommand(updateItemsStatus, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestId", _pendingRequestId.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                UpdateDailyStats(quantity);

                // Обновляем локальное состояние книги
                _selectedBook.AvailableCopies -= quantity;

                CustomMessageBox.Show($"Индивидуальная выдача успешно создана!\nВыдано книг: {quantity}", "Успех",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании выдачи: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void UpdateDailyStats(int additionalBooks)
        {
            try
            {
                DateTime selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT id_record, delivery_book FROM InformationServiseTb WHERE date_record = @Date";

                    using (var cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
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

        private int GetAvailableIDBookWithQuantity(string title, string author, int requiredQuantity)
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
                                int IDBook = Convert.ToInt32(reader["IDBook"]);
                                System.Diagnostics.Debug.WriteLine($"Найдена книга ID={IDBook}");
                                return IDBook;
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

        private async void CreateGroupIssuance(int quantity)
        {
            try
            {
                List<int> bookIds = GetAvailableBookIds(_selectedBook.TitleBook, _selectedBook.AuthorName, quantity);

                if (bookIds.Count < quantity)
                {
                    CustomMessageBox.Show($"Недостаточно экземпляров книги.\nТребуется: {quantity}, доступно: {bookIds.Count}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                DateTime plannedReturnDate = DateTime.Now.AddDays(30);
                string bookIdsString = string.Join(",", bookIds);

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Начинаем транзакцию
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем групповую выдачу
                            using (var cmd = new SqlCommand("sp_CreateGroupIssuance", conn, transaction))
                            {
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@GroupId", _selectedGroup.GroupID);
                                cmd.Parameters.AddWithValue("@TeacherId", _selectedTeacher.TeacherID);
                                cmd.Parameters.AddWithValue("@StudentId", _selectedStudent.ReaderID);
                                cmd.Parameters.AddWithValue("@PlannedReturnDate", plannedReturnDate);
                                cmd.Parameters.AddWithValue("@SubjectName", "Урок");
                                cmd.Parameters.AddWithValue("@Notes", "Групповая выдача");
                                cmd.Parameters.AddWithValue("@BookIds", bookIdsString);

                                var issuanceIdParam = cmd.Parameters.Add("@GroupIssuanceId", System.Data.SqlDbType.Int);
                                issuanceIdParam.Direction = System.Data.ParameterDirection.Output;

                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. ОБНОВЛЯЕМ СТАТУС КАЖДОЙ ВЫДАННОЙ КНИГИ НА "ВЫДАНА" (StatusBook = 3)
                            foreach (int bookId in bookIds)
                            {
                                string updateBookStatus = @"
                            UPDATE Books 
                            SET StatusBook = 3,
                                CopiesInUse = CopiesInUse + 1,
                                AvailableCopies = AvailableCopies - 1
                            WHERE IDBook = @BookId";

                                using (var cmd = new SqlCommand(updateBookStatus, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BookId", bookId);
                                    await cmd.ExecuteNonQueryAsync();
                                    System.Diagnostics.Debug.WriteLine($"Статус книги ID={bookId} обновлен на 'Выдан'");
                                }
                            }

                            // 3. Обновляем статус заявки на "Выдана"
                            if (_pendingRequestId.HasValue)
                            {
                                string updateRequestStatus = @"
                            UPDATE BookRequests 
                            SET StatusID = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Выдана'), 
                                IssuedDate = GETDATE() 
                            WHERE ID_Request = @RequestId";

                                using (var cmd = new SqlCommand(updateRequestStatus, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestId", _pendingRequestId.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // 4. Обновляем статус элементов заявки на "Выдана"
                                string updateItemsStatus = @"
                            UPDATE RequestItems 
                            SET StatusID = (SELECT ID_Status FROM RequestItemStatuses WHERE StatusName = 'Выдана'),
                                IssuedQuantity = Quantity
                            WHERE RequestID = @RequestId";

                                using (var cmd = new SqlCommand(updateItemsStatus, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestId", _pendingRequestId.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                UpdateDailyStats(quantity);

                // Обновляем локальное состояние книги
                _selectedBook.AvailableCopies -= quantity;

                CustomMessageBox.Show($"Групповая выдача успешно создана!\nВыдано книг: {quantity}", "Успех",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                // Возвращаемся на страницу управления заявками
                NavigationService?.GoBack();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании групповой выдачи: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

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
              AND b.StatusBook = (SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%')
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
                                int idBook = Convert.ToInt32(reader["IDBook"]);
                                bookIds.Add(idBook);
                                System.Diagnostics.Debug.WriteLine($"Добавлена книга ID={idBook}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения ID книг: {ex.Message}");
            }
            return bookIds;
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTodayStats();
            LoadTeachers();

            if (_pendingRequestId.HasValue)
            {
                AutoFillFromRequest();
            }
        }


        private async void AutoFillFromRequest()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AutoFillFromRequest: RequestId={_pendingRequestId}, GroupId={_pendingGroupId}, UserTypeId={_pendingUserTypeId}");

                if (_pendingGroupId.HasValue && _pendingGroupId > 0)
                {
                    GroupTypeRadio.IsChecked = true;
                    await LoadAndSelectGroup(_pendingGroupCode);
                    await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                }
                else
                {
                    ReaderTypeRadio.IsChecked = true;
                    if (_pendingUserTypeId == 3)
                    {
                        await LoadAndSelectTeacher(_pendingUserId.Value, _pendingUserName);
                    }
                    else
                    {
                        await LoadAndSelectReader(_pendingUserId.Value, _pendingUserName);
                    }
                }

                // ВАЖНО: Загружаем книги ПОСЛЕ того, как определили тип выдачи
                await LoadRequestBooks(_pendingRequestId.Value);

                CustomMessageBox.Show($"Данные из заявки №{_pendingRequestId} загружены.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка автозаполнения: {ex.Message}");
                CustomMessageBox.Show($"Ошибка загрузки данных из заявки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private async System.Threading.Tasks.Task LoadAndSelectTeacher(int userId, string userName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                SELECT ReaderID, LastName, FirstName, MiddleName, CardNumber 
                FROM ReaderForm 
                WHERE ReaderID = @UserId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var teacher = new BookIssuanceService.TeacherInfo
                                {
                                    TeacherID = Convert.ToInt32(reader["ReaderID"]),
                                    LastName = reader["LastName"].ToString(),
                                    FirstName = reader["FirstName"].ToString(),
                                    MiddleName = reader["MiddleName"]?.ToString()
                                };
                                _selectedTeacher = teacher;
                                TeacherSearchBox.Text = teacher.FullName;
                                TeacherSearchBox.Foreground = Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки преподавателя: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadAndSelectGroup(string groupCode)
        {
            if (string.IsNullOrEmpty(groupCode)) return;
            try
            {
                var group = _groups.FirstOrDefault(g => g.GroupCode == groupCode);
                if (group != null)
                {
                    _selectedGroup = group;
                    GroupSearchBox.Text = group.GroupCode;
                    GroupSearchBox.Foreground = Brushes.Black;
                    await LoadStudentsByGroup(group.GroupID);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки группы: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadStudentsByGroup(int groupId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT DISTINCT
                            rf.ReaderID,
                            rf.LastName,
                            rf.FirstName,
                            rf.MiddleName,
                            rf.CardNumber
                        FROM ReaderForm rf
                        WHERE rf.GroupID = @GroupId AND rf.TypeID = 1
                        ORDER BY rf.LastName, rf.FirstName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            _students.Clear();
                            while (await reader.ReadAsync())
                            {
                                _students.Add(new BookIssuanceService.ReaderInfo
                                {
                                    ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                    LastName = reader["LastName"].ToString(),
                                    FirstName = reader["FirstName"].ToString(),
                                    MiddleName = reader["MiddleName"]?.ToString(),
                                    CardNumber = reader["CardNumber"].ToString()
                                });
                            }
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Загружено студентов для группы {groupId}: {_students.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки студентов: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadAndSelectStudent(int userId, string userName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT ReaderID, LastName, FirstName, MiddleName, CardNumber 
                        FROM ReaderForm 
                        WHERE ReaderID = @UserId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var student = new BookIssuanceService.ReaderInfo
                                {
                                    ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                    LastName = reader["LastName"].ToString(),
                                    FirstName = reader["FirstName"].ToString(),
                                    MiddleName = reader["MiddleName"]?.ToString(),
                                    CardNumber = reader["CardNumber"].ToString()
                                };
                                _selectedStudent = student;
                                StudentSearchBox.Text = student.DisplayInfo;
                                StudentSearchBox.Foreground = Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки студента: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadAndSelectReader(int userId, string userName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT ReaderID, LastName, FirstName, MiddleName, CardNumber 
                        FROM ReaderForm 
                        WHERE ReaderID = @UserId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var readerInfo = new BookIssuanceService.ReaderInfo
                                {
                                    ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                    LastName = reader["LastName"].ToString(),
                                    FirstName = reader["FirstName"].ToString(),
                                    MiddleName = reader["MiddleName"]?.ToString(),
                                    CardNumber = reader["CardNumber"].ToString()
                                };
                                _selectedReader = readerInfo;
                                ReaderSearchBox.Text = readerInfo.DisplayInfo;
                                ReaderSearchBox.Foreground = Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки читателя: {ex.Message}");
            }
        }

        // FIX 3: Получение реального доступного количества через GetActualAvailableCopies
        private async System.Threading.Tasks.Task LoadRequestBooks(int requestId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== LoadRequestBooks START ===");
                System.Diagnostics.Debug.WriteLine($"RequestId: {requestId}");

                string query = @"
            SELECT 
                MIN(ri.BookID) AS BookID,
                b.TitleBook,
                ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                SUM(ri.Quantity) AS Quantity
            FROM RequestItems ri
            JOIN Books b ON ri.BookID = b.IDBook
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE ri.RequestID = @RequestId
            GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor";

                var booksFromRequest = new List<RequestBookItem>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                booksFromRequest.Add(new RequestBookItem
                                {
                                    IDBook = Convert.ToInt32(reader["BookID"]),
                                    TitleBook = reader["TitleBook"].ToString(),
                                    AuthorName = reader["AuthorName"].ToString(),
                                    Quantity = Convert.ToInt32(reader["Quantity"]),
                                    AvailableCopies = 0
                                });
                                System.Diagnostics.Debug.WriteLine($"Книга из заявки: {reader["TitleBook"]}, Кол-во: {reader["Quantity"]}");
                            }
                        }
                    }
                }

                // Для каждой книги получаем ОБЩЕЕ реальное доступное количество
                foreach (var book in booksFromRequest)
                {
                    book.AvailableCopies = await GetTotalActualAvailableCopies(book.TitleBook, book.AuthorName, requestId);
                    System.Diagnostics.Debug.WriteLine($"Книга: {book.TitleBook}, Доступно: {book.AvailableCopies}, Запрошено: {book.Quantity}");
                }

                if (booksFromRequest.Any())
                {
                    var firstBook = booksFromRequest.First();

                    // Создаём объект книги для отображения
                    var newBook = new BookIssuanceService.BookInfo
                    {
                        IDBook = firstBook.IDBook,
                        TitleBook = firstBook.TitleBook,
                        AuthorName = firstBook.AuthorName,
                        AvailableCopies = firstBook.AvailableCopies
                    };

                    // Проверяем, есть ли уже такая книга в коллекции _books
                    var existingBook = _books.FirstOrDefault(b => b.TitleBook == firstBook.TitleBook && b.AuthorName == firstBook.AuthorName);
                    if (existingBook != null)
                    {
                        existingBook.AvailableCopies = firstBook.AvailableCopies;
                        _selectedBook = existingBook;
                    }
                    else
                    {
                        _books.Add(newBook);
                        _selectedBook = newBook;
                    }

                    // Отображаем выбранную книгу
                    SelectedBookTitle.Text = _selectedBook.TitleBook;
                    SelectedBookAuthor.Text = _selectedBook.AuthorName;
                    SelectedBookPanel.Visibility = Visibility.Visible;
                    BookSearchBox.Text = _selectedBook.TitleBook;
                    BookSearchBox.Foreground = System.Windows.Media.Brushes.Black;
                    AvailableQuantityText.Text = _selectedBook.AvailableCopies.ToString();

                    // Устанавливаем количество
                    if (GroupTypeRadio.IsChecked == true)
                    {
                        QuantityBox.Text = firstBook.Quantity.ToString();
                        QuantityBox.IsEnabled = true;
                    }
                    else
                    {
                        QuantityBox.Text = firstBook.Quantity.ToString();
                        QuantityBox.IsEnabled = false;
                    }

                    System.Diagnostics.Debug.WriteLine($"Выбрана книга: {_selectedBook.TitleBook}, Доступно: {_selectedBook.AvailableCopies}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("В заявке нет книг!");
                    CustomMessageBox.Show("В заявке нет книг для выдачи.", "Внимание",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг из заявки: {ex.Message}");
                CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        // Добавьте этот метод для получения реального количества доступных книг
        // FIX 2 & 3: Атомарный расчёт реально доступного количества
        private async Task<int> GetActualAvailableCopies(int bookId, int? excludeRequestId = null)
        {
            try
            {
                string query = @"
            SELECT 
                b.AvailableCopies - ISNULL((
                    SELECT SUM(ri.Quantity)
                    FROM RequestItems ri
                    INNER JOIN BookRequests br ON ri.RequestID = br.ID_Request
                    WHERE ri.BookID = @BookID
                      AND ri.StatusID IN (1, 2)
                      AND br.StatusID NOT IN (4, 5, 6)
                ), 0) AS ActuallyAvailable
            FROM Books b
            WHERE b.IDBook = @BookID";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BookID", bookId);
                        var result = await cmd.ExecuteScalarAsync();
                        int available = result != DBNull.Value ? Convert.ToInt32(result) : 0;

                        // Если есть ID заявки, прибавляем обратно количество книг из ЭТОЙ заявки
                        if (excludeRequestId.HasValue && excludeRequestId.Value > 0)
                        {
                            string getCurrentRequestQuantity = @"
                        SELECT ISNULL(SUM(Quantity), 0)
                        FROM RequestItems
                        WHERE RequestID = @RequestID AND BookID = @BookID";

                            using (var cmd2 = new SqlCommand(getCurrentRequestQuantity, conn))
                            {
                                cmd2.Parameters.AddWithValue("@RequestID", excludeRequestId.Value);
                                cmd2.Parameters.AddWithValue("@BookID", bookId);
                                int currentRequestQuantity = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
                                available += currentRequestQuantity;
                            }
                        }

                        return available;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
        // Получение ОБЩЕГО реального количества доступных книг по названию и автору
        private async Task<int> GetTotalActualAvailableCopies(string title, string author, int excludeRequestId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== GetTotalActualAvailableCopies ===");
                System.Diagnostics.Debug.WriteLine($"Title: {title}, Author: {author}, ExcludeRequestId: {excludeRequestId}");

                // Просто возвращаем общее количество, игнорируя резервирования
                string totalQuery = @"
            SELECT ISNULL(SUM(b.AvailableCopies), 0)
            FROM Books b
            WHERE b.TitleBook = @Title";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(totalQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        var result = await cmd.ExecuteScalarAsync();
                        int totalAvailable = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"Total Available (без вычета резервов): {totalAvailable}");
                        return totalAvailable;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                return 0;
            }
        }
        // Добавьте этот метод для загрузки доступности книги
        private async System.Threading.Tasks.Task LoadBookAvailability(RequestBookItem book)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT AvailableCopies FROM Books WHERE IDBook = @IDBook";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IDBook", book.IDBook);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null)
                        {
                            book.AvailableCopies = Convert.ToInt32(result);
                            System.Diagnostics.Debug.WriteLine($"Книга ID={book.IDBook}, Доступно: {book.AvailableCopies}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки доступности книги: {ex.Message}");
            }
        }
        /// <summary>
        /// Устанавливает данные из заявки для автоматического заполнения (индивидуальная выдача)
        /// </summary>
        public void SetDataFromRequestIndividual(int requestId, int userId, string userName, string lessonName, DateTime lessonDate, int userTypeId)
        {
            _pendingRequestId = requestId;  // Это важно!
            _pendingUserId = userId;
            _pendingUserName = userName;
            _pendingLessonName = lessonName;
            _pendingLessonDate = lessonDate;
            _pendingUserTypeId = userTypeId;
            _pendingGroupId = null;
            _pendingGroupCode = "";
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