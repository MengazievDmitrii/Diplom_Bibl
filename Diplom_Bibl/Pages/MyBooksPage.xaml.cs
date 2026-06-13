using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class MyBooksPage : Page
    {
        private readonly string _conn = DatabaseHelper.ConnectionString;
        private readonly ObservableCollection<IssuedBookItem> _myBooks;
        private readonly int _currentReaderId;

        public class IssuedBookItem
        {
            public int Id { get; set; }
            public string IssuanceType { get; set; }
            public string BookTitle { get; set; }
            public string BookAuthor { get; set; }
            public string InventaryNumber { get; set; }
            public string RecipientName { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ActualReturnDate { get; set; }
            public string Status { get; set; }
            public bool IsOverdue { get; set; }
            public int Quantity { get; set; } = 1;
            public string Notes { get; set; }

            public string StatusText
            {
                get
                {
                    if (ActualReturnDate.HasValue)
                        return "Возвращена";
                    else if (IsOverdue)
                        return "Просрочено";
                    else
                        return "В срок";
                }
            }

            public SolidColorBrush StatusColor
            {
                get
                {
                    if (ActualReturnDate.HasValue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                    else if (IsOverdue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    else
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }
            }
        }

        public MyBooksPage(int readerId)
        {
            InitializeComponent();
            _currentReaderId = readerId;
            _myBooks = new ObservableCollection<IssuedBookItem>();
            MyBooksDataGrid.ItemsSource = _myBooks;
            this.Loaded += MyBooksPage_Loaded;
        }

        private async void MyBooksPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                await LoadMyBooksAsync();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadMyBooksAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                Dispatcher.Invoke(() => _myBooks.Clear());

                int totalBooksCount = 0;
                int currentlyHeldCount = 0;
                int overdueCount = 0;
                int returnedCount = 0;

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();

                    // 1. Индивидуальные выдачи
                    LoadIndividualIssuances(conn, ref totalBooksCount, ref currentlyHeldCount, ref overdueCount, ref returnedCount);

                    // 2. Групповые выдачи (группировка по книге внутри выдачи)
                    LoadGroupIssuances(conn, ref totalBooksCount, ref currentlyHeldCount, ref overdueCount, ref returnedCount);

                    Dispatcher.Invoke(() =>
                    {
                        TotalIssuedCount.Text = totalBooksCount.ToString();
                        CurrentlyHeldCount.Text = currentlyHeldCount.ToString();
                        OverdueCount.Text = overdueCount.ToString();
                        ReturnedCount.Text = returnedCount.ToString();
                    });
                }
            });
        }

        private void LoadIndividualIssuances(SqlConnection conn, ref int totalBooksCount, ref int currentlyHeldCount, ref int overdueCount, ref int returnedCount)
        {
            string sql = @"
                SELECT 
                    bi.IssuanceId AS Id,
                    'individual' AS IssuanceType,
                    b.TitleBook,
                    ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS BookAuthor,
                    b.InventaryNumber,
                    CONCAT(r.LastName, ' ', r.FirstName, ' ', ISNULL(r.MiddleName, '')) AS RecipientName,
                    bi.IssuanceDate,
                    bi.PlannedReturnDate,
                    bi.ReturnDate,
                    bi.Status,
                    bi.Notes,
                    CASE 
                        WHEN bi.ReturnDate IS NULL AND bi.PlannedReturnDate < GETDATE() THEN 1 
                        ELSE 0 
                    END AS IsOverdue
                FROM BookIssuance bi
                INNER JOIN Books b ON bi.BookId = b.IDBook
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                INNER JOIN ReaderForm r ON bi.ReaderId = r.ReaderID
                WHERE bi.ReaderId = @ReaderId
                ORDER BY bi.IssuanceDate DESC";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var book = new IssuedBookItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            IssuanceType = reader["IssuanceType"].ToString(),
                            BookTitle = reader["TitleBook"].ToString(),
                            BookAuthor = reader["BookAuthor"].ToString(),
                            InventaryNumber = reader["InventaryNumber"].ToString(),
                            RecipientName = reader["RecipientName"].ToString(),
                            IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                            PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                            ActualReturnDate = reader["ReturnDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ReturnDate"]),
                            Status = reader["Status"].ToString(),
                            Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString(),
                            IsOverdue = Convert.ToBoolean(reader["IsOverdue"]),
                            Quantity = 1
                        };

                        Dispatcher.Invoke(() => _myBooks.Add(book));

                        totalBooksCount++;
                        if (book.ActualReturnDate.HasValue)
                            returnedCount++;
                        else if (book.IsOverdue)
                            overdueCount++;
                        else
                            currentlyHeldCount++;
                    }
                }
            }
        }

        /// <summary>
        /// Групповые выдачи - группировка по книге внутри выдачи
        /// </summary>
        private void LoadGroupIssuances(SqlConnection conn, ref int totalBooksCount, ref int currentlyHeldCount, ref int overdueCount, ref int returnedCount)
        {
            try
            {
                // Проверяем тип пользователя
                string userTypeSql = "SELECT TypeID FROM ReaderForm WHERE ReaderID = @ReaderId";
                int userTypeId = 1;
                using (var cmd = new SqlCommand(userTypeSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId);
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        userTypeId = Convert.ToInt32(result);
                }

                // Если не преподаватель, выходим
                if (userTypeId != 3)
                    return;

                string sql = @"
                    SELECT 
                        gi.GroupIssuanceId AS Id,
                        'group' AS IssuanceType,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS BookAuthor,
                        g.GroupCode AS RecipientName,
                        gi.IssuanceDate,
                        gi.PlannedReturnDate,
                        gi.ReturnDate,
                        gi.Status,
                        gi.Notes,
                        CASE 
                            WHEN gi.ReturnDate IS NULL AND gi.PlannedReturnDate < GETDATE() THEN 1 
                            ELSE 0 
                        END AS IsOverdue,
                        SUM(gii.Quantity) AS TotalQuantity
                    FROM GroupBookIssuance gi
                    INNER JOIN GroupIssuanceItems gii ON gi.GroupIssuanceId = gii.GroupIssuanceId
                    INNER JOIN Books b ON gii.BookId = b.IDBook
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    INNER JOIN Groups g ON gi.GroupId = g.GroupID
                    WHERE gi.TeacherId = @TeacherId
                    GROUP BY 
                        gi.GroupIssuanceId, b.TitleBook, a.SurnameAuthor, a.NameAuthor, a.PatronymicAuthor,
                        g.GroupCode, gi.IssuanceDate, gi.PlannedReturnDate, gi.ReturnDate, gi.Status, gi.Notes
                    ORDER BY gi.IssuanceDate DESC, b.TitleBook";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TeacherId", _currentReaderId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int quantity = reader["TotalQuantity"] != DBNull.Value ? Convert.ToInt32(reader["TotalQuantity"]) : 1;

                            var book = new IssuedBookItem
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                IssuanceType = reader["IssuanceType"].ToString(),
                                BookTitle = reader["TitleBook"].ToString(),
                                BookAuthor = reader["BookAuthor"].ToString(),
                                InventaryNumber = "", // Для групповой выдачи инвентарные номера не показываем
                                RecipientName = $"Группа {reader["RecipientName"].ToString()}",
                                IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                ActualReturnDate = reader["ReturnDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ReturnDate"]),
                                Status = reader["Status"].ToString(),
                                Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString(),
                                IsOverdue = Convert.ToBoolean(reader["IsOverdue"]),
                                Quantity = quantity
                            };

                            Dispatcher.Invoke(() => _myBooks.Add(book));

                            totalBooksCount += quantity;
                            if (book.ActualReturnDate.HasValue)
                                returnedCount += quantity;
                            else if (book.IsOverdue)
                                overdueCount += quantity;
                            else
                                currentlyHeldCount += quantity;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки групповых выдач: {ex.Message}");
            }
        }
    }
}