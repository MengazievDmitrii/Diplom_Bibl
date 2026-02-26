using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddBook.xaml
    /// </summary>
    public partial class AddBook : Page
    {

        private readonly string _connectionString =
            "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private int? _currentBookId = null;
        private System.Collections.ObjectModel.ObservableCollection<dynamic> _allBooks;

        public AddBook()
        {
            InitializeComponent();
            LoadAuthors();
            LoadViewBooks();
            LoadPublishers();
            LoadGenres();
            LoadStatuses();
            LoadReceipts();
            LoadBooks();
        }
        private void LoadAuthors()
        {
            try
            {
                const string sql = "SELECT ID_Autor, SurnameAuthor, NameAuthor, PatronymicAuthor FROM Authors ORDER BY SurnameAuthor";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int ID_Autor = reader["ID_Autor"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["ID_Autor"]);
                            string SurnameAuthor = reader["SurnameAuthor"]?.ToString() ?? "";
                            string NameAuthor = reader["NameAuthor"]?.ToString() ?? "";
                            string PatronymicAuthor = reader["PatronymicAuthor"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_Autor,
                                DisplayAuthors = $"{SurnameAuthor} {NameAuthor} {PatronymicAuthor}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbAuthor.ItemsSource = items;
                        CbAuthor.DisplayMemberPath = "DisplayAuthors";
                        CbAuthor.SelectedValuePath = "ID";
                        LvBooks.DisplayMemberPath = "DisplayAuthors";

                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void LoadViewBooks()
        {
            try
            {
                const string sql = "SELECT ID_View_Book, View_Book FROM ViewBook ORDER BY View_Book";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int ID_View_Book = reader["ID_View_Book"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["ID_View_Book"]);
                            string View_Book = reader["View_Book"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = ID_View_Book,
                                DisplayViewBook = $"{View_Book}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbViewBook.ItemsSource = items;
                        CbViewBook.DisplayMemberPath = "DisplayViewBook";
                        CbViewBook.SelectedValuePath = "ID";

                    }
                }
            }


            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void LoadPublishers()
        {
            try
            {
                const string sql = "SELECT IDPublishingHouse, TitlePublishingHouse FROM PublishingHouse ORDER BY TitlePublishingHouse";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int IDPublishingHouse = reader["IDPublishingHouse"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["IDPublishingHouse"]);
                            string TitlePublishingHouse = reader["TitlePublishingHouse"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = IDPublishingHouse,
                                DisplayPH = $"{TitlePublishingHouse}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbPublisher.ItemsSource = items;
                        CbPublisher.DisplayMemberPath = "DisplayPH";
                        CbPublisher.SelectedValuePath = "ID";

                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadGenres()
        {
            try
            {
                const string sql = "SELECT ID_Gener, TitleGener  FROM GenersBook ORDER BY TitleGener";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int ID_Gener = reader["ID_Gener"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["ID_Gener"]);
                            string TitleGener = reader["TitleGener"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = ID_Gener,
                                DisplayGen = $"{TitleGener}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbGenre.ItemsSource = items;
                        CbGenre.DisplayMemberPath = "DisplayGen";
                        CbGenre.SelectedValuePath = "ID";

                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadStatuses()
        {
            try
            {
                const string sql = "SELECT ID_Status, Title_Status FROM StatusBook ORDER BY Title_Status";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int ID_Status = reader["ID_Status"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["ID_Status"]);
                            string Title_Status = reader["Title_Status"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = ID_Status,
                                DisplayStatus = $"{Title_Status}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbStatus.ItemsSource = items;
                        CbStatus.DisplayMemberPath = "DisplayStatus";
                        CbStatus.SelectedValuePath = "ID";

                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadReceipts()
        {
            try
            {
                const string sql = "SELECT IDReceipt, ReceiptNumber, ReceiptDate FROM ReceiptBooks ORDER BY ReceiptNumber";
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int IDReceipt = reader["IDReceipt"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["IDReceipt"]);
                            string ReceiptNumber = reader["ReceiptNumber"]?.ToString() ?? "";
                            string ReceiptDate = reader["ReceiptDate"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = IDReceipt,
                                DisplayReceipt = $"{ReceiptNumber} {ReceiptDate}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        CbReceipt.ItemsSource = items;
                        CbReceipt.DisplayMemberPath = "DisplayReceipt";
                        CbReceipt.SelectedValuePath = "ID";

                    }
                }
            }




            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadBooks()
        {
            try
            {
                const string query = @"
                SELECT 
                    b.IDBook,
                    b.InventaryNumber AS InventoryNumber,
                            b.TitleBook AS Title,
                    b.YearPublishing AS Year,
                    b.Author AS AuthorId,
                    ISNULL(a.NameAuthor, '') AS AuthorName,
                    ISNULL(a.SurnameAuthor, '') AS AuthorSurname,
                    ISNULL(a.PatronymicAuthor, '') AS AuthorPatronymic,
                    b.Gener AS GenreId,
                    ISNULL(g.TitleGener, '') AS GenreName,
                    b.StatusBook AS StatusId,
                    ISNULL(s.Title_Status, '') AS StatusTitle,
                    b.ID_View_Book AS ViewId,
                    ISNULL(v.View_Book, '') AS ViewName,
                    b.PublishingHouse AS PublisherId,
                    ISNULL(p.TitlePublishingHouse, '') AS PublisherName,
                    b.IDReceip AS ReceiptId,
                    ISNULL(r.ReceiptNumber, '') AS ReceiptNumber,
                    b.Anotation, b.Price
                FROM Books b
                        LEFT JOIN Authors a ON b.Author = a.ID_Autor
                        LEFT JOIN GenersBook g ON b.Gener = g.ID_Gener
                        LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                        LEFT JOIN ViewBook v ON b.ID_View_Book = v.ID_View_Book
                        LEFT JOIN PublishingHouse p ON b.PublishingHouse = p.IDPublishingHouse
                        LEFT JOIN ReceiptBooks r ON b.IDReceip = r.IDReceipt
                        ORDER BY b.TitleBook; ";

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var books = new System.Collections.ObjectModel.ObservableCollection<dynamic>();

                        while (reader.Read())
                        {
                            books.Add(new
                            {
                                IDBook = Convert.ToInt32(reader["IDBook"]),
                                InventoryNumber = reader["InventoryNumber"].ToString(),
                                Title = reader["Title"].ToString(),
                                Year = Convert.ToInt32(reader["Year"]),
                                AuthorId = reader["AuthorId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AuthorId"]),
                                GenreId = reader["GenreId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["GenreId"]),
                                StatusId = reader["StatusId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["StatusId"]),
                                ViewId = Convert.ToInt32(reader["ViewId"]),
                                PublisherId = reader["PublisherId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["PublisherId"]),
                                ReceiptId = reader["ReceiptId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ReceiptId"]),
                                AuthorName = $"{reader["AuthorSurname"]} {reader["AuthorName"]} {reader["AuthorPatronymic"]}".Trim(),
                                GenreName = reader["GenreName"].ToString(),
                                StatusTitle = reader["StatusTitle"].ToString(),
                                ViewName = reader["ViewName"].ToString(),
                                PublisherName = reader["PublisherName"].ToString(),
                                Price = reader["Price"]?.ToString(),
                                Anotation = reader["Anotation"].ToString().TrimEnd()
                            });
                        }

                        _allBooks = books;
                        ApplyBookFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки книг:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(TbInventoryNumber.Text) ||
                string.IsNullOrWhiteSpace(TbTitle.Text) ||
                CbAuthor.SelectedValue == null ||
                CbViewBook.SelectedValue == null ||
                !int.TryParse(TbYear.Text, out int year))
            {
                MessageBox.Show("Заполните обязательные поля (*).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            const string sql = @"
        INSERT INTO Books (
            InventaryNumber, TitleBook, Author, PublishingHouse, YearPublishing,
            Gener, StatusBook, Anotation, IDReceip, ID_View_Book, Price
        ) VALUES (
            @Inv, @Title, @Author, @Pub, @Year,
            @Genre, @Status, @Annot, @Receipt, @View, @Price
        )";
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Inv", TbInventoryNumber.Text.Trim());
                        cmd.Parameters.AddWithValue("@Title", TbTitle.Text.Trim());
                        cmd.Parameters.AddWithValue("@Author", CbAuthor.SelectedValue);
                        cmd.Parameters.AddWithValue("@Pub", CbPublisher.SelectedValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@Genre", CbGenre.SelectedValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", CbStatus.SelectedValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Annot", TbAnnotation.Text.Trim() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Receipt", CbReceipt.SelectedValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@View", CbViewBook.SelectedValue);
                        cmd.Parameters.AddWithValue("@Price", TbPrice.Text);

                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Книга сохранена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearForm();
                        LoadBooks(); // Обновить список
                    }
                }
            }
            catch (SqlException ex) { MessageBox.Show($"Ошибка обновления книги: \n{ex}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void ClearForm()
        {
            TbInventoryNumber.Clear();
            TbTitle.Clear();
            TbYear.Clear();
            TbAnnotation.Clear();
            TbPrice.Clear();

            // Сбрасываем выбор в ComboBox (устанавливаем null или -1)
            CbAuthor.SelectedIndex = -1;
            CbViewBook.SelectedIndex = -1;
            CbPublisher.SelectedIndex = -1;
            CbGenre.SelectedIndex = -1;
            CbStatus.SelectedIndex = -1;
            CbReceipt.SelectedIndex = -1;

            // Скрываем кнопки редактирования/удаления
            _currentBookId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }
        private void ApplyBookFilter(string searchTerm)
        {
            if (_allBooks == null)
            {
                LvBooks.ItemsSource = null;
                return;
            }

            var filtered = _allBooks.Where(book =>
            {
                string title = book.Title?.ToString() ?? "";
                string inv = book.InventoryNumber?.ToString() ?? "";
                string author = book.AuthorName?.ToString() ?? "";

                return string.IsNullOrEmpty(searchTerm) ||
                       title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       inv.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       author.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();

            LvBooks.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<dynamic>(filtered);
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyBookFilter(SearchBox.Text.Trim());
        }
        private void LvBooks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            dynamic selectedItem = LvBooks.SelectedItem;
            if (selectedItem == null) return;

            try
            {
                _currentBookId = selectedItem.IDBook;

                // Основные поля
                TbInventoryNumber.Text = selectedItem.InventoryNumber ?? "";
                TbTitle.Text = selectedItem.Title ?? "";
                TbYear.Text = selectedItem.Year?.ToString() ?? "";
                TbAnnotation.Text = selectedItem.Anotation ?? "";
                TbPrice.Text = selectedItem.Price ?? "";

                // ComboBox: устанавливаем SelectedValue = ID
                SetComboBoxSelection(CbAuthor, selectedItem.AuthorId);
                SetComboBoxSelection(CbViewBook, selectedItem.ViewId);
                SetComboBoxSelection(CbPublisher, selectedItem.PublisherId);
                SetComboBoxSelection(CbGenre, selectedItem.GenreId);
                SetComboBoxSelection(CbStatus, selectedItem.StatusId);
                SetComboBoxSelection(CbReceipt, selectedItem.ReceiptId);

                // Переключаем кнопки
                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
                BtnGenerateAct.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки книги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetComboBoxSelection(ComboBox cb, object idValue)
        {
            if (idValue == null || idValue == DBNull.Value)
            {
                cb.SelectedIndex = -1;
                return;
            }

            // Если значение — int, но в ComboBox хранится как object — приводим
            if (int.TryParse(idValue.ToString(), out int id))
            {
                cb.SelectedValue = id;
            }
            else
            {
                cb.SelectedIndex = -1;
            }
        }
        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.GoBack();
            }
            else
            {
                // На случай, если страница открыта не в Frame
                Window.GetWindow(this)?.Close();
            }
        }
        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной книги для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Валидация
            string invNumber = TbInventoryNumber.Text?.Trim();
            string title = TbTitle.Text?.Trim();
            string price = TbPrice.Text?.Trim();
            if (string.IsNullOrEmpty(invNumber) || string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Заполните обязательные поля: инвентарный номер и название.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TbYear.Text, out int year))
            {
                MessageBox.Show("Год издания должен быть числом.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            const string updateQuery = @"
        UPDATE Books 
        SET 
            InventaryNumber = @Inv,
            TitleBook = @Title,
            Author = @Author,
            PublishingHouse = @Pub,
            YearPublishing = @Year,
            Gener = @Genre,
            StatusBook = @Status,
            Anotation = @Annot,
            IDReceip = @Receipt,
            ID_View_Book = @View,
Price = @Price
        WHERE IDBook = @Id;";

            //try
            //{
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", invNumber);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Author", CbAuthor.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Pub", CbPublisher.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Year", year);
                    cmd.Parameters.AddWithValue("@Genre", CbGenre.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", CbStatus.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Annot", TbAnnotation.Text?.Trim() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Receipt", CbReceipt.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@View", CbViewBook.SelectedValue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Price", price);
                    cmd.Parameters.AddWithValue("@Id", _currentBookId.Value);

                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        MessageBox.Show("Книга успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadBooks();   // Обновляем список
                        ClearForm();   // Сбрасываем форму
                    }
                    else
                    {
                        MessageBox.Show("Книга не найдена (возможно, удалена).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ClearForm();
                    }
                }
            }
        }
        //catch (Exception ex)
        //{
        //    MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //}
    
        
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной книги для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить эту книгу?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            const string deleteQuery = "DELETE FROM Books WHERE IDBook = @Id;";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentBookId.Value);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            MessageBox.Show("Книга удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadBooks();
                            ClearForm();
                        }
                        else
                        {
                            MessageBox.Show("Книга уже удалена.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearForm();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show("Невозможно удалить книгу: на неё ссылаются другие записи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddAuthor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddAuthor());
        }

        private void BtnAddViewBook_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddViewBook());
        }

        private void BtnAddPublisher_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddPublishingHouse());
        }

        private void BtnAddGenre_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddGener());
        }

        private void BtnAddStatus_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddStatusBook());
        }

        private void BtnAddReceipt_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdmissionBook());
        }

        // Аналогично для других: BtnAddViewBook_Click → new AddViewBook(), и т.д.

        private void BtnGenerateAct_Click(object sender, RoutedEventArgs e)
        {
            if (CbReceipt.SelectedValue is int receiptId)
            {
                try
                {
                    byte[] pdfBytes = ReceiptActPdfService.GenerateReceiptActPdf(receiptId); // ✅ Только 1 аргумент
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"Акт_поступления_{receiptId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                    string fullPath = Path.Combine(desktop, fileName);
                    File.WriteAllBytes(fullPath, pdfBytes);
                    MessageBox.Show($"PDF сохранён:\n{fullPath}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка генерации PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите акт для формирования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

