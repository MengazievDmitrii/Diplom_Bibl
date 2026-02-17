using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom_Bibl.Pages

{
    /// <summary>
    /// Логика взаимодействия для LibraryFundPage.xaml
    /// </summary>
    public partial class LibraryFundPage : Page
    {
        private readonly string _conn = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private ObservableCollection<BookItem> _allBooks;
        private ObservableCollection<BookItem> _filteredBooks;

        public LibraryFundPage()
        {
            InitializeComponent();
            LoadBooks();
        }

        private void AdmissionBook_Click(object sender, RoutedEventArgs e)
        {
            Pages.AdmissionBook admissionBook = new Pages.AdmissionBook();
            NavigationService.Navigate(admissionBook);
        }

        private void AddBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddBook());
        }

        private void DisposalBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DisposalAct());
        }
        private class BookItem
        {
            public int IDBook { get; set; }
            public string InventaryNumber { get; set; }
            public string TitleBook { get; set; }
            public string AuthorName { get; set; }
            public int YearPublishing { get; set; }
            public string PublishingHouseName { get; set; }
            public string GenreName { get; set; }
            public string StatusName { get; set; }
        }

        private void LoadBooks()
        {
            const string sql = @"
                SELECT 
                    b.IDBook,
                    b.InventaryNumber,
                    b.TitleBook,
                    b.YearPublishing,
                    ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                    ISNULL(p.TitlePublishingHouse, '') AS PublishingHouseName,
                    ISNULL(g.TitleGener, '') AS GenreName,
                    ISNULL(s.Title_Status, '') AS StatusName
                FROM Books b
                LEFT JOIN Authors a ON b.Author = a.ID_Autor
                LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status";

            var list = new ObservableCollection<BookItem>();

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new BookItem
                        {
                            IDBook = Convert.ToInt32(reader["IDBook"]),
                            InventaryNumber = reader["InventaryNumber"].ToString(),
                            TitleBook = reader["TitleBook"].ToString(),
                            AuthorName = reader["AuthorName"].ToString(),
                            YearPublishing = Convert.ToInt32(reader["YearPublishing"]),
                            PublishingHouseName = reader["PublishingHouseName"].ToString(),
                            GenreName = reader["GenreName"].ToString(),
                            StatusName = reader["StatusName"].ToString()
                        });
                    }
                }
            }

            _allBooks = list;
            _filteredBooks = new ObservableCollection<BookItem>(_allBooks);
            DgBooks.ItemsSource = _filteredBooks;
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = TbSearch.Text?.Trim().ToLower() ?? "";

            var filtered = _allBooks.Where(b =>
                b.InventaryNumber.ToLower().Contains(term) ||
                b.TitleBook.ToLower().Contains(term) ||
                b.AuthorName.ToLower().Contains(term) ||
                b.PublishingHouseName.ToLower().Contains(term) ||
                b.GenreName.ToLower().Contains(term) ||
                b.StatusName.ToLower().Contains(term) ||
                b.YearPublishing.ToString().Contains(term)
            ).ToList();

            _filteredBooks.Clear();
            foreach (var item in filtered)
                _filteredBooks.Add(item);
        }
    }

}

