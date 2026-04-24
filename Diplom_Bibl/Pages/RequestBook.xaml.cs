using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace Diplom_Bibl.Pages
{
    public partial class RequestBook : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private readonly int _currentUserId;
        private readonly int _currentUserTypeId;
        private ObservableCollection<BookItem> _availableBooks;
        private ObservableCollection<SelectedBookItem> _selectedBooks;

        // Класс для отображения заявки
        public class RequestItem
        {
            public int ID_Request { get; set; }
            public string RequestNumber { get; set; } = "";
            public DateTime RequestDate { get; set; }
            public int UserID { get; set; }
            public string UserName { get; set; } = "";
            public int? GroupID { get; set; }
            public string GroupCode { get; set; } = "";
            public string LessonName { get; set; } = "";
            public DateTime LessonDate { get; set; }
            public int StatusID { get; set; }
            public string StatusName { get; set; } = "";
            public int TotalBooks { get; set; }
            public DateTime? ExpirationDate { get; set; }

            public SolidColorBrush StatusColor
            {
                get
                {
                    switch (StatusName)
                    {
                        case "Новая": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF"));
                        case "В обработке": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                        case "Готова": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                        case "Выдана": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A2B8"));
                        case "Отменена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                        case "Просрочена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                        default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                    }
                }
            }

            public Visibility ConfirmVisible => (StatusName == "Новая" || StatusName == "В обработке") ? Visibility.Visible : Visibility.Collapsed;
            public Visibility CancelVisible => (StatusName != "Выдана" && StatusName != "Отменена" && StatusName != "Просрочена") ? Visibility.Visible : Visibility.Collapsed;
        }

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

        public class GroupItem
        {
            public int GroupID { get; set; }
            public string GroupCode { get; set; } = "";
        }

        public RequestBook(int userId, int userTypeId)
        {
            InitializeComponent();
            _currentUserId = userId;
            _currentUserTypeId = userTypeId;
            DpLessonDateSimple.SelectedDate = DateTime.Today;
            LoadUserData();
            LoadAvailableBooks();
        }

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
                CustomMessageBox.Show($"Ошибка загрузки данных пользователя: {ex.Message}", "Ошибка",
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
                        while (reader.Read())
                        {
                            groups.Add(new GroupItem
                            {
                                GroupID = Convert.ToInt32(reader["GroupID"]),
                                GroupCode = reader["GroupCode"].ToString()
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
                CustomMessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadAvailableBooks(string searchTerm = "")
        {
            try
            {
                string query = @"
                    SELECT 
                        MIN(b.IDBook) AS IDBook,
                        b.TitleBook, 
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        SUM(b.AvailableCopies) AS AvailableCopies
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    WHERE b.AvailableCopies > 0";

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query += " AND (b.TitleBook LIKE @Search OR a.SurnameAuthor LIKE @Search)";
                }

                query += " GROUP BY b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor";
                query += " ORDER BY b.TitleBook";

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
                                _availableBooks.Add(new BookItem
                                {
                                    IDBook = Convert.ToInt32(reader["IDBook"]),
                                    TitleBook = reader["TitleBook"].ToString(),
                                    AuthorName = reader["AuthorName"].ToString(),
                                    AvailableCopies = Convert.ToInt32(reader["AvailableCopies"])
                                });
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

        private string GenerateUniqueRequestNumber()
        {
            try
            {
                string datePart = DateTime.Now.ToString("yyyyMMdd");
                int sequence = 1;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Получаем максимальный номер за сегодня
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
                            // Извлекаем номер из строки (например, "ЗАЯВ-20260424-0001" -> "0001")
                            int lastDashIndex = lastNumber.LastIndexOf('-');
                            if (lastDashIndex >= 0 && lastDashIndex + 1 < lastNumber.Length)
                            {
                                string seqStr = lastNumber.Substring(lastDashIndex + 1);
                                if (int.TryParse(seqStr, out int lastSeq))
                                {
                                    sequence = lastSeq + 1;
                                }
                            }
                        }
                    }
                }

                // Формат: ЗАЯВ-20260424-0001 (всего 18 символов)
                // Подходит для nvarchar(20)
                return $"ЗАЯВ-{datePart}-{sequence:D4}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка генерации номера: {ex.Message}");
                // Резервный вариант - используем случайное число (всегда 4 цифры)
                return $"ЗАЯВ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1, 9999):D4}";
            }
        }
        private void TbBookSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadAvailableBooks(TbBookSearch.Text.Trim());
        }

        private void BtnSearchBook_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableBooks(TbBookSearch.Text.Trim());
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TbBookSearch.Clear();
            LoadAvailableBooks();
        }

        private void DgAvailableBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgAvailableBooks.SelectedItem is BookItem selectedBook)
            {
                AddBookToRequest(selectedBook);
            }
        }

        private void AddBookToRequest(BookItem book)
        {
            if (_selectedBooks == null)
            {
                _selectedBooks = new ObservableCollection<SelectedBookItem>();
            }

            SelectedBookItem existing = _selectedBooks.FirstOrDefault(b => b.BookTitle == book.TitleBook && b.AuthorName == book.AuthorName);
            if (existing != null)
            {
                if (existing.Quantity + 1 <= existing.MaxQuantity)
                {
                    existing.Quantity++;
                }
                else
                {
                    CustomMessageBox.Show($"Доступно только {existing.MaxQuantity} экземпляров этой книги.", "Предупреждение",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
            }
            else
            {
                _selectedBooks.Add(new SelectedBookItem
                {
                    IDBook = book.IDBook,
                    BookTitle = book.TitleBook,
                    AuthorName = book.AuthorName,
                    Quantity = 1,
                    MaxQuantity = book.AvailableCopies
                });
            }

            DgSelectedBooks.ItemsSource = _selectedBooks;
            UpdateTotalCount();
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

        private void UpdateTotalCount()
        {
            int total = _selectedBooks?.Sum(b => b.Quantity) ?? 0;
            TbTotalBooks.Text = total.ToString();
        }

        private void DpLessonDateSimple_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpLessonDateSimple.SelectedDate.HasValue && DpLessonDateSimple.SelectedDate.Value.Date < DateTime.Today)
            {
                CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDateSimple.SelectedDate = DateTime.Today;
            }
        }

        private void TbLessonTime_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9:]$");
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbLessonName.Text))
            {
                CustomMessageBox.Show("Введите название занятия/предмета.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbLessonName.Focus();
                return false;
            }

            if (!DpLessonDateSimple.SelectedDate.HasValue)
            {
                CustomMessageBox.Show("Выберите дату занятия.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDateSimple.Focus();
                return false;
            }

            if (DpLessonDateSimple.SelectedDate.Value.Date < DateTime.Today)
            {
                CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDateSimple.Focus();
                return false;
            }

            if (_currentUserTypeId == 3 && CbGroup.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите группу.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                CbGroup.Focus();
                return false;
            }

            if (_selectedBooks == null || _selectedBooks.Count == 0)
            {
                CustomMessageBox.Show("Добавьте хотя бы одну книгу в заявку.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async void BtnSubmitRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            BtnSubmitRequest.IsEnabled = false;

            try
            {
                string bookItems = string.Join(",", _selectedBooks.Select(b => $"{b.IDBook}:{b.Quantity}"));

                int? groupId = CbGroup.SelectedValue as int?;
                DateTime lessonDate = DpLessonDateSimple.SelectedDate.Value;
                string lessonTime = TbLessonTime.Text?.Trim();
                if (string.IsNullOrWhiteSpace(lessonTime))
                    lessonTime = null;

                string generatedNumber =  GenerateUniqueRequestNumber();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

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
                                cmd.Parameters.AddWithValue("@UserID", _currentUserId);
                                cmd.Parameters.AddWithValue("@UserTypeID", _currentUserTypeId);
                                cmd.Parameters.AddWithValue("@GroupID", groupId ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@LessonName", TbLessonName.Text.Trim());
                                cmd.Parameters.AddWithValue("@LessonDate", lessonDate);
                                cmd.Parameters.AddWithValue("@LessonTime", string.IsNullOrWhiteSpace(lessonTime) ? (object)DBNull.Value : lessonTime);

                                requestId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

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

                            string insertNotificationSql = @"
                                INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                                VALUES (@RequestID, 
                                    (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Новая заявка'),
                                    @Message, 0, GETDATE())";

                            using (var cmd = new SqlCommand(insertNotificationSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@RequestID", requestId);
                                cmd.Parameters.AddWithValue("@Message", $"Новая заявка №{generatedNumber} от пользователя ID {_currentUserId}");
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();

                            CustomMessageBox.Show($"Заявка №{generatedNumber} успешно создана!\n\nПосле обработки библиотекарем вы получите уведомление.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearForm();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                CustomMessageBox.Show("Произошла ошибка при генерации номера заявки. Попробуйте еще раз.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании заявки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                BtnSubmitRequest.IsEnabled = true;
            }
        }

        private void ClearForm()
        {
            TbLessonName.Clear();
            DpLessonDateSimple.SelectedDate = DateTime.Today;
            TbLessonTime.Text = "";
            CbGroup.SelectedIndex = -1;
            if (_selectedBooks != null)
            {
                _selectedBooks.Clear();
            }
            DgSelectedBooks.ItemsSource = null;
            UpdateTotalCount();
            LoadAvailableBooks();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvailableBooks();
            DpLessonDateSimple.SelectedDate = DateTime.Today;
        }
    }
}