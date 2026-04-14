using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    public partial class ReturnBooksPage : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private BookIssuanceService _issuanceService;
        private ObservableCollection<BookIssuanceService.ReaderInfo> _readers;
        private ObservableCollection<BookIssuanceService.IssuanceItem> _activeIssuances;
        private ObservableCollection<GroupIssuanceDisplayItem> _groupIssuances;
        private BookIssuanceService.ReaderInfo _selectedReader;
        private bool _isPlaceholderActive = true;

        // Класс для отображения групповых выдач
        public class GroupIssuanceDisplayItem
        {
            public int GroupIssuanceId { get; set; }
            public string GroupCode { get; set; }
            public string TeacherName { get; set; }
            public int TeacherId { get; set; }
            public string StudentName { get; set; }
            public int StudentId { get; set; }
            public string SubjectName { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public int TotalBooks { get; set; }
            public int ReturnedBooks { get; set; }
            public int RemainingBooks => TotalBooks - ReturnedBooks;
            public string Status { get; set; }
            public ObservableCollection<GroupBookGroup> BookGroups { get; set; }

            public bool IsOverdue => DateTime.Now > PlannedReturnDate && Status == "Выдана";

            public SolidColorBrush OverdueColor => IsOverdue ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));

            public string DisplayName => $"👥 Группа {GroupCode}";
            public string TeacherInfo => $"Преподаватель: {TeacherName}";
            public string StudentInfo => $"Студент: {StudentName}";
            public string SubInfo => $"Всего книг: {TotalBooks}, возвращено: {ReturnedBooks}";
            public bool ReturnButtonVisible => RemainingBooks > 0;
            public bool ReturnAllButtonVisible => RemainingBooks > 0;
        }

        public class GroupBookItem
        {
            public int ItemId { get; set; }
            public int BookId { get; set; }
            public string BookTitle { get; set; }
            public string AuthorName { get; set; }
            public string InventaryNumber { get; set; }
            public int Quantity { get; set; }
            public int ReturnedQuantity { get; set; }
            public int RemainingQuantity => Quantity - ReturnedQuantity;
            public bool IsFullyReturned => RemainingQuantity == 0;
        }

        public class GroupBookGroup
        {
            public string BookTitle { get; set; }
            public string AuthorName { get; set; }
            public int TotalQuantity { get; set; }
            public int TotalReturned { get; set; }
            public int RemainingQuantity => TotalQuantity - TotalReturned;
            public List<GroupBookItem> Items { get; set; }
            public string DisplayName => $"{BookTitle} - {AuthorName}";
        }

        public ReturnBooksPage()
        {
            InitializeComponent();
            _issuanceService = new BookIssuanceService(_connectionString);
            _readers = new ObservableCollection<BookIssuanceService.ReaderInfo>();
            _activeIssuances = new ObservableCollection<BookIssuanceService.IssuanceItem>();
            _groupIssuances = new ObservableCollection<GroupIssuanceDisplayItem>();

            LoadReaders();
        }

        private void LoadReaders()
        {
            try
            {
                _readers = _issuanceService.GetReaders();
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки читателей: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadActiveIssuances(int readerId, string readerType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ЗАГРУЗКА ВЫДАЧ для читателя ID={readerId}, тип={readerType} ===");

                // Загружаем индивидуальные выдачи
                _activeIssuances = _issuanceService.GetReaderActiveIssuances(readerId);
                System.Diagnostics.Debug.WriteLine($"Загружено индивидуальных выдач: {_activeIssuances.Count}");

                foreach (var item in _activeIssuances)
                {
                    item.IssuanceTypeDisplay = "Индивидуальная";
                    item.OverdueColor = item.IsOverdue ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }

                // Загружаем групповые выдачи ТОЛЬКО если это преподаватель
                _groupIssuances.Clear();

                if (readerType.ToLower().Contains("сотрудник"))
                {
                    LoadAllGroupIssuances();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Читатель не является сотрудником, групповые выдачи не загружаются");
                }

                DisplayAllIssuances();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки выдач: {ex.Message}");
                 CustomMessageBox.Show($"Ошибка загрузки выдач: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void LoadAllGroupIssuances()
        {
            try
            {
                _groupIssuances.Clear();

                // Загружаем ТОЛЬКО групповые выдачи, где текущий читатель является преподавателем
                string sql = @"
            SELECT 
                gi.GroupIssuanceId,
                g.GroupCode,
                gi.SubjectName,
                gi.IssuanceDate,
                gi.PlannedReturnDate,
                gi.Status,
                t.ReaderID AS TeacherId,
                t.LastName + ' ' + t.FirstName + ' ' + ISNULL(t.MiddleName, '') AS TeacherName,
                s.ReaderID AS StudentId,
                s.LastName + ' ' + s.FirstName + ' ' + ISNULL(s.MiddleName, '') AS StudentName
            FROM GroupBookIssuance gi
            INNER JOIN Groups g ON gi.GroupId = g.GroupID
            INNER JOIN ReaderForm t ON gi.TeacherId = t.ReaderID
            LEFT JOIN ReaderForm s ON gi.StudentId = s.ReaderID
            WHERE gi.Status = 'Выдана' 
              AND gi.TeacherId = @TeacherId  -- Фильтр по конкретному преподавателю
            ORDER BY gi.PlannedReturnDate";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TeacherId", _selectedReader.ReaderID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var groupIssuance = new GroupIssuanceDisplayItem
                                {
                                    GroupIssuanceId = Convert.ToInt32(reader["GroupIssuanceId"]),
                                    GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                    TeacherName = reader["TeacherName"]?.ToString() ?? "",
                                    TeacherId = Convert.ToInt32(reader["TeacherId"]),
                                    StudentName = reader["StudentName"]?.ToString() ?? "Не указан",
                                    StudentId = reader["StudentId"] != DBNull.Value ? Convert.ToInt32(reader["StudentId"]) : 0,
                                    SubjectName = reader["SubjectName"]?.ToString() ?? "",
                                    IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                    PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                    Status = reader["Status"]?.ToString() ?? "",
                                    BookGroups = new ObservableCollection<GroupBookGroup>()
                                };

                                _groupIssuances.Add(groupIssuance);
                                System.Diagnostics.Debug.WriteLine($"  - Найдена групповая выдача для преподавателя {groupIssuance.TeacherName}: {groupIssuance.GroupCode}");
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Найдено групповых выдач для преподавателя ID={_selectedReader.ReaderID}: {_groupIssuances.Count}");

                // Загружаем книги для каждой групповой выдачи
                foreach (var group in _groupIssuances)
                {
                    LoadGroupBooks(group);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки групповых выдач: {ex.Message}");
            }
        }

        private void LoadGroupBooks(GroupIssuanceDisplayItem group)
        {
            try
            {
                string sql = @"
                    SELECT 
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        SUM(git.Quantity) AS TotalQuantity,
                        SUM(git.ReturnedQuantity) AS TotalReturned
                    FROM GroupIssuanceItems git
                    INNER JOIN Books b ON git.BookId = b.IDBook
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE git.GroupIssuanceId = @GroupIssuanceId
                    GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@GroupIssuanceId", group.GroupIssuanceId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var bookGroup = new GroupBookGroup
                                {
                                    BookTitle = reader["TitleBook"]?.ToString() ?? "",
                                    AuthorName = reader["AuthorName"]?.ToString() ?? "",
                                    TotalQuantity = Convert.ToInt32(reader["TotalQuantity"]),
                                    TotalReturned = Convert.ToInt32(reader["TotalReturned"]),
                                    Items = new List<GroupBookItem>()
                                };

                                group.BookGroups.Add(bookGroup);
                                group.TotalBooks += bookGroup.TotalQuantity;
                                group.ReturnedBooks += bookGroup.TotalReturned;
                            }
                        }
                    }
                }

                // Загружаем отдельные экземпляры для возврата
                LoadGroupBookItems(group);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки книг: {ex.Message}");
            }
        }

        private void LoadGroupBookItems(GroupIssuanceDisplayItem group)
        {
            try
            {
                string sql = @"
                    SELECT 
                        git.ItemId,
                        b.IDBook,
                        b.InventaryNumber,
                        git.Quantity,
                        git.ReturnedQuantity,
                        b.TitleBook
                    FROM GroupIssuanceItems git
                    INNER JOIN Books b ON git.BookId = b.IDBook
                    WHERE git.GroupIssuanceId = @GroupIssuanceId";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@GroupIssuanceId", group.GroupIssuanceId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string bookTitle = reader["TitleBook"]?.ToString() ?? "";
                                var bookGroup = group.BookGroups.FirstOrDefault(g => g.BookTitle == bookTitle);

                                if (bookGroup != null)
                                {
                                    bookGroup.Items.Add(new GroupBookItem
                                    {
                                        ItemId = Convert.ToInt32(reader["ItemId"]),
                                        BookId = Convert.ToInt32(reader["IDBook"]),
                                        BookTitle = bookTitle,
                                        InventaryNumber = reader["InventaryNumber"]?.ToString() ?? "",
                                        Quantity = Convert.ToInt32(reader["Quantity"]),
                                        ReturnedQuantity = Convert.ToInt32(reader["ReturnedQuantity"])
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки экземпляров: {ex.Message}");
            }
        }

        private void DisplayAllIssuances()
        {
            var allItems = new ObservableCollection<object>();

            System.Diagnostics.Debug.WriteLine($"=== ОТОБРАЖЕНИЕ ВЫДАЧ ===");
            System.Diagnostics.Debug.WriteLine($"Индивидуальных выдач: {_activeIssuances.Count}");
            System.Diagnostics.Debug.WriteLine($"Групповых выдач: {_groupIssuances.Count}");

            // Добавляем индивидуальные выдачи
            foreach (var item in _activeIssuances)
            {
                allItems.Add(new
                {
                    Type = "individual",
                    item.Id,
                    item.BookTitle,
                    item.InventaryNumber,
                    item.IssuanceDate,
                    item.PlannedReturnDate,
                    item.Status,
                    item.IsOverdue,
                    item.OverdueColor,
                    DisplayName = $"📚 {item.BookTitle}",
                    SubInfo = $"Инв. №: {item.InventaryNumber}",
                    ReturnButtonVisible = true,
                    ReturnAllButtonVisible = false
                });
            }

            // Добавляем групповые выдачи
            foreach (var group in _groupIssuances.Where(g => g.BookGroups.Any()))
            {
                allItems.Add(new
                {
                    Type = "group",
                    Id = group.GroupIssuanceId,
                    group.GroupCode,
                    group.TeacherName,
                    group.TeacherId,
                    group.StudentName,
                    group.StudentId,
                    group.SubjectName,
                    group.IssuanceDate,
                    group.PlannedReturnDate,
                    group.Status,
                    group.TotalBooks,
                    group.ReturnedBooks,
                    group.RemainingBooks,
                    group.IsOverdue,
                    group.OverdueColor,
                    BookGroups = group.BookGroups,
                    DisplayName = group.DisplayName,
                    TeacherInfo = group.TeacherInfo,
                    StudentInfo = group.StudentInfo,
                    SubInfo = group.SubInfo,
                    ReturnButtonVisible = group.ReturnButtonVisible,
                    ReturnAllButtonVisible = group.ReturnAllButtonVisible
                });

                System.Diagnostics.Debug.WriteLine($"  - Добавлена группа {group.GroupCode} с {group.BookGroups.Count} книгами");
            }

            IssuancesList.ItemsSource = allItems;
            BooksSection.Visibility = allItems.Any() ? Visibility.Visible : Visibility.Collapsed;
            NoBooksText.Visibility = allItems.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ReaderSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderActive && ReaderSearchBox.Text == "Введите фамилию, имя или номер билета...")
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
                ReaderSearchBox.Text = "Введите фамилию, имя или номер билета...";
                ReaderSearchBox.Foreground = Brushes.Gray;
                _isPlaceholderActive = true;
                ReadersListBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ReaderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ReadersListBox == null) return;

            string search = ReaderSearchBox.Text.Trim();
            if (_isPlaceholderActive || string.IsNullOrWhiteSpace(search))
            {
                ReadersListBox.Visibility = Visibility.Collapsed;
                return;
            }

            var filtered = _readers.Where(r =>
                r.LastName.ToLower().Contains(search.ToLower()) ||
                r.FirstName.ToLower().Contains(search.ToLower()) ||
                r.CardNumber.ToLower().Contains(search.ToLower())).Take(10).ToList();

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
                SelectReader(selected);
            }
        }

        private void SelectReader(BookIssuanceService.ReaderInfo reader)
        {
            _selectedReader = reader;
            ReaderSearchBox.Text = reader.DisplayInfo;
            ReaderSearchBox.Foreground = Brushes.Black;
            ReadersListBox.Visibility = Visibility.Collapsed;

            ReaderFullNameText.Text = reader.FullName;
            ReaderCardNumberText.Text = reader.CardNumber;
            ReaderTypeText.Text = reader.TypeName;
            ReaderInfoSection.Visibility = Visibility.Visible;

            // Загружаем выдачи - групповые выдачи загружаются для всех, 
            // но отображаются они у преподавателя (через TeacherId)
            LoadActiveIssuances(reader.ReaderID, reader.TypeName);
        }

        private void ClearReaderButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedReader = null;
            ReaderSearchBox.Text = "Введите фамилию, имя или номер билета...";
            ReaderSearchBox.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
            ReadersListBox.Visibility = Visibility.Collapsed;
            ReaderInfoSection.Visibility = Visibility.Collapsed;
            BooksSection.Visibility = Visibility.Collapsed;
            _activeIssuances.Clear();
            _groupIssuances.Clear();
        }

        private void ReturnIndividualBook(int issuanceId)
        {
            try
            {
                var result =  CustomMessageBox.Show(
                    "Вернуть книгу?",
                    "Подтверждение возврата",
                    CustomMessageBoxButton.YesNo,
                    CustomMessageBoxImage.Question);

                if (result == CustomMessageBoxResult.Yes)
                {
                    _issuanceService.ReturnBook("individual", issuanceId);

                     CustomMessageBox.Show("Книга успешно возвращена!", "Успех",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                    if (_selectedReader != null)
                    {
                        LoadActiveIssuances(_selectedReader.ReaderID, _selectedReader.TypeName);
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при возврате книги: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ReturnGroupBooks(int groupIssuanceId, ObservableCollection<GroupBookGroup> bookGroups, bool returnAll = false)
        {
            try
            {
                if (returnAll)
                {
                    var allItems = bookGroups.SelectMany(g => g.Items).Where(i => i.RemainingQuantity > 0).ToList();

                    var result =  CustomMessageBox.Show(
                        $"Вернуть все оставшиеся книги ({allItems.Count} шт.)?",
                        "Подтверждение возврата всех книг",
                        CustomMessageBoxButton.YesNo,
                        CustomMessageBoxImage.Question);

                    if (result != CustomMessageBoxResult.Yes) return;

                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                // Обновляем все невозвращенные книги
                                foreach (var bookGroup in bookGroups.Where(g => g.RemainingQuantity > 0))
                                {
                                    foreach (var book in bookGroup.Items.Where(i => i.RemainingQuantity > 0))
                                    {
                                        // Обновляем количество книг и статус
                                        string updateBooksSql = @"
                                    UPDATE Books 
                                    SET 
                                        CopiesInUse = CopiesInUse - 1,
                                        AvailableCopies = AvailableCopies + 1,
                                        StatusBook = 1
                                    WHERE IDBook = @BookId";

                                        using (var cmd = new SqlCommand(updateBooksSql, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@BookId", book.BookId);
                                            cmd.ExecuteNonQuery();
                                        }

                                        // Обновляем GroupIssuanceItems
                                        string updateItemsSql = @"
                                    UPDATE GroupIssuanceItems 
                                    SET ReturnedQuantity = Quantity
                                    WHERE ItemId = @ItemId";

                                        using (var cmd = new SqlCommand(updateItemsSql, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@ItemId", book.ItemId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }

                                // Обновляем статус групповой выдачи
                                string updateStatusSql = @"
                            UPDATE GroupBookIssuance 
                            SET ReturnDate = GETDATE(), Status = 'Возвращена'
                            WHERE GroupIssuanceId = @GroupIssuanceId";

                                using (var cmd = new SqlCommand(updateStatusSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@GroupIssuanceId", groupIssuanceId);
                                    cmd.ExecuteNonQuery();
                                }

                                transaction.Commit();

                                 CustomMessageBox.Show($"Все книги успешно возвращены!", "Успех",
                                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
                else
                {
                    // Частичный возврат - показываем диалог выбора
                    ShowPartialReturnDialog(groupIssuanceId, bookGroups);
                }

                // Обновляем список выдач
                if (_selectedReader != null)
                {
                    LoadActiveIssuances(_selectedReader.ReaderID, _selectedReader.TypeName);
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при возврате групповых книг: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void ShowPartialReturnDialog(int groupIssuanceId, ObservableCollection<GroupBookGroup> bookGroups)
        {
            try
            {
                var dialog = new Window
                {
                    Title = "Возврат книг группы",
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = "Выберите книги для возврата:",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                var listBox = new ListBox
                {
                    Height = 250,
                    SelectionMode = SelectionMode.Multiple,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                foreach (var bookGroup in bookGroups.Where(g => g.RemainingQuantity > 0))
                {
                    foreach (var book in bookGroup.Items.Where(i => i.RemainingQuantity > 0))
                    {
                        var checkBox = new CheckBox
                        {
                            Content = $"{bookGroup.BookTitle} (Инв. №: {book.InventaryNumber})",
                            Tag = book,
                            Margin = new Thickness(5)
                        };
                        listBox.Items.Add(checkBox);
                    }
                }

                panel.Children.Add(listBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var returnButton = new Button
                {
                    Content = "Возврат",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Style = (Style)Application.Current.Resources["SaveButtonStyle"]
                };

                var cancelButton = new Button
                {
                    Content = "Отмена",
                    Width = 100,
                    Height = 35,
                    Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
                    Background = (SolidColorBrush)Application.Current.Resources["LightGray"],
                    Foreground = (SolidColorBrush)Application.Current.Resources["TextColor"]
                };

                returnButton.Click += (s, args) =>
                {
                    try
                    {
                        int returnedCount = 0;
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            conn.Open();
                            using (var transaction = conn.BeginTransaction())
                            {
                                try
                                {
                                    foreach (CheckBox checkBox in listBox.Items)
                                    {
                                        if (checkBox.IsChecked == true && checkBox.Tag is GroupBookItem book)
                                        {
                                            // Обновляем количество книг и статус
                                            string updateBooksSql = @"
                                        UPDATE Books 
                                        SET 
                                            CopiesInUse = CopiesInUse - 1,
                                            AvailableCopies = AvailableCopies + 1,
                                            StatusBook = 1
                                        WHERE IDBook = @BookId";

                                            using (var cmd = new SqlCommand(updateBooksSql, conn, transaction))
                                            {
                                                cmd.Parameters.AddWithValue("@BookId", book.BookId);
                                                cmd.ExecuteNonQuery();
                                            }

                                            // Обновляем возвращенное количество
                                            string updateItemsSql = @"
                                        UPDATE GroupIssuanceItems 
                                        SET ReturnedQuantity = ReturnedQuantity + 1
                                        WHERE ItemId = @ItemId";

                                            using (var cmd = new SqlCommand(updateItemsSql, conn, transaction))
                                            {
                                                cmd.Parameters.AddWithValue("@ItemId", book.ItemId);
                                                cmd.ExecuteNonQuery();
                                            }

                                            returnedCount++;
                                        }
                                    }

                                    // Проверяем, все ли книги возвращены
                                    string checkSql = @"
                                SELECT COUNT(*) 
                                FROM GroupIssuanceItems 
                                WHERE GroupIssuanceId = @GroupIssuanceId 
                                  AND Quantity > ReturnedQuantity";

                                    using (var cmd = new SqlCommand(checkSql, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@GroupIssuanceId", groupIssuanceId);
                                        int remaining = (int)cmd.ExecuteScalar();

                                        if (remaining == 0)
                                        {
                                            string updateStatusSql = @"
                                        UPDATE GroupBookIssuance 
                                        SET ReturnDate = GETDATE(), Status = 'Возвращена'
                                        WHERE GroupIssuanceId = @GroupIssuanceId";

                                            using (var cmd2 = new SqlCommand(updateStatusSql, conn, transaction))
                                            {
                                                cmd2.Parameters.AddWithValue("@GroupIssuanceId", groupIssuanceId);
                                                cmd2.ExecuteNonQuery();
                                            }
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

                         CustomMessageBox.Show($"Возвращено книг: {returnedCount}", "Успех",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                        dialog.Close();

                        if (_selectedReader != null)
                        {
                            LoadActiveIssuances(_selectedReader.ReaderID, _selectedReader.TypeName);
                        }
                    }
                    catch (Exception ex)
                    {
                         CustomMessageBox.Show($"Ошибка при возврате: {ex.Message}", "Ошибка",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                };

                cancelButton.Click += (s, args) => dialog.Close();

                buttonPanel.Children.Add(returnButton);
                buttonPanel.Children.Add(cancelButton);
                panel.Children.Add(buttonPanel);

                dialog.Content = panel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при открытии диалога возврата: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var context = button?.DataContext;

            if (context == null) return;

            var type = context.GetType();
            var typeName = type.GetProperty("Type")?.GetValue(context)?.ToString();

            if (typeName == "individual")
            {
                var id = (int)type.GetProperty("Id")?.GetValue(context);
                ReturnIndividualBook(id);
            }
            else if (typeName == "group")
            {
                var id = (int)type.GetProperty("Id")?.GetValue(context);
                var bookGroups = type.GetProperty("BookGroups")?.GetValue(context) as ObservableCollection<GroupBookGroup>;
                if (bookGroups != null)
                {
                    ReturnGroupBooks(id, bookGroups, false);
                }
            }
        }

        private void ReturnAllButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var context = button?.DataContext;

            if (context == null) return;

            var type = context.GetType();
            var typeName = type.GetProperty("Type")?.GetValue(context)?.ToString();

            if (typeName == "group")
            {
                var id = (int)type.GetProperty("Id")?.GetValue(context);
                var bookGroups = type.GetProperty("BookGroups")?.GetValue(context) as ObservableCollection<GroupBookGroup>;
                if (bookGroups != null)
                {
                    ReturnGroupBooks(id, bookGroups, true);
                }
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReaders();
        }
    }

    public class GroupTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is string typeName && typeName == "group")
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}