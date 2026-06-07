using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
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
    /// <summary>
    /// Страница управления библиотечным фондом
    /// Разработано для дипломного проекта
    /// </summary>
    public partial class LibraryFundPage : Page
    {
        #region Поля класса

        private readonly string _conn = DatabaseHelper.ConnectionString;

        private readonly ObservableCollection<BookGroupItem> _allBooks;
        private ObservableCollection<BookGroupItem> _filteredBooks;
        private readonly List<StatusItem> _allStatuses;
        private readonly List<GenreItem> _allGenres;
        private readonly List<PublisherItem> _allPublishers;
        private readonly List<AuthorFilterItem> _allAuthors;
        private readonly ObservableCollection<BookSearchItem> _allBooksForSearch;
        private readonly ObservableCollection<BookSearchItem> _searchResults;

        private bool _isSearchPlaceholderActive = true;
        private bool _isSearchProgrammatic = false;
        private bool _isLoading = false;
        private bool _isDataLoaded = false; 



        #endregion

        #region Модели данных

        public class BookSearchItem
        {
            public int IDBook { get; set; }
            public string TitleBook { get; set; } = "";
            public string AuthorName { get; set; } = "";
        }

        public class AuthorFilterItem
        {
            public int ID_Author { get; set; }
            public string DisplayAuthors { get; set; } = "";
        }

        public class StatusItem
        {
            public int ID_Status { get; set; }
            public string Title_Status { get; set; } = "";
            public string DisplayName => Title_Status;
        }

        public class GenreItem
        {
            public int ID_Gener { get; set; }
            public string TitleGener { get; set; } = "";
            public string DisplayName => TitleGener;
        }

        public class PublisherItem
        {
            public int ID_Publisher { get; set; }
            public string TitlePublishingHouse { get; set; } = "";
            public string DisplayName => TitlePublishingHouse;
        }

        public class BookGroupItem
        {
            public int GroupId { get; set; }
            public string TitleBook { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int? AuthorID { get; set; }
            public int YearPublishing { get; set; }
            public string PublishingHouseName { get; set; } = "";
            public int? PublisherID { get; set; }
            public string GenreName { get; set; } = "";
            public int? GenreID { get; set; }
            public int TotalCopies { get; set; }
            public int AvailableCopies { get; set; }
            public int IssuedCopies { get; set; }
            public int DisposedCopies { get; set; }
            public string StatusName { get; set; } = "";
            public int? StatusID { get; set; }
            public List<int> BookIds { get; set; } = new List<int>();

            private SolidColorBrush _statusColor;
            private string _statusText;
            public int GetStatusIdForFilter()
            {
                if (AvailableCopies > 0) return 1;      // В наличии
                if (IssuedCopies > 0) return 3;         // Выдана
                if (DisposedCopies > 0) return 5;       // Списана
                if (StatusID.HasValue && StatusID.Value == 4) return 4;  // Утрачена
                if (StatusID.HasValue && StatusID.Value == 6) return 6;  // Зарезервирована
                return 1;
            }

            // Используем полное имя для Color, чтобы избежать конфликта
            public SolidColorBrush StatusColor
            {
                get
                {
                    if (_statusColor != null) return _statusColor;
                    int priorityStatusId = GetPriorityStatusId();

                    if (priorityStatusId == 1)
                        _statusColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745"));
                    else if (priorityStatusId == 3)
                        _statusColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC107"));
                    else if (priorityStatusId == 4 || priorityStatusId == 5)
                        _statusColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545"));
                    else if (priorityStatusId == 6)
                        _statusColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007BFF"));
                    else
                        _statusColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6C757D"));

                    return _statusColor;
                }
            }

            public string StatusText
            {
                get
                {
                    if (_statusText != null) return _statusText;
                    int priorityStatusId = GetPriorityStatusId();

                    if (priorityStatusId == 1) _statusText = "В наличии";
                    else if (priorityStatusId == 3) _statusText = "Выдана";
                    else if (priorityStatusId == 4) _statusText = "Утрачена";
                    else if (priorityStatusId == 5) _statusText = "Списана";
                    else if (priorityStatusId == 6) _statusText = "Зарезервирована";
                    else _statusText = "Неизвестно";

                    return _statusText;
                }
            }

            private int GetPriorityStatusId()
            {
                // Приоритет: В наличии > Выдана > Зарезервирована > Утрачена > Списана

                // Если есть хоть один доступный экземпляр — статус "В наличии"
                if (AvailableCopies > 0)
                    return 1;

                // Если есть выданные экземпляры (и нет доступных)
                if (IssuedCopies > 0)
                    return 3;

                // Если есть зарезервированные
                if (StatusID.HasValue && StatusID.Value == 6)
                    return 6;

                // Если есть утраченные
                if (StatusID.HasValue && StatusID.Value == 4)
                    return 4;

                // Если есть списанные (и нет других статусов)
                if (DisposedCopies > 0)
                    return 5;

                return 1;
            }
        }

        #endregion

        #region Конструктор

        public LibraryFundPage()
        {
            InitializeComponent();

            _allBooks = new ObservableCollection<BookGroupItem>();
            _filteredBooks = new ObservableCollection<BookGroupItem>();
            _allStatuses = new List<StatusItem>();
            _allGenres = new List<GenreItem>();
            _allPublishers = new List<PublisherItem>();
            _allAuthors = new List<AuthorFilterItem>();
            _allBooksForSearch = new ObservableCollection<BookSearchItem>();
            _searchResults = new ObservableCollection<BookSearchItem>();

            SetSearchPlaceholder();
            AddBook.Visibility = Visibility.Collapsed;

            // Скрываем локальный индикатор загрузки при старте
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Collapsed;

           

            this.Loaded += async (s, e) => await LibraryFundPage_LoadedAsync();
        }

        #endregion

        #region Загрузка данных

        private async Task LibraryFundPage_LoadedAsync()
        {
            if (_isDataLoaded) return; // Не грузим дважды
            await LoadDataAsync();
        }

        #endregion

        #region Загрузка данных

       
        private async Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            await Dispatcher.InvokeAsync(() =>
            {
                if (LoadingOverlay != null)
                    LoadingOverlay.Visibility = Visibility.Visible;
               
            });

            try
            {
                // Загружаем всё параллельно
                await Task.WhenAll(
                    Task.Run(() => LoadBooks()),
                    Task.Run(() => LoadAuthorsForFilter()),
                    Task.Run(() => LoadStatuses()),
                    Task.Run(() => LoadGenres()),
                    Task.Run(() => LoadPublishers()),
                    Task.Run(() => LoadBooksForSearch())
                );

                await Dispatcher.InvokeAsync(() =>
                {
                    ApplyAllFilters();
                    UpdateStatistics();
                    _isDataLoaded = true;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
            finally
            {
                // Скрываем ТОЛЬКО локальный индикатор загрузки
                await Dispatcher.InvokeAsync(() =>
                {
                    if (LoadingOverlay != null)
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    
                });
                _isLoading = false;
            }
        }
        private void ShowLoading(bool show)
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        }


        private async Task LoadBooks()
        {
            try
            {
                string sql = @"
            WITH IssuanceStats AS (
                SELECT 
                    b.TitleBook,
                    COUNT(*) AS IssuedCount
                FROM BookIssuance bi
                INNER JOIN Books b ON bi.BookId = b.IDBook
                WHERE bi.ReturnDate IS NULL AND bi.Status = 'Выдана'
                GROUP BY b.TitleBook
                
                UNION ALL
                
                SELECT 
                    b.TitleBook,
                    SUM(gii.Quantity - gii.ReturnedQuantity) AS IssuedCount
                FROM GroupBookIssuance gbi
                INNER JOIN GroupIssuanceItems gii ON gbi.GroupIssuanceId = gii.GroupIssuanceId
                INNER JOIN Books b ON gii.BookId = b.IDBook
                WHERE gbi.ReturnDate IS NULL AND gbi.Status = 'Выдана'
                GROUP BY b.TitleBook
            ),
            TotalIssued AS (
                SELECT 
                    TitleBook,
                    SUM(IssuedCount) AS TotalIssued
                FROM IssuanceStats
                GROUP BY TitleBook
            ),
            DisposedStats AS (
                SELECT 
                    TitleBook,
                    COUNT(*) AS DisposedCount
                FROM Books
                WHERE StatusBook = 5
                GROUP BY TitleBook
            )
            SELECT 
                MIN(b.IDBook) AS GroupId,
                b.TitleBook,
                ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + ISNULL(a.PatronymicAuthor, ''), '') AS AuthorName,
                MIN(b.Author) AS AuthorID,
                MIN(b.YearPublishing) AS YearPublishing,
                MIN(ISNULL(ph.TitlePublishingHouse, '')) AS PublishingHouseName,
                MIN(b.PublishingHouse) AS PublisherID,
                MIN(ISNULL(g.TitleGener, '')) AS GenreName,
                MIN(b.Gener) AS GenreID,
                COUNT(*) AS TotalCopies,
                COUNT(*) - ISNULL(MAX(ti.TotalIssued), 0) - ISNULL(MAX(ds.DisposedCount), 0) AS AvailableCopies,
                ISNULL(MAX(ti.TotalIssued), 0) AS IssuedCopies,
                ISNULL(MAX(ds.DisposedCount), 0) AS DisposedCopies,
                CASE 
                    WHEN ISNULL(MAX(ds.DisposedCount), 0) > 0 THEN 'Списана'
                    WHEN COUNT(*) - ISNULL(MAX(ti.TotalIssued), 0) - ISNULL(MAX(ds.DisposedCount), 0) > 0 THEN 'В наличии'
                    WHEN ISNULL(MAX(ti.TotalIssued), 0) > 0 THEN 'Выдана'
                    ELSE 'Неизвестно'
                END AS StatusName,
                NULL AS StatusID
            FROM Books b
            LEFT JOIN Authors a ON b.Author = a.ID_Autor
            LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
            LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
            LEFT JOIN TotalIssued ti ON b.TitleBook = ti.TitleBook
            LEFT JOIN DisposedStats ds ON b.TitleBook = ds.TitleBook
            GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
            ORDER BY b.TitleBook";

                var yearsSet = new HashSet<int>();
                var booksList = new List<BookGroupItem>(); // Временный список

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync()) // ДОБАВЛЕН await
                    {
                        while (await reader.ReadAsync())
                        {
                            var book = new BookGroupItem
                            {
                                GroupId = Convert.ToInt32(reader["GroupId"]),
                                TitleBook = reader["TitleBook"]?.ToString() ?? "",
                                AuthorName = reader["AuthorName"]?.ToString() ?? "",
                                AuthorID = reader["AuthorID"] != DBNull.Value ? Convert.ToInt32(reader["AuthorID"]) : (int?)null,
                                YearPublishing = Convert.ToInt32(reader["YearPublishing"]),
                                PublishingHouseName = reader["PublishingHouseName"]?.ToString() ?? "",
                                PublisherID = reader["PublisherID"] != DBNull.Value ? Convert.ToInt32(reader["PublisherID"]) : (int?)null,
                                GenreName = reader["GenreName"]?.ToString() ?? "",
                                GenreID = reader["GenreID"] != DBNull.Value ? Convert.ToInt32(reader["GenreID"]) : (int?)null,
                                TotalCopies = Convert.ToInt32(reader["TotalCopies"]),
                                AvailableCopies = Convert.ToInt32(reader["AvailableCopies"]),
                                IssuedCopies = Convert.ToInt32(reader["IssuedCopies"]),
                                DisposedCopies = Convert.ToInt32(reader["DisposedCopies"]),
                                StatusName = reader["StatusName"]?.ToString() ?? "Неизвестно",
                                StatusID = reader["StatusID"] != DBNull.Value ? Convert.ToInt32(reader["StatusID"]) : (int?)null
                            };
                            booksList.Add(book);
                            if (book.YearPublishing > 0) yearsSet.Add(book.YearPublishing);
                        }
                    }
                }

                // Обновляем UI в потоке диспетчера
                await Dispatcher.InvokeAsync(() =>
                {
                    _allBooks.Clear();
                    foreach (var book in booksList) _allBooks.Add(book);
                    LoadYearFilters(yearsSet.ToList());
                    ApplyAllFilters();
                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }
        private async Task LoadBooksForSearch()
        {
            try
            {
                string sql = @"
                    SELECT DISTINCT
                        MIN(b.IDBook) AS IDBook,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE b.StatusBook != 5
                    GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
                    ORDER BY b.TitleBook";

                _allBooksForSearch.Clear();

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allBooksForSearch.Add(new BookSearchItem
                            {
                                IDBook = Convert.ToInt32(reader["IDBook"]),
                                TitleBook = reader["TitleBook"]?.ToString() ?? "",
                                AuthorName = reader["AuthorName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки списка книг: {ex.Message}");
            }
        }

        private async Task LoadAuthorsForFilter()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";
                var authorsList = new List<AuthorFilterItem>();
                authorsList.Add(new AuthorFilterItem { ID_Author = 0, DisplayAuthors = "Все авторы" });

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = Convert.ToInt32(reader["ID_Autor"]);
                            string surname = reader["SurnameAuthor"]?.ToString() ?? "";
                            string name = reader["NameAuthor"]?.ToString() ?? "";
                            string patronymic = reader["PatronymicAuthor"] != DBNull.Value ? reader["PatronymicAuthor"].ToString() : "";

                            authorsList.Add(new AuthorFilterItem
                            {
                                ID_Author = id,
                                DisplayAuthors = $"{surname} {name} {patronymic}".Trim()
                            });
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _allAuthors.Clear();
                    foreach (var author in authorsList)
                        _allAuthors.Add(author);

                    if (FilterAuthorCombo != null)
                    {
                        FilterAuthorCombo.ItemsSource = null;
                        FilterAuthorCombo.ItemsSource = _allAuthors;
                        FilterAuthorCombo.SelectedValuePath = "ID_Author";
                        FilterAuthorCombo.DisplayMemberPath = "DisplayAuthors";
                        FilterAuthorCombo.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private async Task LoadStatuses()
        {
            try
            {
                const string sql = "SELECT ID_Status, LTRIM(RTRIM(Title_Status)) AS Title_Status FROM StatusBook ORDER BY Title_Status";
                var statusesList = new List<StatusItem>();
                statusesList.Add(new StatusItem { ID_Status = 0, Title_Status = "Все статусы" });

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            statusesList.Add(new StatusItem
                            {
                                ID_Status = Convert.ToInt32(reader["ID_Status"]),
                                Title_Status = reader["Title_Status"]?.ToString()?.Trim() ?? ""
                            });
                        }
                    }
                }

                // ВАЖНО: Обновляем UI через Dispatcher
                await Dispatcher.InvokeAsync(() =>
                {
                    _allStatuses.Clear();
                    foreach (var status in statusesList)
                        _allStatuses.Add(status);

                    if (FilterStatusCombo != null)
                    {
                        FilterStatusCombo.ItemsSource = null; // Сначала сбрасываем
                        FilterStatusCombo.ItemsSource = _allStatuses;
                        FilterStatusCombo.DisplayMemberPath = "DisplayName";
                        FilterStatusCombo.SelectedValuePath = "ID_Status";
                        FilterStatusCombo.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки статусов: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private async Task LoadGenres()
        {
            try
            {
                const string sql = "SELECT ID_Gener, TitleGener FROM GenersBook ORDER BY TitleGener";
                var genresList = new List<GenreItem>();
                genresList.Add(new GenreItem { ID_Gener = 0, TitleGener = "Все жанры" });

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            genresList.Add(new GenreItem
                            {
                                ID_Gener = Convert.ToInt32(reader["ID_Gener"]),
                                TitleGener = reader["TitleGener"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _allGenres.Clear();
                    foreach (var genre in genresList)
                        _allGenres.Add(genre);

                    if (FilterGenreCombo != null)
                    {
                        FilterGenreCombo.ItemsSource = null;
                        FilterGenreCombo.ItemsSource = _allGenres;
                        FilterGenreCombo.DisplayMemberPath = "DisplayName";
                        FilterGenreCombo.SelectedValuePath = "ID_Gener";
                        FilterGenreCombo.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private async Task LoadPublishers()
        {
            try
            {
                const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";
                var publishersList = new List<PublisherItem>();
                publishersList.Add(new PublisherItem { ID_Publisher = 0, TitlePublishingHouse = "Все издательства" });

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            publishersList.Add(new PublisherItem
                            {
                                ID_Publisher = Convert.ToInt32(reader["IDPublishingHouse"]),
                                TitlePublishingHouse = reader["TitlePublishingHouse"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _allPublishers.Clear();
                    foreach (var publisher in publishersList)
                        _allPublishers.Add(publisher);

                    if (FilterPublisherCombo != null)
                    {
                        FilterPublisherCombo.ItemsSource = null;
                        FilterPublisherCombo.ItemsSource = _allPublishers;
                        FilterPublisherCombo.DisplayMemberPath = "DisplayName";
                        FilterPublisherCombo.SelectedValuePath = "ID_Publisher";
                        FilterPublisherCombo.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
        }

        private void LoadYearFilters(List<int> years)
        {
            if (FilterYearCombo == null) return;

            FilterYearCombo.Items.Clear();
            FilterYearCombo.Items.Add(new ComboBoxItem { Content = "Все годы", IsSelected = true });

            foreach (var year in years.OrderByDescending(y => y))
            {
                FilterYearCombo.Items.Add(new ComboBoxItem { Content = year.ToString() });
            }

            FilterYearCombo.SelectedIndex = 0;
        }

        #endregion

        #region Поиск и фильтрация

        private void SetSearchPlaceholder()
        {
            _isSearchPlaceholderActive = true;
            if (BookSearchBox != null)
            {
                BookSearchBox.Text = "Введите название книги или автора...";
                BookSearchBox.Foreground = Brushes.Gray;
            }
        }

        private void ClearSearchPlaceholder()
        {
            if (_isSearchPlaceholderActive)
            {
                _isSearchPlaceholderActive = false;
                if (BookSearchBox != null)
                {
                    BookSearchBox.Text = "";
                    BookSearchBox.Foreground = Brushes.Black;
                }
            }
        }

        private void BookSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearSearchPlaceholder();
            if (ClearSearchBtn != null)
                ClearSearchBtn.Visibility = Visibility.Visible;
        }

        private void BookSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BookSearchBox?.Text))
            {
                SetSearchPlaceholder();
                if (SearchResultsList != null)
                    SearchResultsList.Visibility = Visibility.Collapsed;
                if (ClearSearchBtn != null)
                    ClearSearchBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void BookSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSearchProgrammatic) return;

            string searchText = BookSearchBox?.Text?.Trim();

            if (string.IsNullOrEmpty(searchText) || _isSearchPlaceholderActive)
            {
                if (_searchResults != null)
                    _searchResults.Clear();

                if (SearchResultsList != null)
                    SearchResultsList.Visibility = Visibility.Collapsed;

                ApplyAllFilters();
                return;
            }

            await PerformSearch(searchText);
        }

        private async Task PerformSearch(string searchText)
        {
            try
            {
                if (_searchResults != null)
                    _searchResults.Clear();

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT DISTINCT TOP 20 
                            MIN(b.IDBook) AS IDBook,
                            b.TitleBook,
                            ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName
                        FROM Books b
                        LEFT JOIN Authors a ON b.Author = a.ID_Autor
                        WHERE b.TitleBook LIKE @SearchText 
                           OR a.SurnameAuthor LIKE @SearchText
                           OR a.NameAuthor LIKE @SearchText
                        GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
                        ORDER BY b.TitleBook";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SearchText", $"%{searchText}%");
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                _searchResults?.Add(new BookSearchItem
                                {
                                    IDBook = Convert.ToInt32(reader["IDBook"]),
                                    TitleBook = reader["TitleBook"]?.ToString() ?? "",
                                    AuthorName = reader["AuthorName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }

                if (SearchResultsList != null)
                {
                    SearchResultsList.ItemsSource = _searchResults;
                    SearchResultsList.Visibility = (_searchResults != null && _searchResults.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска: {ex.Message}");
            }
        }

        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList?.SelectedItem is BookSearchItem selected)
            {
                _isSearchProgrammatic = true;
                BookSearchBox.Text = selected.TitleBook;
                if (SearchResultsList != null)
                    SearchResultsList.Visibility = Visibility.Collapsed;
                _isSearchProgrammatic = false;

                ApplyAllFilters();
                ScrollToBook(selected.TitleBook);
            }
        }

        private void ScrollToBook(string bookTitle)
        {
            try
            {
                if (DgBooks == null || _filteredBooks == null) return;

                var targetBook = _filteredBooks.FirstOrDefault(b => b.TitleBook == bookTitle);
                if (targetBook != null)
                {
                    DgBooks.ScrollIntoView(targetBook);
                    DgBooks.SelectedItem = targetBook;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка прокрутки: {ex.Message}");
            }
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            _isSearchProgrammatic = true;
            SetSearchPlaceholder();

            if (_searchResults != null)
                _searchResults.Clear();

            if (SearchResultsList != null)
                SearchResultsList.Visibility = Visibility.Collapsed;

            if (ClearSearchBtn != null)
                ClearSearchBtn.Visibility = Visibility.Collapsed;

            _isSearchProgrammatic = false;

            ApplyAllFilters();
        }

        private void ApplyAllFilters()
        {
            if (_allBooks == null || _allBooks.Count == 0) return;

            string searchTerm = (_isSearchPlaceholderActive || string.IsNullOrWhiteSpace(BookSearchBox?.Text))
                ? ""
                : BookSearchBox.Text.Trim().ToLower();

            int selectedAuthorId = GetSelectedId(FilterAuthorCombo);
            int selectedStatusId = GetSelectedId(FilterStatusCombo);
            int selectedGenreId = GetSelectedId(FilterGenreCombo);
            int selectedPublisherId = GetSelectedId(FilterPublisherCombo);

            string yearFilter = "Все годы";
            if (FilterYearCombo?.SelectedItem is ComboBoxItem yearItem)
                yearFilter = yearItem.Content?.ToString() ?? "Все годы";

            var filtered = _allBooks.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(b =>
                    (b.TitleBook?.ToLower().Contains(searchTerm) ?? false) ||
                    (b.AuthorName?.ToLower().Contains(searchTerm) ?? false));
            }

            if (selectedAuthorId > 0)
                filtered = filtered.Where(b => b.AuthorID == selectedAuthorId);

            if (selectedStatusId > 0)
            {
                filtered = filtered.Where(b => b.GetStatusIdForFilter() == selectedStatusId);
            }

            if (selectedGenreId > 0)
                filtered = filtered.Where(b => b.GenreID == selectedGenreId);

            if (selectedPublisherId > 0)
                filtered = filtered.Where(b => b.PublisherID == selectedPublisherId);

            if (yearFilter != "Все годы" && int.TryParse(yearFilter, out int year))
                filtered = filtered.Where(b => b.YearPublishing == year);

            var resultList = filtered.ToList();

            _filteredBooks.Clear();
            foreach (var item in resultList)
                _filteredBooks.Add(item);

            if (DgBooks != null)
                DgBooks.ItemsSource = _filteredBooks;

            if (SearchCountLabel != null)
                SearchCountLabel.Text = $"Найдено: {resultList.Count} из {_allBooks.Count} наименований";
        }

        private int GetSelectedId(ComboBox comboBox)
        {
            if (comboBox != null && comboBox.SelectedValue != null && int.TryParse(comboBox.SelectedValue.ToString(), out int id))
                return id;
            return 0;
        }

        #endregion

        #region Обработчики событий UI

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyAllFilters();
        }

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FilterAuthorCombo != null) FilterAuthorCombo.SelectedIndex = 0;
            if (FilterStatusCombo != null) FilterStatusCombo.SelectedIndex = 0;
            if (FilterGenreCombo != null) FilterGenreCombo.SelectedIndex = 0;
            if (FilterPublisherCombo != null) FilterPublisherCombo.SelectedIndex = 0;
            if (FilterYearCombo != null) FilterYearCombo.SelectedIndex = 0;

            SetSearchPlaceholder();
            if (ClearSearchBtn != null)
                ClearSearchBtn.Visibility = Visibility.Collapsed;

            if (_searchResults != null)
                _searchResults.Clear();

            if (SearchResultsList != null)
                SearchResultsList.Visibility = Visibility.Collapsed;

            ApplyAllFilters();
        }

        private void DgBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgBooks?.SelectedItem is BookGroupItem selectedBook)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    NavigationService?.Navigate(new AddBook(selectedBook.GroupId));
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка при открытии книги: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        #endregion

        #region Навигация

        private void AdmissionBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AdmissionBook());
        }

        private void AddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddBook());
        }

        private void BulkAddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new BulkAddBooks());
        }

        private void DisposalBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new DisposalAct());
        }

        private void FundMovementBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new FundMovementPage());
        }

        #endregion

        #region Статистика

        private void UpdateStatistics()
        {
            if (_allBooks == null || _allBooks.Count == 0) return;

            if (TotalBooksCount != null)
                TotalBooksCount.Text = _allBooks.Count.ToString();

            int totalCopies = _allBooks.Sum(b => b.TotalCopies);
            int availableCopies = _allBooks.Sum(b => b.AvailableCopies);
            int issuedCopies = _allBooks.Sum(b => b.IssuedCopies);
            int disposedCopies = _allBooks.Sum(b => b.DisposedCopies);

            if (TotalCopiesCount != null)
                TotalCopiesCount.Text = totalCopies.ToString();
            if (AvailableBooksCount != null)
                AvailableBooksCount.Text = availableCopies.ToString();
            if (IssuedBooksCount != null)
                IssuedBooksCount.Text = issuedCopies.ToString();
            if (DisposedBooksCount != null)
                DisposedBooksCount.Text = disposedCopies.ToString();
        }

        #endregion

       
    }
}