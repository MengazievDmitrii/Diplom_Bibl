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

        private readonly string _connectionString = DatabaseHelper.ConnectionString; // Строка подключения к БД
        private readonly int _currentReaderId;      // ID текущего читателя/преподавателя
        private readonly int _currentUserTypeId;    // Тип пользователя (1-студент, 3-преподаватель)
        private ObservableCollection<BookItem> _availableBooks;      // Коллекция доступных книг
        private ObservableCollection<SelectedBookItem> _selectedBooks; // Коллекция выбранных книг
        private ObservableCollection<SchedulePeriodItem> _schedulePeriods; // Коллекция пар (расписание)
        private bool _isInitialized = false;  // Флаг инициализации страницы
        private bool _isLoading = false;      // Флаг загрузки данных

        #endregion

        #region Вложенные модели данных

        /// <summary>
        /// Модель книги для отображения в списке доступных книг
        /// </summary>
        public class BookItem
        {
            public int IDBook { get; set; }              // Уникальный идентификатор книги
            public string TitleBook { get; set; } = "";  // Название книги
            public string AuthorName { get; set; } = ""; // ФИО автора
            public int AvailableCopies { get; set; }     // Количество доступных экземпляров
        }

        /// <summary>
        /// Модель выбранной книги с количеством экземпляров
        /// </summary>
        public class SelectedBookItem
        {
            public int IDBook { get; set; }              // ID книги
            public string BookTitle { get; set; } = "";  // Название
            public string AuthorName { get; set; } = ""; // Автор
            public int Quantity { get; set; } = 1;       // Запрашиваемое количество
            public int MaxQuantity { get; set; }         // Максимально доступное количество
        }

        /// <summary>
        /// Модель учебной группы
        /// </summary>
        public class GroupItem
        {
            public int GroupID { get; set; }             // ID группы
            public string GroupCode { get; set; } = "";  // Код группы (например, ИС-141)
        }

        /// <summary>
        /// Модель пары (расписание звонков)
        /// </summary>
        public class SchedulePeriodItem
        {
            public int PeriodNumber { get; set; }        // Номер пары (1-8)
            public string StartTime { get; set; }        // Время начала
            public string EndTime { get; set; }          // Время окончания
            public string DisplayText => $"{PeriodNumber} пара: {StartTime} - {EndTime}";
        }

        #endregion

        #region Конструктор и инициализация

        /// <summary>
        /// Конструктор страницы оформления заявки
        /// </summary>
        /// <param name="readerId">ID пользователя</param>
        /// <param name="userTypeId">Тип пользователя</param>
        public RequestBook(int readerId, int userTypeId)
        {
            InitializeComponent();

            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;

            SetupUIForUserType();                       // Настраиваем интерфейс в зависимости от типа
            DpLessonDateSimple.SelectedDate = DateTime.Today; // Устанавливаем сегодняшнюю дату

            // Скрываем индикатор загрузки при старте
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Collapsed;

            // Подписываемся на событие загрузки страницы
            this.Loaded += async (s, e) => await RequestBook_LoadedAsync();
        }

        /// <summary>
        /// Асинхронный обработчик загрузки страницы
        /// </summary>
        private async Task RequestBook_LoadedAsync()
        {
            if (_isInitialized) return; // Предотвращаем повторную инициализацию
            _isInitialized = true;
            await InitializeAsync();     // Запускаем асинхронную инициализацию
        }

        /// <summary>
        /// Асинхронная инициализация страницы - загрузка всех данных
        /// </summary>
        private async Task InitializeAsync()
        {
            if (_isLoading) return;      // Предотвращаем повторную загрузку
            _isLoading = true;

            try
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(true)); // Показываем индикатор

                // Загружаем все данные параллельно для ускорения
                await Task.WhenAll(
                    Task.Run(() => LoadUserDataAsync()),      // Данные пользователя
                    Task.Run(() => LoadScheduleForDate(DateTime.Today)), // Расписание
                    Task.Run(() => LoadAvailableBooksAsync()) // Список книг
                );
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
                await Dispatcher.InvokeAsync(() => ShowLoading(false)); // Скрываем индикатор
                _isLoading = false;
            }
        }

        /// <summary>
        /// Показать/скрыть индикатор загрузки
        /// </summary>
        /// <param name="show">true - показать, false - скрыть</param>
        private void ShowLoading(bool show)
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (MainContentGrid != null)
                MainContentGrid.IsEnabled = !show; // Блокируем/разблокируем интерфейс
        }

        /// <summary>
        /// Настройка видимости UI элементов в зависимости от типа пользователя
        /// Для преподавателя показываем все поля, для студента - только выбор книг
        /// </summary>
        private void SetupUIForUserType()
        {
            if (_currentUserTypeId == 3) // Преподаватель
            {
                GroupPanel.Visibility = Visibility.Visible;
                TbLessonName.Visibility = Visibility.Visible;
                DpLessonDateSimple.Visibility = Visibility.Visible;
                CbLessonPeriod.Visibility = Visibility.Visible;
            }
            else // Читатель (студент)
            {
                GroupPanel.Visibility = Visibility.Collapsed;
                TbLessonName.Visibility = Visibility.Collapsed;
                DpLessonDateSimple.Visibility = Visibility.Collapsed;
                CbLessonPeriod.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Работа с расписанием звонков

        /// <summary>
        /// Загрузка расписания звонков для выбранной даты
        /// </summary>
        /// <param name="date">Дата занятия</param>
        private async Task LoadScheduleForDate(DateTime date)
        {
            try
            {
                int dayOfWeek = GetDayOfWeekNumber(date);           // Получаем номер дня недели
                bool isSummerSchedule = IsSummerSchedule(date);    // Определяем летнее/зимнее расписание

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

                // Фильтруем доступные пары по текущему времени для сегодняшней даты
                var availablePeriods = allPeriods;
                if (date.Date == DateTime.Today)
                {
                    availablePeriods = FilterAvailablePeriodsByCurrentTime(allPeriods);
                }

                // Обновляем выпадающий список в UI
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

        /// <summary>
        /// Фильтрация доступных пар по текущему времени
        /// </summary>
        /// <param name="allPeriods">Все пары</param>
        /// <returns>Список доступных пар</returns>
        private List<SchedulePeriodItem> FilterAvailablePeriodsByCurrentTime(List<SchedulePeriodItem> allPeriods)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            SchedulePeriodItem currentPeriod = null;      // Текущая пара (идет сейчас)
            SchedulePeriodItem nextAvailablePeriod = null; // Следующая пара (еще не началась)

            foreach (var period in allPeriods)
            {
                if (!TimeSpan.TryParse(period.StartTime, out TimeSpan start)) continue;
                if (!TimeSpan.TryParse(period.EndTime, out TimeSpan end)) continue;

                // Проверяем, идет ли эта пара сейчас
                if (currentTime >= start && currentTime <= end)
                {
                    currentPeriod = period;
                }

                // Ищем ближайшую будущую пару
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
                return availablePeriods; // Нет доступных пар

            // Добавляем все пары с минимального номера
            availablePeriods.AddRange(allPeriods.Where(p => p.PeriodNumber >= minAvailablePeriodNumber));
            return availablePeriods;
        }

        /// <summary>
        /// Преобразование дня недели в числовой формат (1=ПН, 2=ВТ, ..., 7=ВС)
        /// </summary>
        private int GetDayOfWeekNumber(DateTime date)
        {
            int day = (int)date.DayOfWeek;
            return day == 0 ? 7 : day; // Воскресенье = 0 преобразуем в 7
        }

        /// <summary>
        /// Определение летнего расписания (действует с 26 мая)
        /// </summary>
        private bool IsSummerSchedule(DateTime date)
        {
            return date >= new DateTime(date.Year, 5, 26);
        }

        /// <summary>
        /// Обработчик изменения даты занятия - загружаем расписание для новой даты
        /// </summary>
        private async void DpLessonDateSimple_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpLessonDateSimple.SelectedDate.HasValue)
            {
                DateTime selectedDate = DpLessonDateSimple.SelectedDate.Value.Date;

                // Проверяем, что дата не раньше сегодня
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

        #region Загрузка данных пользователя и групп

        /// <summary>
        /// Загрузка данных пользователя (тип)
        /// </summary>
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

                // Если преподаватель, загружаем список групп
                if (_currentUserTypeId == 3)
                {
                    await Dispatcher.InvokeAsync(() => GroupPanel.Visibility = Visibility.Visible);
                    await LoadGroupsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка списка учебных групп из БД
        /// </summary>
        private async Task LoadGroupsAsync()
        {
            try
            {
                const string query = "SELECT GroupID, GroupCode FROM Groups ORDER BY GroupCode";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var groups = new ObservableCollection<GroupItem>();

                        while (await reader.ReadAsync())
                        {
                            short groupId = reader.IsDBNull(0) ? (short)0 : reader.GetInt16(0);
                            string groupCode = reader.IsDBNull(1) ? "Неизвестно" : reader.GetString(1);

                            groups.Add(new GroupItem
                            {
                                GroupID = groupId,
                                GroupCode = groupCode
                            });
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            CbGroup.ItemsSource = groups;
                            CbGroup.DisplayMemberPath = "GroupCode";
                            CbGroup.SelectedValuePath = "GroupID";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки групп: {ex.Message}");
            }
        }

        #endregion

        #region Работа с книгами

        /// <summary>
        /// Загрузка доступных книг из БД
        /// </summary>
        /// <param name="searchTerm">Поисковый запрос (опционально)</param>
        private async Task LoadAvailableBooksAsync(string searchTerm = "")
        {
            try
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

                var booksList = new List<BookItem>();

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
                                booksList.Add(new BookItem
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

                // Обновляем UI
                await Dispatcher.InvokeAsync(() =>
                {
                    _availableBooks = new ObservableCollection<BookItem>(booksList);
                    DgAvailableBooks.ItemsSource = _availableBooks;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг: {ex.Message}");
            }
        }

        /// <summary>
        /// Добавление книги в заявку
        /// </summary>
        /// <param name="book">Книга для добавления</param>
        private void AddBookToRequest(BookItem book)
        {
            if (_selectedBooks == null)
                _selectedBooks = new ObservableCollection<SelectedBookItem>();

            // Проверка доступности
            if (book.AvailableCopies <= 0)
            {
                CustomMessageBox.Show($"Книга '{book.TitleBook}' временно недоступна.",
                    "Книга недоступна", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            SelectedBookItem existing = _selectedBooks.FirstOrDefault(b => b.IDBook == book.IDBook);

            if (existing != null)
            {
                // Проверка лимита для студента (только 1 экземпляр)
                if (_currentUserTypeId != 3 && existing.Quantity >= 1)
                {
                    CustomMessageBox.Show("Для индивидуальной выдачи можно взять только 1 экземпляр книги.",
                        "Ограничение", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return;
                }

                // Увеличиваем количество
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

        /// <summary>
        /// Обновление общего количества выбранных книг
        /// </summary>
        private void UpdateTotalCount()
        {
            int total = _selectedBooks?.Sum(b => b.Quantity) ?? 0;
            TbTotalBooks.Text = total.ToString();
        }

        #endregion

        #region Генерация номера заявки

        /// <summary>
        /// Генерация уникального номера заявки в формате ЗАЯВ-ГГГГММДД-XXXX
        /// </summary>
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
                // При ошибке генерируем случайный номер
                return $"ЗАЯВ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1, 9999):D4}";
            }
        }

        #endregion

        #region Обработчики событий UI

        /// <summary>
        /// Поиск книг при изменении текста в поле поиска
        /// </summary>
        private async void TbBookSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadAvailableBooksAsync(TbBookSearch.Text.Trim());
        }

        /// <summary>
        /// Кнопка поиска книг
        /// </summary>
        private async void BtnSearchBook_Click(object sender, RoutedEventArgs e)
        {
            await LoadAvailableBooksAsync(TbBookSearch.Text.Trim());
        }

        /// <summary>
        /// Кнопка очистки поиска
        /// </summary>
        private async void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TbBookSearch.Clear();
            await LoadAvailableBooksAsync();
        }

        /// <summary>
        /// Двойной клик по книге для добавления в заявку
        /// </summary>
        private void DgAvailableBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgAvailableBooks.SelectedItem is BookItem selectedBook)
                AddBookToRequest(selectedBook);
        }

        /// <summary>
        /// Удаление книги из заявки
        /// </summary>
        private void BtnRemoveBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SelectedBookItem selectedBook && _selectedBooks != null)
            {
                _selectedBooks.Remove(selectedBook);
                DgSelectedBooks.ItemsSource = _selectedBooks;
                UpdateTotalCount();
            }
        }

        /// <summary>
        /// Изменение количества экземпляров книги
        /// </summary>
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

        /// <summary>
        /// Кнопка отмены - возврат на предыдущую страницу
        /// </summary>
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

        /// <summary>
        /// Валидация формы перед отправкой
        /// </summary>
        private bool ValidateForm()
        {
            // Для преподавателя - проверяем все поля
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

                if (CbGroup.SelectedItem == null)
                {
                    CustomMessageBox.Show("Выберите группу.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }

                if (CbLessonPeriod.SelectedItem == null)
                {
                    CustomMessageBox.Show("Выберите пару / время занятия.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    return false;
                }
            }

            // Проверка наличия выбранных книг
            if (_selectedBooks == null || _selectedBooks.Count == 0)
            {
                CustomMessageBox.Show("Добавьте хотя бы одну книгу в заявку.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Для студента - проверяем, что не выбрано больше 1 экземпляра на книгу
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

        /// <summary>
        /// Отправка заявки (основной метод сохранения)
        /// </summary>
        private async void BtnSubmitRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            BtnSubmitRequest.IsEnabled = false;

            try
            {
                ShowLoading(true);

                int? groupId = CbGroup.SelectedValue != null ? Convert.ToInt32(CbGroup.SelectedValue) : (int?)null;
                DateTime lessonDate = DpLessonDateSimple.SelectedDate.Value.Date;

                string lessonTime = null;
                if (CbLessonPeriod.SelectedItem is SchedulePeriodItem selectedPeriod)
                {
                    lessonTime = selectedPeriod.StartTime;
                }

                string generatedNumber = GenerateUniqueRequestNumber();

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

                            int requestId;
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

                            // 4. Обновляем уведомления у библиотекарей
                            await ForceUpdateNotificationsForLibrarians();

                            await Dispatcher.InvokeAsync(() =>
                            {
                                CustomMessageBox.Show($"Заявка №{generatedNumber} успешно создана!\n\nПосле обработки библиотекарем вы получите уведомление.",
                                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            });

                            await ClearForm();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
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

        /// <summary>
        /// Принудительное обновление уведомлений у всех онлайн-библиотекарей
        /// </summary>
        private async Task ForceUpdateNotificationsForLibrarians()
        {
            try
            {
                var mainWindows = Application.Current.Windows.OfType<MainWindow>();

                foreach (var mainWindow in mainWindows)
                {
                    var rolesField = typeof(MainWindow).GetField("_userRoles",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var roles = rolesField?.GetValue(mainWindow) as List<string>;

                    if (roles != null && (roles.Contains("LIBRARIAN") || roles.Contains("ADMIN")))
                    {
                        var notificationServiceField = typeof(MainWindow).GetField("_notificationService",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var notificationService = notificationServiceField?.GetValue(mainWindow) as NotificationService;

                        if (notificationService != null)
                        {
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

        /// <summary>
        /// Очистка формы после успешного создания заявки
        /// </summary>
        private async Task ClearForm()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TbLessonName.Clear();
                DpLessonDateSimple.SelectedDate = DateTime.Today;
                CbLessonPeriod.SelectedIndex = -1;
                CbGroup.SelectedIndex = -1;
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
