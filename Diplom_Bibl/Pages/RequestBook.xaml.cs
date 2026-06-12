using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using Diplom_Bibl.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Страница оформления заявки на книги для занятия
    /// Разработано для дипломного проекта
    /// </summary>
    public partial class RequestBook : Page
    {
        #region Поля и свойства

        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private readonly int _currentReaderId;
        private readonly int _currentUserTypeId;
        private ObservableCollection<BookItem> _availableBooks;
        private ObservableCollection<SelectedBookItem> _selectedBooks;
        private ObservableCollection<SchedulePeriodItem> _schedulePeriods;
        private ObservableCollection<GroupItem> _allGroups;           // Все группы для поиска
        private bool _isInitialized = false;
        private bool _isLoading = false;
        private bool _isUpdatingGroupsProgrammatically = false;       // Флаг программного обновления

        #endregion

        #region Вложенные модели данных

        public class BookItem
        {
            public int IDBook { get; set; }
            public string TitleBook { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int AvailableCopies { get; set; }
        }

        public class SelectedBookItem
        {
            public int IDBook { get; set; }
            public string BookTitle { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; } = 1;
            public int MaxQuantity { get; set; }
        }

        /// <summary>
        /// Модель учебной группы (расширенная для поиска)
        /// </summary>
        public class GroupItem
        {
            public int GroupID { get; set; }
            public string GroupCode { get; set; } = "";
            public string DisplayText => GroupCode;
        }

        public class SchedulePeriodItem
        {
            public int PeriodNumber { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string DisplayText => $"{PeriodNumber} пара: {StartTime} - {EndTime}";
        }

        #endregion

        #region Конструктор и инициализация

        public RequestBook(int readerId, int userTypeId)
        {
            InitializeComponent();

            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;

            SetupUIForUserType();
            DpLessonDateSimple.SelectedDate = DateTime.Today;

            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Collapsed;

            this.Loaded += async (s, e) => await RequestBook_LoadedAsync();
        }

        private async Task RequestBook_LoadedAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(true));

                // Загружаем данные последовательно, а не параллельно (избегаем конфликтов)
                await LoadUserDataAsync();
                await LoadScheduleForDate(DateTime.Today);
                await LoadAvailableBooksAsync();

                System.Diagnostics.Debug.WriteLine($"📊 Статус загрузки:");
                System.Diagnostics.Debug.WriteLine($"  - Групп загружено: {_allGroups?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"  - Тип пользователя: {_currentUserTypeId}");
                System.Diagnostics.Debug.WriteLine($"  - Видимость панели групп: {GroupPanel.Visibility}");
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(false));
                _isLoading = false;
            }
        }


        private void ShowLoading(bool show)
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (MainContentGrid != null)
                MainContentGrid.IsEnabled = !show;
        }

        private void SetupUIForUserType()
        {
            if (_currentUserTypeId == 3)
            {
                GroupPanel.Visibility = Visibility.Visible;
                TbLessonName.Visibility = Visibility.Visible;
                DpLessonDateSimple.Visibility = Visibility.Visible;
                CbLessonPeriod.Visibility = Visibility.Visible;
            }
            else
            {
                GroupPanel.Visibility = Visibility.Collapsed;
                TbLessonName.Visibility = Visibility.Collapsed;
                DpLessonDateSimple.Visibility = Visibility.Collapsed;
                CbLessonPeriod.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Работа с расписанием звонков

        private async Task LoadScheduleForDate(DateTime date)
        {
            try
            {
                int dayOfWeek = GetDayOfWeekNumber(date);
                bool isSummerSchedule = IsSummerSchedule(date);

                string query = @"
                    SELECT PeriodNumber, 
                           CAST(StartTime AS NVARCHAR(5)) AS StartTime,
                           CAST(EndTime AS NVARCHAR(5)) AS EndTime
                    FROM ScheduleSettings
                    WHERE DayOfWeek = @DayOfWeek 
                      AND IsSummerSchedule = @IsSummerSchedule
                      AND IsActive = 1
                    ORDER BY PeriodNumber";

                var allPeriods = new List<SchedulePeriodItem>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DayOfWeek", dayOfWeek);
                        cmd.Parameters.AddWithValue("@IsSummerSchedule", isSummerSchedule);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                allPeriods.Add(new SchedulePeriodItem
                                {
                                    PeriodNumber = reader.GetInt32(0),
                                    StartTime = reader.GetString(1),
                                    EndTime = reader.GetString(2)
                                });
                            }
                        }
                    }
                }

                var availablePeriods = allPeriods;
                if (date.Date == DateTime.Today)
                {
                    availablePeriods = FilterAvailablePeriodsByCurrentTime(allPeriods);
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _schedulePeriods = new ObservableCollection<SchedulePeriodItem>(availablePeriods);
                    CbLessonPeriod.ItemsSource = _schedulePeriods;
                    if (_schedulePeriods.Any()) CbLessonPeriod.SelectedIndex = 0;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки расписания: {ex.Message}");
            }
        }

        private List<SchedulePeriodItem> FilterAvailablePeriodsByCurrentTime(List<SchedulePeriodItem> allPeriods)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            SchedulePeriodItem currentPeriod = null;
            SchedulePeriodItem nextAvailablePeriod = null;

            foreach (var period in allPeriods)
            {
                if (!TimeSpan.TryParse(period.StartTime, out TimeSpan start)) continue;
                if (!TimeSpan.TryParse(period.EndTime, out TimeSpan end)) continue;

                if (currentTime >= start && currentTime <= end)
                {
                    currentPeriod = period;
                }

                if (start > currentTime)
                {
                    if (nextAvailablePeriod == null || period.PeriodNumber < nextAvailablePeriod.PeriodNumber)
                    {
                        nextAvailablePeriod = period;
                    }
                }
            }

            var availablePeriods = new List<SchedulePeriodItem>();
            int minAvailablePeriodNumber = 1;

            if (currentPeriod != null)
                minAvailablePeriodNumber = currentPeriod.PeriodNumber;
            else if (nextAvailablePeriod != null)
                minAvailablePeriodNumber = nextAvailablePeriod.PeriodNumber;
            else
                return availablePeriods;

            availablePeriods.AddRange(allPeriods.Where(p => p.PeriodNumber >= minAvailablePeriodNumber));
            return availablePeriods;
        }

        private int GetDayOfWeekNumber(DateTime date)
        {
            int day = (int)date.DayOfWeek;
            return day == 0 ? 7 : day;
        }

        private bool IsSummerSchedule(DateTime date)
        {
            return date >= new DateTime(date.Year, 5, 26);
        }

        private async void DpLessonDateSimple_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpLessonDateSimple.SelectedDate.HasValue)
            {
                DateTime selectedDate = DpLessonDateSimple.SelectedDate.Value.Date;

                if (selectedDate < DateTime.Today)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    });
                    DpLessonDateSimple.SelectedDate = DateTime.Today;
                    return;
                }

                await LoadScheduleForDate(selectedDate);
            }
        }

        #endregion

        #region Загрузка данных пользователя и групп (с поиском)

        private async Task LoadUserDataAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string typeQuery = "SELECT TypeName FROM ReaderType WHERE TypeID = @TypeID";
                    using (var cmd = new SqlCommand(typeQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@TypeID", _currentUserTypeId);
                        var result = await cmd.ExecuteScalarAsync();
                        await Dispatcher.InvokeAsync(() => TbUserType.Text = result?.ToString() ?? "");
                    }
                }

                if (_currentUserTypeId == 3)
                {
                    await Dispatcher.InvokeAsync(() => GroupPanel.Visibility = Visibility.Visible);
                    await LoadAllGroupsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка всех групп с поддержкой поиска
        /// </summary>
        private async Task LoadAllGroupsAsync()
        {
            try
            {
                const string query = "SELECT GroupID, GroupCode FROM Groups ORDER BY GroupCode";

                var groups = new ObservableCollection<GroupItem>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int groupId = 0;
                            if (!reader.IsDBNull(0))
                            {
                                groupId = Convert.ToInt32(reader.GetValue(0));
                            }

                            string groupCode = "";
                            if (!reader.IsDBNull(1))
                            {
                                groupCode = reader.GetString(1);
                            }

                            if (groupId > 0 && !string.IsNullOrEmpty(groupCode))
                            {
                                groups.Add(new GroupItem
                                {
                                    GroupID = groupId,
                                    GroupCode = groupCode
                                });
                            }
                        }
                    }
                }

                _allGroups = groups;

                await Dispatcher.InvokeAsync(() =>
                {
                    CbGroup.ItemsSource = _allGroups;
                    // Убираем DisplayMemberPath - используем только ItemTemplate из XAML
                    CbGroup.SelectedValuePath = "GroupID";
                    CbGroup.Text = "";

                    System.Diagnostics.Debug.WriteLine($"✅ Загружено групп: {_allGroups.Count}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки групп: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private void FilterGroups(string searchText)
        {
            if (_allGroups == null) return;

            ObservableCollection<GroupItem> filteredGroups;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredGroups = new ObservableCollection<GroupItem>(_allGroups);
            }
            else
            {
                var filtered = _allGroups
                    .Where(g => g.GroupCode.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                filteredGroups = new ObservableCollection<GroupItem>(filtered);
            }

            CbGroup.ItemsSource = filteredGroups;
            // Убираем DisplayMemberPath - используем только ItemTemplate из XAML
            CbGroup.SelectedValuePath = "GroupID";

            if (filteredGroups.Count == 1 && !string.IsNullOrWhiteSpace(searchText))
            {
                CbGroup.SelectedItem = filteredGroups[0];
            }

            System.Diagnostics.Debug.WriteLine($"🔍 Фильтрация групп: '{searchText}' -> найдено {filteredGroups.Count}");
        }


        
        /// <summary>
        /// Обработчик изменения текста в комбобоксе (поиск)
        /// </summary>
        private void CbGroup_KeyUp(object sender, KeyEventArgs e)
        {
            if (_isUpdatingGroupsProgrammatically) return;

            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            string searchText = comboBox.Text;
            FilterGroups(searchText);

            if (CbGroup.Items.Count > 0 && !comboBox.IsDropDownOpen)
            {
                comboBox.IsDropDownOpen = true;
            }
        }

        /// <summary>
        /// Обработчик закрытия выпадающего списка
        /// </summary>
        private void CbGroup_DropDownClosed(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            if (comboBox.SelectedItem == null && !string.IsNullOrWhiteSpace(comboBox.Text))
            {
                string searchText = comboBox.Text;
                var foundGroup = _allGroups?.FirstOrDefault(g =>
                    g.GroupCode.Equals(searchText, StringComparison.OrdinalIgnoreCase));

                if (foundGroup != null)
                {
                    _isUpdatingGroupsProgrammatically = true;
                    comboBox.SelectedItem = foundGroup;
                    _isUpdatingGroupsProgrammatically = false;
                }
            }

            if (string.IsNullOrWhiteSpace(comboBox.Text))
            {
                FilterGroups("");
            }
        }
       
        /// <summary>
        /// Получение ID выбранной группы
        /// </summary>
        private int? GetSelectedGroupId()
        {
            if (CbGroup.SelectedItem is GroupItem selectedGroup)
            {
                return selectedGroup.GroupID;
            }

            if (CbGroup.SelectedValue != null && CbGroup.SelectedValue is int id && id > 0)
            {
                return id;
            }

            return null;
        }

        #endregion

        #region Работа с книгами

        private async Task LoadAvailableBooksAsync(string searchTerm = "")
        {
            try
            {
                // Выполняем запрос в фоне, но обновляем UI через Dispatcher
                var booksList = await Task.Run(async () =>
                {
                    string query = @"
                SELECT
                    MIN(b.IDBook) AS IDBook,
                    b.TitleBook,
                    ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                    COUNT(*) AS AvailableCopies
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                INNER JOIN StatusBook sb ON b.StatusBook = sb.ID_Status
                WHERE sb.ID_Status = 1
                  AND b.AvailableCopies = 1";

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        query += " AND (b.TitleBook LIKE @Search OR a.SurnameAuthor LIKE @Search)";
                    }

                    query += @"
                GROUP BY
                    b.TitleBook,
                    a.SurnameAuthor,
                    a.NameAuthor,
                    a.PatronymicAuthor
                ORDER BY b.TitleBook";

                    var result = new List<BookItem>();

                    using (var conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            if (!string.IsNullOrWhiteSpace(searchTerm))
                            {
                                cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                            }

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    result.Add(new BookItem
                                    {
                                        IDBook = reader.GetInt32(0),
                                        TitleBook = reader.GetString(1),
                                        AuthorName = reader.GetString(2),
                                        AvailableCopies = reader.GetInt32(3)
                                    });
                                }
                            }
                        }
                    }

                    return result;
                });

                // Обновляем UI один раз после завершения загрузки
                await Dispatcher.InvokeAsync(() =>
                {
                    _availableBooks = new ObservableCollection<BookItem>(booksList);
                    DgAvailableBooks.ItemsSource = _availableBooks;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private void AddBookToRequest(BookItem book)
        {
            if (_selectedBooks == null)
                _selectedBooks = new ObservableCollection<SelectedBookItem>();

            if (book.AvailableCopies <= 0)
            {
                CustomMessageBox.Show($"Книга '{book.TitleBook}' временно недоступна.",
                    "Книга недоступна", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            SelectedBookItem existing = _selectedBooks.FirstOrDefault(b => b.IDBook == book.IDBook);

            if (existing != null)
            {
                if (_currentUserTypeId != 3 && existing.Quantity >= 1)
                {
                    CustomMessageBox.Show("Для индивидуальной выдачи можно взять только 1 экземпляр книги.",
                        "Ограничение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                if (existing.Quantity + 1 <= book.AvailableCopies)
                    existing.Quantity++;
                else
                    CustomMessageBox.Show($"Доступно только {book.AvailableCopies} экземпляров этой книги.",
                        "Предупреждение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
            else
            {
                int maxQuantity = (_currentUserTypeId == 3) ? book.AvailableCopies : 1;

                _selectedBooks.Add(new SelectedBookItem
                {
                    IDBook = book.IDBook,
                    BookTitle = book.TitleBook,
                    AuthorName = book.AuthorName,
                    Quantity = 1,
                    MaxQuantity = maxQuantity
                });
            }

            DgSelectedBooks.ItemsSource = _selectedBooks;
            UpdateTotalCount();
        }

        private void UpdateTotalCount()
        {
            int total = _selectedBooks?.Sum(b => b.Quantity) ?? 0;
            TbTotalBooks.Text = total.ToString();
        }

        #endregion

        #region Генерация номера заявки

        private string GenerateUniqueRequestNumber()
        {
            try
            {
                string datePart = DateTime.Now.ToString("yyyyMMdd");
                int sequence = 1;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT TOP 1 RequestNumber 
                        FROM BookRequests 
                        WHERE RequestNumber LIKE @Pattern 
                        ORDER BY ID_Request DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Pattern", $"ЗАЯВ-{datePart}-%");
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            string lastNumber = result.ToString();
                            int lastDashIndex = lastNumber.LastIndexOf('-');
                            if (lastDashIndex >= 0 && lastDashIndex + 1 < lastNumber.Length)
                            {
                                string seqStr = lastNumber.Substring(lastDashIndex + 1);
                                if (int.TryParse(seqStr, out int lastSeq))
                                    sequence = lastSeq + 1;
                            }
                        }
                    }
                }

                return $"ЗАЯВ-{datePart}-{sequence:D4}";
            }
            catch
            {
                return $"ЗАЯВ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1, 9999):D4}";
            }
        }

        #endregion

        #region Обработчики событий UI

        private async void TbBookSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadAvailableBooksAsync(TbBookSearch.Text.Trim());
        }

        private async void BtnSearchBook_Click(object sender, RoutedEventArgs e)
        {
            await LoadAvailableBooksAsync(TbBookSearch.Text.Trim());
        }

        private async void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TbBookSearch.Clear();
            await LoadAvailableBooksAsync();
        }

        private void DgAvailableBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgAvailableBooks.SelectedItem is BookItem selectedBook)
                AddBookToRequest(selectedBook);
        }

        private void BtnRemoveBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SelectedBookItem selectedBook && _selectedBooks != null)
            {
                _selectedBooks.Remove(selectedBook);
                DgSelectedBooks.ItemsSource = _selectedBooks;
                UpdateTotalCount();
            }
        }

        private void Quantity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is IntegerUpDown spinner && spinner.DataContext is SelectedBookItem book)
            {
                int newValue = spinner.Value ?? 1;
                if (newValue < 1)
                    spinner.Value = 1;
                if (newValue > book.MaxQuantity)
                {
                    CustomMessageBox.Show($"Доступно только {book.MaxQuantity} экземпляров этой книги.", "Предупреждение",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    spinner.Value = book.MaxQuantity;
                }
                book.Quantity = spinner.Value ?? 1;
                UpdateTotalCount();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (NavigationService != null && NavigationService.CanGoBack)
                    NavigationService.GoBack();
                else
                    Application.Current.MainWindow?.Close();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        #endregion

        #region Валидация и отправка заявки

        private bool ValidateForm()
        {
            if (_currentUserTypeId == 3)
            {
                if (string.IsNullOrWhiteSpace(TbLessonName.Text))
                {
                    CustomMessageBox.Show("Введите название занятия/предмета.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }

                if (!DpLessonDateSimple.SelectedDate.HasValue)
                {
                    CustomMessageBox.Show("Выберите дату занятия.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }

                if (DpLessonDateSimple.SelectedDate.Value.Date < DateTime.Today)
                {
                    CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }

                // НОВАЯ ПРОВЕРКА: выбранная группа
                int? selectedGroupId = GetSelectedGroupId();
                if (selectedGroupId == null || selectedGroupId <= 0)
                {
                    CustomMessageBox.Show("Выберите группу из списка или введите корректный код группы.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    CbGroup.Focus();
                    return false;
                }

                if (CbLessonPeriod.SelectedItem == null)
                {
                    CustomMessageBox.Show("Выберите пару / время занятия.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }
            }

            if (_selectedBooks == null || _selectedBooks.Count == 0)
            {
                CustomMessageBox.Show("Добавьте хотя бы одну книгу в заявку.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            if (_currentUserTypeId != 3)
            {
                foreach (var book in _selectedBooks)
                {
                    if (book.Quantity > 1)
                    {
                        CustomMessageBox.Show($"Для индивидуальной выдачи можно взять только 1 экземпляр книги '{book.BookTitle}'.",
                            "Ограничение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            return true;
        }

        private async void BtnSubmitRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            BtnSubmitRequest.IsEnabled = false;

            try
            {
                ShowLoading(true);

                int? groupId = GetSelectedGroupId();
                DateTime lessonDate = DpLessonDateSimple.SelectedDate.HasValue
                    ? DpLessonDateSimple.SelectedDate.Value.Date
                    : DateTime.Today;

                string lessonTime = null;
                if (CbLessonPeriod.SelectedItem is SchedulePeriodItem selectedPeriod)
                {
                    lessonTime = selectedPeriod.StartTime;
                }

                string generatedNumber = GenerateUniqueRequestNumber();

                int requestId = 0;

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем заявку
                            string insertRequestSql = @"
                        INSERT INTO BookRequests (
                            RequestNumber, UserID, UserTypeID, GroupID, LessonName, 
                            LessonDate, LessonTime, StatusID, RequestDate, ExpirationDate, CreatedDate
                        ) VALUES (
                            @RequestNumber, @UserID, @UserTypeID, @GroupID, @LessonName,
                            @LessonDate, @LessonTime, 
                            (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Новая'),
                            GETDATE(), DATEADD(DAY, 1, @LessonDate), GETDATE()
                        );
                        SELECT SCOPE_IDENTITY();";

                            using (var cmd = new SqlCommand(insertRequestSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@RequestNumber", generatedNumber);
                                cmd.Parameters.AddWithValue("@UserID", _currentReaderId);
                                cmd.Parameters.AddWithValue("@UserTypeID", _currentUserTypeId);
                                cmd.Parameters.AddWithValue("@GroupID", groupId.HasValue ? (object)groupId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@LessonName", TbLessonName.Text.Trim());
                                cmd.Parameters.Add("@LessonDate", SqlDbType.Date).Value = lessonDate;

                                if (!string.IsNullOrWhiteSpace(lessonTime) && TimeSpan.TryParse(lessonTime, out TimeSpan parsedTime))
                                {
                                    cmd.Parameters.Add("@LessonTime", SqlDbType.Time).Value = parsedTime;
                                }
                                else
                                {
                                    cmd.Parameters.Add("@LessonTime", SqlDbType.Time).Value = DBNull.Value;
                                }

                                requestId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            // 2. Добавляем книги в заявку
                            string insertItemsSql = @"
                        INSERT INTO RequestItems (RequestID, BookID, Quantity, StatusID)
                        VALUES (@RequestID, @BookID, @Quantity, 
                            (SELECT ID_Status FROM RequestItemStatuses WHERE StatusName = 'Ожидает'))";

                            foreach (var book in _selectedBooks)
                            {
                                using (var cmd = new SqlCommand(insertItemsSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestID", requestId);
                                    cmd.Parameters.AddWithValue("@BookID", book.IDBook);
                                    cmd.Parameters.AddWithValue("@Quantity", book.Quantity);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            // 3. Создаем уведомление для библиотекаря
                            string insertNotificationSql = @"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        VALUES (@RequestID, 
                            (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Новая заявка'),
                            @Message, 0, GETDATE())";

                            using (var cmd = new SqlCommand(insertNotificationSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@RequestID", requestId);
                                cmd.Parameters.AddWithValue("@Message", $"Новая заявка №{generatedNumber}");
                                await cmd.ExecuteNonQueryAsync();
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

                // ПОКАЗЫВАЕМ СООБЩЕНИЕ ПОЛЬЗОВАТЕЛЮ (СРАЗУ)
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Заявка №{generatedNumber} успешно создана!\n\n",
                        "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                });

                // ОЧИЩАЕМ ФОРМУ
                await ClearForm();

                // ОБНОВЛЯЕМ УВЕДОМЛЕНИЯ В ФОНОВОМ ПОТОКЕ (без ожидания)
                _ = Task.Run(async () =>
                {
                    // Даём время БД на сохранение
                    await Task.Delay(500);
                    await ForceUpdateNotificationsForLibrarians();
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка при создании заявки: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
            finally
            {
                ShowLoading(false);
                BtnSubmitRequest.IsEnabled = true;
            }
        }

        private async Task ForceUpdateNotificationsForLibrarians()
        {
            try
            {
                // Находим все открытые окна MainWindow
                var mainWindows = Application.Current.Windows.OfType<MainWindow>();

                foreach (var mainWindow in mainWindows)
                {
                    // Получаем сервис уведомлений через публичный метод или свойство
                    // Если нет публичного доступа, используем рефлексию (медленно, но терпимо)
                    var notificationServiceField = typeof(MainWindow).GetField("_notificationService",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (notificationServiceField != null)
                    {
                        var notificationService = notificationServiceField.GetValue(mainWindow) as NotificationService;
                        if (notificationService != null)
                        {
                            // ИСПРАВЛЕНО: Добавляем задержку перед проверкой
                            await Task.Delay(100);
                            await notificationService.CheckNewNotifications();
                            System.Diagnostics.Debug.WriteLine("✅ Уведомления библиотекаря обновлены");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка принудительного обновления уведомлений: {ex.Message}");
            }
        }

        private async Task ClearForm()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TbLessonName.Clear();
                DpLessonDateSimple.SelectedDate = DateTime.Today;
                CbLessonPeriod.SelectedIndex = -1;
                CbGroup.SelectedIndex = -1;
                CbGroup.Text = "";  // Очищаем текст поиска
                if (_selectedBooks != null)
                    _selectedBooks.Clear();
                DgSelectedBooks.ItemsSource = null;
                UpdateTotalCount();
            });
            await LoadAvailableBooksAsync();
        }

        #endregion
    }
}