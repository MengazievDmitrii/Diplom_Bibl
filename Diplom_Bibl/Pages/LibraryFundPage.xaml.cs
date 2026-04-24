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
    public partial class LibraryFundPage : Page
    {
        private readonly string _conn = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private ObservableCollection<BookItem> _allBooks;
        private ObservableCollection<BookItem> _filteredBooks;
        private List<StatusItem> _allStatuses = new List<StatusItem>();
        private List<GenreItem> _allGenres = new List<GenreItem>();
        private List<PublisherItem> _allPublishers = new List<PublisherItem>();
        private List<AuthorItem> _allAuthors = new List<AuthorItem>();
        private bool _isPlaceholderActive = true;

        // Классы для элементов фильтрации
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

        public class AuthorItem
        {
            public int ID_Author { get; set; }
            public string SurnameAuthor { get; set; }
            public string NameAuthor { get; set; }
            public string PatronymicAuthor { get; set; }
            public string DisplayName => $"{SurnameAuthor} {NameAuthor} {PatronymicAuthor}".Trim();
        }

        public class BookItem
        {
            public int IDBook { get; set; }
            public string InventaryNumber { get; set; }
            public string TitleBook { get; set; }
            public string AuthorName { get; set; }
            public int? AuthorID { get; set; }
            public int YearPublishing { get; set; }
            public string PublishingHouseName { get; set; }
            public int? PublisherID { get; set; }
            public string GenreName { get; set; }
            public int? GenreID { get; set; }
            public string StatusName { get; set; }
            public int? StatusID { get; set; }

            // Цвет статуса для отображения
            private SolidColorBrush _statusColor;
            public SolidColorBrush StatusColor
            {
                get
                {
                    if (_statusColor == null)
                    {
                        string status = StatusName?.Trim().ToLower() ?? "";

                        // Точное определение статусов с учетом пробелов
                        if (status.Contains("в наличии"))
                            _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")); // Зеленый
                        else if (status.Contains("выдан"))
                            _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")); // Желтый
                        else if (status.Contains("списана") || status.Contains("утрачен"))
                            _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")); // Красный
                        else if (status.Contains("реставрация"))
                            _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A2B8")); // Голубой
                        else
                            _statusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")); // Серый
                    }
                    return _statusColor;
                }
            }
        }

        public LibraryFundPage()
        {
            InitializeComponent();
            SetSearchPlaceholder();
        }

        private void SetSearchPlaceholder()
        {
            TbSearch.Text = "Введите текст для поиска...";
            TbSearch.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
        }

        private void ClearPlaceholder()
        {
            if (_isPlaceholderActive)
            {
                TbSearch.Text = "";
                TbSearch.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearPlaceholder();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbSearch.Text))
            {
                SetSearchPlaceholder();
                ApplyFilters();
            }
        }

        private void AdmissionBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdmissionBook());
        }

        private void AddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddBook());
        }

        private void DisposalBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DisposalAct());
        }

        private void LoadStatuses()
        {
            try
            {
                const string sql = "SELECT ID_Status, TRIM(Title_Status) AS Title_Status FROM StatusBook ORDER BY Title_Status";

                var statuses = new List<StatusItem>();
                statuses.Add(new StatusItem { ID_Status = 0, Title_Status = "Все статусы" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statuses.Add(new StatusItem
                            {
                                ID_Status = Convert.ToInt32(reader["ID_Status"]),
                                Title_Status = reader["Title_Status"].ToString().Trim()
                            });
                        }
                    }
                }

                _allStatuses = statuses;
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

                var genres = new List<GenreItem>();
                genres.Add(new GenreItem { ID_Gener = 0, TitleGener = "Все жанры" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            genres.Add(new GenreItem
                            {
                                ID_Gener = Convert.ToInt32(reader["ID_Gener"]),
                                TitleGener = reader["TitleGener"].ToString()
                            });
                        }
                    }
                }

                _allGenres = genres;
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

                var publishers = new List<PublisherItem>();
                publishers.Add(new PublisherItem { ID_Publisher = 0, TitlePublishingHouse = "Все издательства" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            publishers.Add(new PublisherItem
                            {
                                ID_Publisher = Convert.ToInt32(reader["IDPublishingHouse"]),
                                TitlePublishingHouse = reader["TitlePublishingHouse"].ToString()
                            });
                        }
                    }
                }

                _allPublishers = publishers;
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

        private void LoadAuthors()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";

                var authors = new List<AuthorItem>();
                authors.Add(new AuthorItem { ID_Author = 0, SurnameAuthor = "Все авторы", NameAuthor = "", PatronymicAuthor = "" });

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            authors.Add(new AuthorItem
                            {
                                ID_Author = Convert.ToInt32(reader["ID_Autor"]),
                                SurnameAuthor = reader["SurnameAuthor"].ToString(),
                                NameAuthor = reader["NameAuthor"].ToString(),
                                PatronymicAuthor = reader["PatronymicAuthor"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                _allAuthors = authors;
                FilterAuthorCombo.ItemsSource = _allAuthors;
                FilterAuthorCombo.DisplayMemberPath = "DisplayName";
                FilterAuthorCombo.SelectedValuePath = "ID_Author";
                FilterAuthorCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки авторов: {ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadBooks()
        {
            try
            {
                const string sql = @"
                    SELECT 
                        b.IDBook,
                        b.InventaryNumber,
                        b.TitleBook,
                        b.Author,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        b.YearPublishing,
                        b.PublishingHouse,
                        ISNULL(p.TitlePublishingHouse, '') AS PublishingHouseName,
                        b.Gener,
                        ISNULL(g.TitleGener, '') AS GenreName,
                        b.StatusBook,
                        ISNULL(s.Title_Status, 'Неизвестно') AS StatusName
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                    LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                    LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                    ORDER BY b.TitleBook";

                var list = new ObservableCollection<BookItem>();
                var yearsSet = new HashSet<int>();

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var book = new BookItem
                            {
                                IDBook = Convert.ToInt32(reader["IDBook"]),
                                InventaryNumber = reader["InventaryNumber"].ToString(),
                                TitleBook = reader["TitleBook"].ToString(),
                                AuthorName = reader["AuthorName"].ToString(),
                                AuthorID = reader["Author"] != DBNull.Value ? Convert.ToInt32(reader["Author"]) : (int?)null,
                                YearPublishing = Convert.ToInt32(reader["YearPublishing"]),
                                PublishingHouseName = reader["PublishingHouseName"].ToString(),
                                PublisherID = reader["PublishingHouse"] != DBNull.Value ? Convert.ToInt32(reader["PublishingHouse"]) : (int?)null,
                                GenreName = reader["GenreName"].ToString(),
                                GenreID = reader["Gener"] != DBNull.Value ? Convert.ToInt32(reader["Gener"]) : (int?)null,
                                StatusName = reader["StatusName"].ToString(),
                                StatusID = reader["StatusBook"] != DBNull.Value ? Convert.ToInt32(reader["StatusBook"]) : (int?)null
                            };

                            list.Add(book);

                            // Собираем уникальные годы
                            if (book.YearPublishing > 0)
                                yearsSet.Add(book.YearPublishing);
                        }
                    }
                }

                _allBooks = list;
                _filteredBooks = new ObservableCollection<BookItem>(_allBooks);
                DgBooks.ItemsSource = _filteredBooks;

                // Загружаем все фильтры
                LoadStatuses();
                LoadGenres();
                LoadPublishers();
                LoadAuthors();

                // Заполняем фильтр по годам
                LoadYearFilters(yearsSet.ToList());
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

        private void ApplyFilters()
        {
            if (_allBooks == null) return;

            string searchTerm = _isPlaceholderActive ? "" : TbSearch.Text?.Trim().ToLower() ?? "";

            // Получаем выбранные фильтры
            int selectedStatusId = GetSelectedId(FilterStatusCombo);
            int selectedGenreId = GetSelectedId(FilterGenreCombo);
            int selectedPublisherId = GetSelectedId(FilterPublisherCombo);
            int selectedAuthorId = GetSelectedId(FilterAuthorCombo);

            string yearFilter = "Все годы";
            if (FilterYearCombo.SelectedItem is ComboBoxItem yearItem)
            {
                yearFilter = yearItem.Content.ToString();
            }

            var filtered = _allBooks.AsEnumerable();

            // Применяем поиск
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(b =>
                    b.InventaryNumber.ToLower().Contains(searchTerm) ||
                    b.TitleBook.ToLower().Contains(searchTerm) ||
                    b.AuthorName.ToLower().Contains(searchTerm) ||
                    b.PublishingHouseName.ToLower().Contains(searchTerm) ||
                    b.GenreName.ToLower().Contains(searchTerm) ||
                    b.StatusName.ToLower().Contains(searchTerm) ||
                    b.YearPublishing.ToString().Contains(searchTerm)
                );
            }

            // Применяем фильтр по статусу
            if (selectedStatusId > 0)
            {
                filtered = filtered.Where(b => b.StatusID == selectedStatusId);
            }

            // Применяем фильтр по жанру
            if (selectedGenreId > 0)
            {
                filtered = filtered.Where(b => b.GenreID == selectedGenreId);
            }

            // Применяем фильтр по издательству
            if (selectedPublisherId > 0)
            {
                filtered = filtered.Where(b => b.PublisherID == selectedPublisherId);
            }

            // Применяем фильтр по автору
            if (selectedAuthorId > 0)
            {
                filtered = filtered.Where(b => b.AuthorID == selectedAuthorId);
            }

            // Применяем фильтр по году
            if (yearFilter != "Все годы" && int.TryParse(yearFilter, out int year))
            {
                filtered = filtered.Where(b => b.YearPublishing == year);
            }

            var resultList = filtered.ToList();

            _filteredBooks.Clear();
            foreach (var item in resultList)
            {
                _filteredBooks.Add(item);
            }

            // Обновляем счетчик результатов
            SearchCountLabel.Text = $"Найдено: {resultList.Count} из {_allBooks.Count}";
        }

        private int GetSelectedId(ComboBox comboBox)
        {
            if (comboBox.SelectedValue != null && int.TryParse(comboBox.SelectedValue.ToString(), out int id))
            {
                return id;
            }
            return 0;
        }

        private void UpdateStatistics()
        {
            if (_allBooks == null) return;

            TotalBooksCount.Text = _allBooks.Count.ToString();

            // Получаем ID статусов с правильными названиями из БД
            int availableStatusId = GetStatusIdByName("В наличии");
            int issuedStatusId = GetStatusIdByName("Выдан");
            int disposedStatusId = GetStatusIdByName("Списана (архив)");
            int lostStatusId = GetStatusIdByName("Утрачен");

            int availableCount = _allBooks.Count(b => b.StatusID == availableStatusId);
            int issuedCount = _allBooks.Count(b => b.StatusID == issuedStatusId);
            int disposedCount = _allBooks.Count(b => b.StatusID == disposedStatusId);
            int lostCount = _allBooks.Count(b => b.StatusID == lostStatusId);

            AvailableBooksCount.Text = availableCount.ToString();
            IssuedBooksCount.Text = issuedCount.ToString();
            // Можно добавить счетчик списанных + утраченных
            DisposedBooksCount.Text = (disposedCount + lostCount).ToString();

            System.Diagnostics.Debug.WriteLine($"Статистика: Всего={_allBooks.Count}, В наличии={availableCount}, Выдано={issuedCount}, Списано+Утрачено={disposedCount + lostCount}");
            System.Diagnostics.Debug.WriteLine($"ID статусов: В наличии={availableStatusId}, Выдан={issuedStatusId}, Списана (архив)={disposedStatusId}, Утрачен={lostStatusId}");
        }

        private int GetStatusIdByName(string statusName)
        {
            var status = _allStatuses.FirstOrDefault(s =>
                s.Title_Status != null && s.Title_Status.Trim().Equals(statusName, StringComparison.OrdinalIgnoreCase));
            return status?.ID_Status ?? 0;
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPlaceholderActive) return;
            ApplyFilters();
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            TbSearch.Text = "";
            FilterStatusCombo.SelectedIndex = 0;
            FilterGenreCombo.SelectedIndex = 0;
            FilterPublisherCombo.SelectedIndex = 0;
            FilterAuthorCombo.SelectedIndex = 0;
            FilterYearCombo.SelectedIndex = 0;
            SetSearchPlaceholder();
        }

        private void DgBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgBooks.SelectedItem is BookItem selectedBook)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    var addBookPage = new AddBook(selectedBook.IDBook);
                    NavigationService.Navigate(addBookPage);
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBooks();
        }

        private void BulkAddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new BulkAddBooks());
        }

        private void FundMovementBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new FundMovementPage());
        }
    }
}