using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для MyBooksPage.xaml
    /// Отображает книги, выданные текущему пользователю
    /// </summary>
    public partial class MyBooksPage : Page
    {
        private readonly string _conn = DatabaseHelper.ConnectionString;
        private readonly ObservableCollection<IssuedBookItem> _myBooks;
        private readonly int _currentReaderId; // ID читателя (студента/препода)

        // Модель выданной книги — теперь с полными полями из vw_FundMovement
        public class IssuedBookItem
        {
            public int Id { get; set; }
            public string BookTitle { get; set; }
            public string BookAuthor { get; set; } 
            public string RecipientName { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ActualReturnDate { get; set; }
            public string Status { get; set; }
            public bool IsOverdue { get; set; }
            public string Notes { get; set; }

            // Текст статуса с учётом фактической даты и флага IsOverdue
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

            // Цвет статуса
            public SolidColorBrush StatusColor
            {
                get
                {
                    if (ActualReturnDate.HasValue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")); // зелёный: возвращена
                    else if (IsOverdue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")); // красный: просрочено
                    else
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")); // зелёный: в срок
                }
            }
        }

        public class ReaderInfo
        {
            public int ReaderId { get; set; }
            public string ReaderType { get; set; } // "Student" или "Teacher"
        }

        public MyBooksPage(int readerId) // Конструктор с ID читателя
        {
            InitializeComponent();
            _currentReaderId = readerId;
            _myBooks = new ObservableCollection<IssuedBookItem>();
            MyBooksDataGrid.ItemsSource = _myBooks;
            LoadMyBooks();
        }

        private ReaderInfo GetReaderType(int readerId)
        {
            string sql = @"
                SELECT TOP 1
                    CASE
                        WHEN [RecipientType] = 'Читатель' AND [GroupCode] IS NULL THEN 'Student'
                        WHEN [RecipientType] = 'Читатель' AND [GroupCode] IS NOT NULL THEN 'Teacher'
                        ELSE 'Unknown'
                    END AS ReaderType
                FROM [DiplomBibl].[dbo].[vw_FundMovement]
                WHERE [RecipientId] = @ReaderId;";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", readerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ReaderInfo
                            {
                                ReaderId = readerId,
                                ReaderType = reader["ReaderType"].ToString()
                            };
                        }
                    }
                }
            }
            return new ReaderInfo { ReaderId = readerId, ReaderType = "Unknown" };
        }

        private void LoadMyBooks()
        {
            try
            {
                _myBooks.Clear();
                var readerInfo = GetReaderType(_currentReaderId);

                string sql;
                if (readerInfo.ReaderType == "Teacher")
                {
                    sql = @"
                SELECT TOP (1000)
                    [IssuanceType],
                    [Id],
                    [IssuanceDate],
                    [PlannedReturnDate],
                    [ReturnDate],
                    [Status],
                    [RecipientId],
                    [RecipientName],
                    [BookId],
                    [BookTitle],
                    COALESCE(a.SurnameAuthor, '') + ' ' + COALESCE(a.NameAuthor, '') + ' ' + COALESCE(a.PatronymicAuthor, '') AS BookAuthor,
                    [IsOverdue],
                    [Notes],
                    [TeacherName]
                FROM [DiplomBibl].[dbo].[vw_FundMovement] v
                LEFT JOIN Books b ON v.BookId = b.IDBook
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                WHERE ([IssuanceType] = 'individual' AND [RecipientId] = @ReaderId)
                   OR ([IssuanceType] = 'group' AND [TeacherName] = (
                       SELECT TOP 1 [RecipientName]
                       FROM [DiplomBibl].[dbo].[vw_FundMovement]
                       WHERE [RecipientId] = @ReaderId
                   ))
                ORDER BY [IssuanceDate];";
                }
                else
                {
                    sql = @"
                SELECT TOP (1000)
                    [IssuanceType],
                    [Id],
                    [IssuanceDate],
                    [PlannedReturnDate],
                    [ReturnDate],
                    [Status],
                    [RecipientId],
                    [RecipientName],
                    [BookId],
                    [BookTitle],
                    COALESCE(a.SurnameAuthor, '') + ' ' + COALESCE(a.NameAuthor, '') + ' ' + COALESCE(a.PatronymicAuthor, '') AS BookAuthor,
                    [IsOverdue],
                    [Notes]
                FROM [DiplomBibl].[dbo].[vw_FundMovement] v
                LEFT JOIN Books b ON v.BookId = b.IDBook
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                WHERE [IssuanceType] = 'individual' AND [RecipientId] = @ReaderId
                ORDER BY [IssuanceDate];";
                }

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            int overdueCount = 0;
                            int heldCount = 0;
                            int returnedCount = 0;

                            while (reader.Read())
                            {
                                var book = new IssuedBookItem
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    BookTitle = reader["BookTitle"].ToString(),
                                    BookAuthor = reader["BookAuthor"].ToString(),  // <-- заполняем автора
                                    RecipientName = reader["RecipientName"].ToString(),
                                    IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                                    PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                                    ActualReturnDate = reader["ReturnDate"] == DBNull.Value
                                        ? (DateTime?)null
                                        : Convert.ToDateTime(reader["ReturnDate"]),
                                    Status = reader["Status"].ToString(),
                                    IsOverdue = Convert.ToBoolean(reader["IsOverdue"]),
                                    Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString()
                                };
                                _myBooks.Add(book);

                                // Статистика
                                if (book.ActualReturnDate.HasValue)
                                    returnedCount++;
                                else if (book.IsOverdue)
                                    overdueCount++;
                                else
                                    heldCount++;
                            }

                            TotalIssuedCount.Text = _myBooks.Count.ToString();
                            CurrentlyHeldCount.Text = heldCount.ToString();
                            OverdueCount.Text = overdueCount.ToString();
                        }
                    }
                }
            }
            catch (SqlException sqlException)
            {
                CustomMessageBox.Show(sqlException.Message, "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
    }

}
