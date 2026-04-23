using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class RequestBook : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private readonly int _currentUserId;
        private readonly int _currentUserTypeId;
        private ObservableCollection<BookItem> _availableBooks;
        private ObservableCollection<SelectedBookItem> _selectedBooks;

        // Исправленный класс RequestItem с правильными статусами
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
            BookItem selectedBook = DgAvailableBooks.SelectedItem as BookItem;
            if (selectedBook != null)
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
            Button button = sender as Button;
            if (button != null)
            {
                SelectedBookItem selectedBook = button.DataContext as SelectedBookItem;
                if (selectedBook != null && _selectedBooks != null)
                {
                    _selectedBooks.Remove(selectedBook);
                    DgSelectedBooks.ItemsSource = _selectedBooks;
                    UpdateTotalCount();
                }
            }
        }

        private void Quantity_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                SelectedBookItem book = textBox.DataContext as SelectedBookItem;
                if (book != null)
                {
                    if (book.Quantity < 1)
                        book.Quantity = 1;
                    if (book.Quantity > book.MaxQuantity)
                    {
                        CustomMessageBox.Show($"Доступно только {book.MaxQuantity} экземпляров этой книги.", "Предупреждение",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        book.Quantity = book.MaxQuantity;
                    }
                    UpdateTotalCount();
                    DgSelectedBooks.Items.Refresh();
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void TbLessonTime_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9:]$");
        }

        private void UpdateTotalCount()
        {
            int total = 0;
            if (_selectedBooks != null)
            {
                total = _selectedBooks.Sum(b => b.Quantity);
            }
            TbTotalBooks.Text = total.ToString();
        }

        private void DpLessonDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpLessonDate.SelectedDate.HasValue && DpLessonDate.SelectedDate.Value.Date < DateTime.Today)
            {
                CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDate.SelectedDate = DateTime.Today;
            }
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

            if (!DpLessonDate.SelectedDate.HasValue)
            {
                CustomMessageBox.Show("Выберите дату занятия.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDate.Focus();
                return false;
            }

            if (DpLessonDate.SelectedDate.Value.Date < DateTime.Today)
            {
                CustomMessageBox.Show("Дата занятия не может быть раньше сегодняшнего дня.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                DpLessonDate.Focus();
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

            try
            {
                string bookItems = string.Join(",", _selectedBooks.Select(b => $"{b.IDBook}:{b.Quantity}"));

                int? groupId = null;
                if (CbGroup.SelectedValue != null)
                {
                    groupId = (int)CbGroup.SelectedValue;
                }
                DateTime lessonDate = DpLessonDate.SelectedDate.Value;
                string lessonTime = TbLessonTime.Text?.Trim();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("sp_CreateBookRequest", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@UserID", _currentUserId);
                        cmd.Parameters.AddWithValue("@UserTypeID", _currentUserTypeId);
                        cmd.Parameters.AddWithValue("@GroupID", groupId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@LessonName", TbLessonName.Text.Trim());
                        cmd.Parameters.AddWithValue("@LessonDate", lessonDate);
                        cmd.Parameters.AddWithValue("@LessonTime", string.IsNullOrWhiteSpace(lessonTime) ? (object)DBNull.Value : lessonTime);
                        cmd.Parameters.AddWithValue("@BookItems", bookItems);

                        var outputNumber = cmd.Parameters.Add("@RequestNumber", System.Data.SqlDbType.NVarChar, 20);
                        outputNumber.Direction = System.Data.ParameterDirection.Output;

                        var outputId = cmd.Parameters.Add("@RequestID", System.Data.SqlDbType.Int);
                        outputId.Direction = System.Data.ParameterDirection.Output;

                        await cmd.ExecuteNonQueryAsync();

                        string requestNumber = outputNumber.Value?.ToString() ?? "";
                        int requestId = (outputId.Value != DBNull.Value) ? Convert.ToInt32(outputId.Value) : 0;

                        CustomMessageBox.Show($"Заявка №{requestNumber} успешно создана!\n\nПосле обработки библиотекарем вы получите уведомление.",
                            "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                        ClearForm();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании заявки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            TbLessonName.Clear();
            DpLessonDate.SelectedDate = DateTime.Today;
            TbLessonTime.Clear();
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
        }
    }
}