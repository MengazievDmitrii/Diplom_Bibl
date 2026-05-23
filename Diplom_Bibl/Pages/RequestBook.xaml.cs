using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
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

        /// <summary>
        /// Строка подключения к базе данных
        /// </summary>
        private readonly string _connectionString = DatabaseHelper.ConnectionString;

        /// <summary>
        /// ID текущего читателя/преподавателя
        /// </summary>
        private readonly int _currentReaderId;

        /// <summary>
        /// Тип пользователя (1-читатель, 2-библиотекарь, 3-преподаватель)
        /// </summary>
        private readonly int _currentUserTypeId;

        /// <summary>
        /// Коллекция доступных книг для выбора
        /// </summary>
        private ObservableCollection<BookItem> _availableBooks;

        /// <summary>
        /// Коллекция выбранных книг для заявки
        /// </summary>
        private ObservableCollection<SelectedBookItem> _selectedBooks;

        /// <summary>
        /// Коллекция доступных пар по расписанию звонков
        /// </summary>
        private ObservableCollection<SchedulePeriodItem> _schedulePeriods;

        #endregion

        #region Вложенные модели данных

        /// <summary>
        /// Модель книги для отображения в списке доступных книг
        /// </summary>
        public class BookItem
        {
            public int IDBook { get; set; }          // Уникальный идентификатор книги
            public string TitleBook { get; set; } = "";   // Название книги
            public string AuthorName { get; set; } = "";  // ФИО автора
            public int AvailableCopies { get; set; }      // Количество доступных экземпляров
        }

        /// <summary>
        /// Модель выбранной книги с количеством экземпляров
        /// </summary>
        public class SelectedBookItem
        {
            public int IDBook { get; set; }          // ID книги
            public string BookTitle { get; set; } = "";   // Название
            public string AuthorName { get; set; } = "";  // Автор
            public int Quantity { get; set; } = 1;        // Запрашиваемое количество
            public int MaxQuantity { get; set; }          // Максимально доступное количество
        }

        /// <summary>
        /// Модель учебной группы
        /// </summary>
        public class GroupItem
        {
            public int GroupID { get; set; }         // ID группы
            public string GroupCode { get; set; } = "";   // Код группы (например, ИС-141)
        }

        /// <summary>
        /// Модель пары (расписание звонков)
        /// </summary>
        public class SchedulePeriodItem
        {
            public int PeriodNumber { get; set; }    // Номер пары (1-8)
            public string StartTime { get; set; }    // Время начала
            public string EndTime { get; set; }      // Время окончания
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

            // Сохраняем данные о пользователе
            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;

            // Устанавливаем дату занятия по умолчанию - сегодня
            DpLessonDateSimple.SelectedDate = DateTime.Today;

            // Загружаем данные пользователя
            LoadUserData();

            // Загружаем список доступных книг
            LoadAvailableBooks();

            // Подписываемся на событие загрузки страницы
            this.Loaded += RequestBook_Loaded;
        }

        /// <summary>
        /// Обработчик загрузки страницы
        /// </summary>
        private async void RequestBook_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем расписание звонков для сегодняшней даты
            await LoadScheduleForDate(DateTime.Today);
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
                // Получаем номер дня недели (1-7)
                int dayOfWeek = GetDayOfWeekNumber(date);

                // Определяем летнее или зимнее расписание
                bool isSummerSchedule = IsSummerSchedule(date);

                // SQL-запрос для получения расписания
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

                // Выполняем запрос к базе данных
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

                // Для сегодняшней даты фильтруем доступные пары по текущему времени
                var availablePeriods = allPeriods;
                if (date.Date == DateTime.Today)
                {
                    availablePeriods = FilterAvailablePeriodsByCurrentTime(allPeriods);
                }

                // Обновляем выпадающий список пар
                _schedulePeriods = new ObservableCollection<SchedulePeriodItem>(availablePeriods);
                CbLessonPeriod.ItemsSource = _schedulePeriods;

                // Выбираем первую доступную пару по умолчанию
                if (_schedulePeriods.Any())
                {
                    CbLessonPeriod.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки расписания: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Фильтрация доступных пар по текущему времени
        /// </summary>
        private List<SchedulePeriodItem> FilterAvailablePeriodsByCurrentTime(List<SchedulePeriodItem> allPeriods)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            SchedulePeriodItem currentPeriod = null;      // Текущая пара (идет сейчас)
            SchedulePeriodItem nextAvailablePeriod = null; // Следующая пара (еще не началась)

            // Анализируем все пары
            foreach (var period in allPeriods)
            {
                // Парсим время начала и окончания
                if (!TimeSpan.TryParse(period.StartTime, out TimeSpan start))
                    continue;
                if (!TimeSpan.TryParse(period.EndTime, out TimeSpan end))
                    continue;

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

            // Определяем минимальный номер доступной пары
            if (currentPeriod != null)
            {
                // Если есть текущая пара - доступны с нее
                minAvailablePeriodNumber = currentPeriod.PeriodNumber;
            }
            else if (nextAvailablePeriod != null)
            {
                // Если нет текущей, но есть будущие - доступны с ближайшей будущей
                minAvailablePeriodNumber = nextAvailablePeriod.PeriodNumber;
            }
            else
            {
                // Нет доступных пар
                return availablePeriods;
            }

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
            // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
            return day == 0 ? 7 : day;
        }

        /// <summary>
        /// Определение летнего расписания (действует с 26 мая)
        /// </summary>
        private bool IsSummerSchedule(DateTime date)
        {
            return date >= new DateTime(date.Year, 5, 26);
        }

        /// <summary>
        /// Обработчик изменения даты занятия
        /// </summary>
        private async void DpLessonDateSimple_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpLessonDateSimple.SelectedDate.HasValue)
            {
                DateTime selectedDate = DpLessonDateSimple.SelectedDate.Value.Date;

                // Проверка - дата не может быть раньше сегодня
                if (selectedDate < DateTime.Today)
                {
                    CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    DpLessonDateSimple.SelectedDate = DateTime.Today;
                    return;
                }

                // Загружаем расписание для выбранной даты
                await LoadScheduleForDate(selectedDate);
            }
        }

        #endregion

        #region Загрузка данных пользователя и групп

        /// <summary>
        /// Загрузка данных пользователя и определение видимости панели группы
        /// </summary>
        private void LoadUserData()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string typeQuery = "SELECT TypeName FROM ReaderType WHERE TypeID = @TypeID";
                    using (var cmd = new SqlCommand(typeQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@TypeID", _currentUserTypeId);
                        var result = cmd.ExecuteScalar();
                        TbUserType.Text = result?.ToString() ?? "";
                    }
                }

                // Для преподавателей (TypeID = 3) показываем выбор группы
                if (_currentUserTypeId == 3)
                {
                    GroupPanel.Visibility = Visibility.Visible;
                    LoadGroups();
                }
                else
                {
                    GroupPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadGroups()
        {
            try
            {
                const string query = "SELECT GroupID, GroupCode FROM Groups ORDER BY GroupCode";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var groups = new ObservableCollection<GroupItem>();

                        // Отладочный вывод - посмотрим сколько колонок
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            System.Diagnostics.Debug.WriteLine($"Колонка {i}: {reader.GetName(i)} - Тип: {reader.GetFieldType(i)}");
                        }

                        while (reader.Read())
                        {
                            // Пробуем разные способы чтения
                            object groupIdObj = reader[0];  // Первая колонка
                            object groupCodeObj = reader[1]; // Вторая колонка

                            System.Diagnostics.Debug.WriteLine($"GroupID: {groupIdObj} (тип: {groupIdObj?.GetType()})");
                            System.Diagnostics.Debug.WriteLine($"GroupCode: {groupCodeObj} (тип: {groupCodeObj?.GetType()})");

                            int groupId = Convert.ToInt32(groupIdObj);
                            string groupCode = groupCodeObj?.ToString() ?? "";

                            groups.Add(new GroupItem
                            {
                                GroupID = groupId,
                                GroupCode = groupCode
                            });
                        }
                        CbGroup.ItemsSource = groups;
                        CbGroup.DisplayMemberPath = "GroupCode";
                        CbGroup.SelectedValuePath = "GroupID";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки групп: {ex.Message}\n{ex.StackTrace}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        #endregion

        #region Работа с книгами

        /// <summary>
        /// Загрузка доступных книг (сгруппированных по названию и автору)
        /// </summary>
        /// <param name="searchTerm">Поисковый запрос</param>
        private void LoadAvailableBooks(string searchTerm = "")
        {
            try
            {
                string query = @"
            SELECT 
                MIN(b.IDBook) AS IDBook,
                b.TitleBook, 
                ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                SUM(dbo.fn_GetActuallyAvailableForRequest(b.IDBook)) AS AvailableCopies
            FROM Books b
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            WHERE dbo.fn_GetActuallyAvailableForRequest(b.IDBook) > 0";

                // Условие поиска добавляется ДО GROUP BY
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query += " AND (b.TitleBook LIKE @Search OR a.SurnameAuthor LIKE @Search)";
                }

                query += @" GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
                    ORDER BY b.TitleBook";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                            cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            _availableBooks = new ObservableCollection<BookItem>();
                            while (reader.Read())
                            {
                                int available = reader.GetInt32(3);
                                if (available > 0)
                                {
                                    _availableBooks.Add(new BookItem
                                    {
                                        IDBook = reader.GetInt32(0),
                                        TitleBook = reader.GetString(1),
                                        AuthorName = reader.GetString(2),
                                        AvailableCopies = available
                                    });
                                }
                            }
                        }
                    }
                }

                DgAvailableBooks.ItemsSource = _availableBooks;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Добавление книги в заявку
        /// </summary>
        private void AddBookToRequest(BookItem book)
        {
            if (_selectedBooks == null)
                _selectedBooks = new ObservableCollection<SelectedBookItem>();

            int actualAvailable = book.AvailableCopies;

            if (actualAvailable <= 0)
            {
                CustomMessageBox.Show($"Книга '{book.TitleBook}' временно недоступна.",
                    "Книга недоступна", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверяем, не добавлена ли уже эта книга
            SelectedBookItem existing = _selectedBooks.FirstOrDefault(b => b.BookTitle == book.TitleBook && b.AuthorName == book.AuthorName);

            if (existing != null)
            {
                // Увеличиваем количество, если не превышает доступное
                if (existing.Quantity + 1 <= actualAvailable)
                    existing.Quantity++;
                else
                    CustomMessageBox.Show($"Доступно только {actualAvailable} экземпляров этой книги.", "Предупреждение",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
            else
            {
                // Добавляем новую книгу
                _selectedBooks.Add(new SelectedBookItem
                {
                    IDBook = book.IDBook,
                    BookTitle = book.TitleBook,
                    AuthorName = book.AuthorName,
                    Quantity = 1,
                    MaxQuantity = actualAvailable
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
                // В случае ошибки генерируем случайный номер
                return $"ЗАЯВ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1, 9999):D4}";
            }
        }

        #endregion

        #region Обработчики событий UI

        /// <summary>
        /// Поиск книг при изменении текста
        /// </summary>
        private void TbBookSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadAvailableBooks(TbBookSearch.Text.Trim());
        }

        /// <summary>
        /// Кнопка поиска книг
        /// </summary>
        private void BtnSearchBook_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableBooks(TbBookSearch.Text.Trim());
        }

        /// <summary>
        /// Кнопка очистки поиска
        /// </summary>
        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TbBookSearch.Clear();
            LoadAvailableBooks();
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
            // Проверка названия занятия
            if (string.IsNullOrWhiteSpace(TbLessonName.Text))
            {
                CustomMessageBox.Show("Введите название занятия/предмета.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Проверка даты занятия
            if (!DpLessonDateSimple.SelectedDate.HasValue)
            {
                CustomMessageBox.Show("Выберите дату занятия.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Дату нельзя выбрать раньше сегодня
            if (DpLessonDateSimple.SelectedDate.Value.Date < DateTime.Today)
            {
                CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Для преподавателя обязателен выбор группы
            if (_currentUserTypeId == 3 && CbGroup.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите группу.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Проверка выбора пары
            if (CbLessonPeriod.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите пару / время занятия.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            // Проверка наличия выбранных книг
            if (_selectedBooks == null || _selectedBooks.Count == 0)
            {
                CustomMessageBox.Show("Добавьте хотя бы одну книгу в заявку.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Отправка заявки (основной метод сохранения)
        /// </summary>
        private async void BtnSubmitRequest_Click(object sender, RoutedEventArgs e)
        {
            // Валидация формы
            if (!ValidateForm()) return;

            // Блокируем кнопку, чтобы избежать повторной отправки
            BtnSubmitRequest.IsEnabled = false;

            try
            {
                // Получаем ID группы (только для преподавателей)
                int? groupId = CbGroup.SelectedValue != null ? Convert.ToInt32(CbGroup.SelectedValue) : (int?)null;

                // Дата занятия (без времени)
                DateTime lessonDate = DpLessonDateSimple.SelectedDate.Value.Date;

                // Время занятия (выбранная пара)
                string lessonTime = null;
                if (CbLessonPeriod.SelectedItem is SchedulePeriodItem selectedPeriod)
                {
                    lessonTime = selectedPeriod.StartTime;
                }

                // Генерация уникального номера заявки
                string generatedNumber = GenerateUniqueRequestNumber();

                // Подключение к базе данных
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Начинаем транзакцию для согласованности данных
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
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
                                // Временная отладка — скопируй этот текст и выполни в SSMS
                                System.Diagnostics.Debug.WriteLine("=== SQL DEBUG ===");
                                System.Diagnostics.Debug.WriteLine(cmd.CommandText);
                                foreach (SqlParameter p in cmd.Parameters)
                                {
                                    System.Diagnostics.Debug.WriteLine($"{p.ParameterName} = {p.Value} (Type: {p.SqlDbType})");
                                }
                                System.Diagnostics.Debug.WriteLine("=== END SQL DEBUG ===");
                                requestId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            // 2. Вставка книг в заявку
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

                            // 3. Создание уведомления для библиотекаря
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

                            // Фиксируем транзакцию
                            transaction.Commit();

                            // Сообщаем об успехе
                            CustomMessageBox.Show($"Заявка №{generatedNumber} успешно создана!\n\nПосле обработки библиотекарем вы получите уведомление.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            // Очищаем форму
                            ClearForm();
                        }
                        catch
                        {
                            // В случае ошибки откатываем изменения
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании заявки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                // Разблокируем кнопку
                BtnSubmitRequest.IsEnabled = true;
            }
        }

        /// <summary>
        /// Очистка формы после успешного создания заявки
        /// </summary>
        private void ClearForm()
        {
            TbLessonName.Clear();
            DpLessonDateSimple.SelectedDate = DateTime.Today;
            CbLessonPeriod.SelectedIndex = -1;
            CbGroup.SelectedIndex = -1;
            if (_selectedBooks != null)
                _selectedBooks.Clear();
            DgSelectedBooks.ItemsSource = null;
            UpdateTotalCount();
            LoadAvailableBooks();
        }

        #endregion
    }
}