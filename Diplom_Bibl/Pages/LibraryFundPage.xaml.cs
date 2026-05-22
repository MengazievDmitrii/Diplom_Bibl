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
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class LibraryFundPage : Page
    {
        private readonly string _conn = DatabaseHelper.ConnectionString;
        private readonly ObservableCollection<BookGroupItem> _allBooks;
        private ObservableCollection<BookGroupItem> _filteredBooks;
        private readonly List<StatusItem> _allStatuses;
        private readonly List<GenreItem> _allGenres;
        private readonly List<PublisherItem> _allPublishers;
        private readonly List<AuthorFilterItem> _allAuthors;
        private readonly ObservableCollection<BookSearchItem> _allBooksForSearch;

        private bool _isSearchPlaceholderActive;
        private bool _isBookSearchProgrammatic;

        // ==================== ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ====================

        public class BookSearchItem
        {
            public int IDBook { get; set; }
            public string DisplayName { get; set; }
            public string TitleBook { get; set; }
            public string AuthorName { get; set; }
        }

        public class AuthorFilterItem
        {
            public int ID_Author { get; set; }
            public string DisplayAuthors { get; set; }
        }

        public class BookGroupItem
        {
            public int GroupId { get; set; }
            public string TitleBook { get; set; }
            public string AuthorName { get; set; }
            public int? AuthorID { get; set; }
            public int YearPublishing { get; set; }
            public string PublishingHouseName { get; set; }
            public int? PublisherID { get; set; }
            public string GenreName { get; set; }
            public int? GenreID { get; set; }
            public int TotalCopies { get; set; }
            public int AvailableCopies { get; set; }
            public int IssuedCopies { get; set; }
            public int DisposedCopies { get; set; }
            public string StatusName { get; set; }
            public int? StatusID { get; set; }
            public List<int> BookIds { get; set; }

            private SolidColorBrush _statusColor;
            private string _statusText;

            /// <summary>
            /// Цвет статуса:
            /// - Зеленый (#28A745) - если есть книги в наличии
            /// - Желтый (#FFC107) - если есть выданные книги
            /// - Красный (#DC3545) - если все книги списаны
            /// </summary>
            public SolidColorBrush StatusColor
            {
                get
                {
                    if (_statusColor != null) return _statusColor;

                    if (AvailableCopies > 0 && IssuedCopies == 0)
                        _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                    else if (AvailableCopies > 0 && IssuedCopies > 0)
                        _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                    else if (AvailableCopies == 0 && IssuedCopies > 0)
                        _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    else
                        _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));

                    return _statusColor;
                }
            }

            /// <summary>
            /// Текст статуса для отображения
            /// </summary>
            public string StatusText
            {
                get
                {
                    if (AvailableCopies > 0 && IssuedCopies == 0)
                        return "В наличии";
                    else if (AvailableCopies > 0 && IssuedCopies > 0)
                        return "Частично выдана";
                    else if (AvailableCopies == 0 && IssuedCopies > 0)
                        return "Выдана";
                    else
                        return "Списана";
                }
            }
        }

        public class StatusItem
        {
            public int ID_Status { get; set; }
            public string Title_Status { get; set; }
            public string DisplayName => Title_Status;
        }

        public class GenreItem
        {
            public int ID_Gener { get; set; }
            public string TitleGener { get; set; }
            public string DisplayName => TitleGener;
        }

        public class PublisherItem
        {
            public int ID_Publisher { get; set; }
            public string TitlePublishingHouse { get; set; }
            public string DisplayName => TitlePublishingHouse;
        }

        // ==================== КОНСТРУКТОР ====================

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

            SetSearchPlaceholder();

            CbBookSearch.IsEditable = true;
            CbBookSearch.StaysOpenOnEdit = true;
        }

        // ==================== ПОИСК И ФИЛЬТРАЦИЯ ====================

        private void SetSearchPlaceholder()
        {
            _isSearchPlaceholderActive = true;
            CbBookSearch.Text = "Введите название книги или автора...";
            CbBookSearch.Foreground = Brushes.Gray;
        }

        private void ClearSearchPlaceholder()
        {
            if (_isSearchPlaceholderActive)
            {
                _isSearchPlaceholderActive = false;
                CbBookSearch.Text = "";
                CbBookSearch.Foreground = Brushes.Black;
            }
        }

        private void CbBookSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearSearchPlaceholder();
        }

        private void CbBookSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CbBookSearch.Text))
            {
                SetSearchPlaceholder();
                if (!_isBookSearchProgrammatic)
                {
                    ApplyAllFilters();
                }
            }
        }

        /// <summary>
        /// Обработчик нажатия клавиш в ComboBox (для Backspace)
        /// </summary>
        private void CbBookSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Очистка при нажатии Backspace или Delete
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                if (string.IsNullOrEmpty(comboBox.Text) || comboBox.Text.Length <= 1)
                {
                    SetSearchPlaceholder();
                    ApplyAllFilters();
                }
            }
            // Очистка при нажатии Escape
            else if (e.Key == Key.Escape)
            {
                SetSearchPlaceholder();
                ApplyAllFilters();
                e.Handled = true;
            }
        }

        private void CbBookSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbBookSearch.SelectedItem is BookSearchItem selectedBook)
            {
                _isBookSearchProgrammatic = true;
                CbBookSearch.Text = selectedBook.TitleBook;
                ApplyAllFilters();
                _isBookSearchProgrammatic = false;
            }
        }

        // ==================== ЗАГРУЗКА ДАННЫХ ====================

        private void LoadBooksForSearch()
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
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string title = reader["TitleBook"].ToString();
                            string author = reader["AuthorName"].ToString();

                            _allBooksForSearch.Add(new BookSearchItem
                            {
                                IDBook = Convert.ToInt32(reader["IDBook"]),
                                TitleBook = title,
                                AuthorName = author,
                                DisplayName = $"{title} - {author}".TrimEnd(' ', '-')
                            });
                        }
                    }
                }

                CbBookSearch.ItemsSource = _allBooksForSearch;
                CbBookSearch.DisplayMemberPath = "DisplayName";
                CbBookSearch.SelectedValuePath = "IDBook";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки списка книг: {ex.Message}");
            }
        }

        private void LoadAuthorsForFilter()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";

                _allAuthors.Clear();
                _allAuthors.Add(new AuthorFilterItem { ID_Author = 0, DisplayAuthors = "Все авторы" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = Convert.ToInt32(reader["ID_Autor"]);
                            string surname = reader["SurnameAuthor"].ToString();
                            string name = reader["NameAuthor"].ToString();
                            string patronymic = reader["PatronymicAuthor"]?.ToString() ?? "";

                            _allAuthors.Add(new AuthorFilterItem
                            {
                                ID_Author = id,
                                DisplayAuthors = $"{surname} {name} {patronymic}".Trim()
                            });
                        }
                    }
                }

                FilterAuthorCombo.ItemsSource = _allAuthors;
                FilterAuthorCombo.SelectedValuePath = "ID_Author";
                FilterAuthorCombo.DisplayMemberPath = "DisplayAuthors";
                FilterAuthorCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadStatuses()
        {
            try
            {
                const string sql = "SELECT ID_Status, TRIM(Title_Status) AS Title_Status FROM StatusBook ORDER BY Title_Status";

                _allStatuses.Clear();
                _allStatuses.Add(new StatusItem { ID_Status = 0, Title_Status = "Все статусы" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allStatuses.Add(new StatusItem
                            {
                                ID_Status = Convert.ToInt32(reader["ID_Status"]),
                                Title_Status = reader["Title_Status"].ToString().Trim()
                            });
                        }
                    }
                }

                FilterStatusCombo.ItemsSource = _allStatuses;
                FilterStatusCombo.DisplayMemberPath = "DisplayName";
                FilterStatusCombo.SelectedValuePath = "ID_Status";
                FilterStatusCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки статусов: {ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadGenres()
        {
            try
            {
                const string sql = "SELECT ID_Gener, TitleGener FROM GenersBook ORDER BY TitleGener";

                _allGenres.Clear();
                _allGenres.Add(new GenreItem { ID_Gener = 0, TitleGener = "Все жанры" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allGenres.Add(new GenreItem
                            {
                                ID_Gener = Convert.ToInt32(reader["ID_Gener"]),
                                TitleGener = reader["TitleGener"].ToString()
                            });
                        }
                    }
                }

                FilterGenreCombo.ItemsSource = _allGenres;
                FilterGenreCombo.DisplayMemberPath = "DisplayName";
                FilterGenreCombo.SelectedValuePath = "ID_Gener";
                FilterGenreCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки жанров: {ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadPublishers()
        {
            try
            {
                const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";

                _allPublishers.Clear();
                _allPublishers.Add(new PublisherItem { ID_Publisher = 0, TitlePublishingHouse = "Все издательства" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allPublishers.Add(new PublisherItem
                            {
                                ID_Publisher = Convert.ToInt32(reader["IDPublishingHouse"]),
                                TitlePublishingHouse = reader["TitlePublishingHouse"].ToString()
                            });
                        }
                    }
                }

                FilterPublisherCombo.ItemsSource = _allPublishers;
                FilterPublisherCombo.DisplayMemberPath = "DisplayName";
                FilterPublisherCombo.SelectedValuePath = "ID_Publisher";
                FilterPublisherCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки издательств: {ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadBooks()
        {
            try
            {
                string sql = @"
                    SELECT 
                        MIN(b.IDBook) AS GroupId,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        MIN(b.Author) AS AuthorID,
                        MIN(b.YearPublishing) AS YearPublishing,
                        MIN(p.TitlePublishingHouse) AS PublishingHouseName,
                        MIN(b.PublishingHouse) AS PublisherID,
                        MIN(g.TitleGener) AS GenreName,
                        MIN(b.Gener) AS GenreID,
                        COUNT(*) AS TotalCopies,
                        SUM(CASE WHEN s.Title_Status LIKE '%В наличии%' THEN 1 ELSE 0 END) AS AvailableCopies,
                        SUM(CASE WHEN s.Title_Status LIKE '%Выдан%' THEN 1 ELSE 0 END) AS IssuedCopies,
                        SUM(CASE WHEN s.Title_Status LIKE '%Списана%' OR s.Title_Status LIKE '%Утрачен%' THEN 1 ELSE 0 END) AS DisposedCopies,
                        MAX(s.Title_Status) AS StatusName,
                        MAX(b.StatusBook) AS StatusID
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                    LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                    LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                    GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor
                    ORDER BY b.TitleBook";

                var yearsSet = new HashSet<int>();
                _allBooks.Clear();

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var book = new BookGroupItem
                            {
                                GroupId = Convert.ToInt32(reader["GroupId"]),
                                TitleBook = reader["TitleBook"].ToString(),
                                AuthorName = reader["AuthorName"].ToString(),
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
                                StatusID = reader["StatusID"] != DBNull.Value ? Convert.ToInt32(reader["StatusID"]) : (int?)null,
                                BookIds = new List<int>()
                            };

                            _allBooks.Add(book);

                            if (book.YearPublishing > 0)
                                yearsSet.Add(book.YearPublishing);
                        }
                    }
                }

                LoadAuthorsForFilter();
                LoadStatuses();
                LoadGenres();
                LoadPublishers();
                LoadBooksForSearch();
                LoadYearFilters(yearsSet.ToList());

                ApplyAllFilters();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadYearFilters(List<int> years)
        {
            FilterYearCombo.Items.Clear();
            FilterYearCombo.Items.Add(new ComboBoxItem { Content = "Все годы", IsSelected = true });

            foreach (var year in years.OrderByDescending(y => y))
            {
                FilterYearCombo.Items.Add(new ComboBoxItem { Content = year.ToString() });
            }

            FilterYearCombo.SelectedIndex = 0;
        }

        // ==================== ПРИМЕНЕНИЕ ФИЛЬТРОВ ====================

        private void ApplyAllFilters()
        {
            if (_allBooks == null || _allBooks.Count == 0) return;

            string searchTerm = _isSearchPlaceholderActive ? "" : CbBookSearch.Text?.Trim().ToLower() ?? "";

            int selectedAuthorId = GetSelectedAuthorId();
            int selectedStatusId = GetSelectedId(FilterStatusCombo);
            int selectedGenreId = GetSelectedId(FilterGenreCombo);
            int selectedPublisherId = GetSelectedId(FilterPublisherCombo);

            string yearFilter = "Все годы";
            if (FilterYearCombo.SelectedItem is ComboBoxItem yearItem)
            {
                yearFilter = yearItem.Content.ToString();
            }

            var filtered = _allBooks.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(b =>
                    b.TitleBook.ToLower().Contains(searchTerm) ||
                    b.AuthorName.ToLower().Contains(searchTerm));
            }

            if (selectedAuthorId > 0)
                filtered = filtered.Where(b => b.AuthorID == selectedAuthorId);

            if (selectedStatusId > 0)
                filtered = filtered.Where(b => b.StatusID == selectedStatusId);

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

            DgBooks.ItemsSource = _filteredBooks;
            SearchCountLabel.Text = $"Найдено: {resultList.Count} из {_allBooks.Count} наименований";
        }

        private int GetSelectedAuthorId()
        {
            if (FilterAuthorCombo.SelectedValue != null && int.TryParse(FilterAuthorCombo.SelectedValue.ToString(), out int id))
                return id;
            return 0;
        }

        private int GetSelectedId(ComboBox comboBox)
        {
            if (comboBox.SelectedValue != null && int.TryParse(comboBox.SelectedValue.ToString(), out int id))
                return id;
            return 0;
        }

        // ==================== СТАТИСТИКА ====================

        private void UpdateStatistics()
        {
            if (_allBooks == null || _allBooks.Count == 0) return;

            TotalBooksCount.Text = _allBooks.Count.ToString();

            int totalCopies = _allBooks.Sum(b => b.TotalCopies);
            int availableCopies = _allBooks.Sum(b => b.AvailableCopies);
            int issuedCopies = _allBooks.Sum(b => b.IssuedCopies);
            int disposedCopies = _allBooks.Sum(b => b.DisposedCopies);

            TotalCopiesCount.Text = totalCopies.ToString();
            AvailableBooksCount.Text = availableCopies.ToString();
            IssuedBooksCount.Text = issuedCopies.ToString();
            DisposedBooksCount.Text = disposedCopies.ToString();
        }

        // ==================== ОБРАБОТЧИКИ ====================

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyAllFilters();
        }

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterAuthorCombo.SelectedIndex = 0;
            FilterStatusCombo.SelectedIndex = 0;
            FilterGenreCombo.SelectedIndex = 0;
            FilterPublisherCombo.SelectedIndex = 0;
            FilterYearCombo.SelectedIndex = 0;
            SetSearchPlaceholder();
            ApplyAllFilters();
        }

        private void DgBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(DgBooks.SelectedItem is BookGroupItem selectedBook)) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                int firstBookId = GetFirstBookIdByGroup(selectedBook.TitleBook, selectedBook.AuthorName);
                if (firstBookId > 0)
                {
                    NavigationService?.Navigate(new AddBook(firstBookId));
                }
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

        private int GetFirstBookIdByGroup(string title, string authorName)
        {
            try
            {
                string sql = @"
                    SELECT TOP 1 b.IDBook
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE b.TitleBook = @Title 
                      AND ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') = @AuthorName";

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@AuthorName", authorName);
                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // ==================== НАВИГАЦИЯ ====================

        private void AdmissionBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AdmissionBook());
        }

        private void AddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddBook());
        }

        private void DisposalBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new DisposalAct());
        }

        private void BulkAddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new BulkAddBooks());
        }

        private void FundMovementBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new FundMovementPage());
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBooks();
        }
    }
}