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

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Класс для причины списания книги
    /// </summary>
    public class ReasonDisposalBook
    {
        public int ID_Reason { get; set; }
        public string ReasonName { get; set; }
    }

    /// <summary>
    /// Класс для результатов поиска книг
    /// </summary>
    public class BookSearchResult
    {
        public int IDBook { get; set; }
        public string InventoryNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string PublisherYear { get; set; } = "";
        public decimal Price { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
    }

    /// <summary>
    /// Страница формирования акта списания книг
    /// </summary>
    public partial class DisposalAct : Page
    {
        #region Поля и свойства

        private readonly string _conn = DatabaseHelper.ConnectionString;
        private readonly ObservableCollection<BookItem> _items = new ObservableCollection<BookItem>();
        private List<BookSearchResult> _searchResults = new List<BookSearchResult>();
        private BookSearchResult _currentSelectedBook = null; // Храним текущую выбранную книгу

        private int _nextRowNum = 1;        // Следующий порядковый номер для новой книги
        private int? _currentWriteOffId = null; // ID текущего акта списания

        #endregion

        #region Вспомогательные классы

        /// <summary>
        /// Класс для отображения книги в таблице акта
        /// </summary>
        private class BookItem
        {
            public int RowNum { get; set; }             // Порядковый номер
            public string InventoryNumber { get; set; } // Инвентарный номер
            public string Author { get; set; }          // Автор
            public string Title { get; set; }           // Название
            public string PublishingHouse { get; set; } // Издательство
            public int Year { get; set; }               // Год издания
            public int PriceRub { get; set; }           // Цена рубли
            public int PriceKop { get; set; }           // Цена копейки
            public int Quantity { get; set; }           // Количество
            public decimal TotalPrice => (PriceRub + PriceKop / 100m) * Quantity; // Общая сумма
            public string PlaceYear => $"{PublishingHouse}, {Year}"; // Издательство и год
        }

        /// <summary>
        /// Результат проверки статуса книги
        /// </summary>
        private class BookStatusCheckResult
        {
            public bool CanBeDisposed { get; set; }     // Можно ли списать
            public string Message { get; set; }         // Сообщение
            public bool IsIssued { get; set; }          // Выдана ли книга
            public string IssuedInfo { get; set; }      // Информация о выдаче
        }

        #endregion

        #region Конструктор и инициализация

        public DisposalAct()
        {
            InitializeComponent();
            DgItems.ItemsSource = _items;               // Привязываем список книг к DataGrid
            LoadReasons();                              // Загружаем причины списания
            DpDate.SelectedDate = DateTime.Today;       // Устанавливаем текущую дату

            // Генерируем следующий номер акта
            string nextNumber = GetNextDisposalNumber();
            TbActNumber.Text = nextNumber;
        }

        /// <summary>
        /// Загрузка причин списания из базы данных
        /// </summary>
        private void LoadReasons()
        {
            try
            {
                const string sql = "SELECT ID_Reason, ReasonName FROM ReasonDisposalBook ORDER BY ReasonName";
                var reasons = new ObservableCollection<ReasonDisposalBook>();

                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reasons.Add(new ReasonDisposalBook
                            {
                                ID_Reason = Convert.ToInt32(reader["ID_Reason"]),
                                ReasonName = reader["ReasonName"].ToString()
                            });
                        }
                    }
                }

                CbReason.ItemsSource = reasons;
                CbReason.DisplayMemberPath = "ReasonName";
                CbReason.SelectedValuePath = "ID_Reason";
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки причин: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение следующего номера акта списания
        /// </summary>
        private string GetNextDisposalNumber()
        {
            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(MAX(CAST(SUBSTRING(DisposalNumber, 4, 4) AS INT)), 0) FROM DisposalBooks",
                    conn))
                {
                    int lastNum = Convert.ToInt32(cmd.ExecuteScalar());
                    return $"СП-{(lastNum + 1):D4}";
                }
            }
        }

        #endregion

        #region Поиск книг с выпадающим списком

        /// <summary>
        /// Обработчик изменения текста в поле поиска (асинхронный поиск при вводе)
        /// </summary>
        private async void TbSearchBook_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TbSearchBook.Text.Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                LbSearchResults.Visibility = Visibility.Collapsed;
                PnBookDetails.Visibility = Visibility.Collapsed;
                return;
            }

            if (searchText.Length < 2) return;

            await SearchBooksAsync(searchText);
        }

        /// <summary>
        /// Асинхронный поиск книг по инвентарному номеру, названию или автору
        /// </summary>
        private async Task SearchBooksAsync(string searchText)
        {
            try
            {
                string query = @"
                    SELECT TOP 20 
                        b.IDBook,
                        b.InventaryNumber,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        ISNULL(ph.TitlePublishingHouse, '') + ', ' + CAST(b.YearPublishing AS NVARCHAR(4)) AS PublisherYear,
                        b.Price,
                        b.TotalCopies,
                        b.AvailableCopies
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN PublishingHouse ph ON b.PublishingHouse = ph.IDPublishingHouse
                    WHERE b.InventaryNumber LIKE @Search 
                       OR b.TitleBook LIKE @Search
                       OR a.SurnameAuthor LIKE @Search
                    ORDER BY b.TitleBook";

                _searchResults.Clear();

                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                _searchResults.Add(new BookSearchResult
                                {
                                    IDBook = reader.GetInt32(0),
                                    InventoryNumber = reader.GetString(1),
                                    Title = reader.GetString(2),
                                    AuthorName = reader.GetString(3),
                                    PublisherYear = reader.GetString(4),
                                    Price = reader.GetDecimal(5),
                                    TotalCopies = reader.GetInt32(6),
                                    AvailableCopies = reader.GetInt32(7)
                                });
                            }
                        }
                    }
                }

                // Показываем результаты поиска в выпадающем списке
                Dispatcher.Invoke(() =>
                {
                    if (_searchResults.Any())
                    {
                        LbSearchResults.ItemsSource = _searchResults;
                        LbSearchResults.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        LbSearchResults.Visibility = Visibility.Collapsed;
                        PnBookDetails.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик выбора книги из выпадающего списка
        /// </summary>
        private void LbSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbSearchResults.SelectedItem is BookSearchResult selected)
            {
                TbSearchBook.Text = selected.InventoryNumber;
                LbSearchResults.Visibility = Visibility.Collapsed;

                // Загружаем детали книги для предпросмотра
                LoadBookDetails(selected);
            }
        }

        /// <summary>
        /// Загрузка деталей выбранной книги и проверка возможности списания
        /// </summary>
        private async void LoadBookDetails(BookSearchResult book)
        {
            try
            {
                // Проверка статуса книги (выдана, уже списана и т.д.)
                var bookCheckResult = await CheckBookStatusAsync(book.InventoryNumber);
                if (!bookCheckResult.CanBeDisposed)
                {
                    if (bookCheckResult.Message.Contains("находится на руках"))
                    {
                        string issuedInfo = GetIssuedInfo(book.InventoryNumber);
                        CustomMessageBox.Show($"{bookCheckResult.Message}\n\n{issuedInfo}",
                            "Невозможно списать", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                    else
                    {
                        CustomMessageBox.Show(bookCheckResult.Message);
                    }
                    TbSearchBook.Clear();
                    PnBookDetails.Visibility = Visibility.Collapsed;
                    return;
                }

                // Сохраняем текущую книгу
                _currentSelectedBook = book;

                // Заполняем панель деталей
                LbInventory.Text = book.InventoryNumber;
                LbTitle.Text = $"{book.AuthorName} — {book.Title}";
                LbPubYear.Text = book.PublisherYear;
                LbPrice.Text = $"{book.Price:F2} руб.";

                PnBookDetails.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Проверка статуса книги (можно ли списать)
        /// </summary>
        private async Task<BookStatusCheckResult> CheckBookStatusAsync(string inventoryNumber)
        {
            const string sql = @"
                SELECT 
                    b.ID_Disposal,
                    s.Title_Status AS StatusName,
                    b.StatusBook,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL OR gi.GroupIssuanceId IS NOT NULL THEN 1 
                        ELSE 0 
                    END AS IsIssued
                FROM Books b
                LEFT JOIN StatusBook s ON b.StatusBook = s.ID_Status
                LEFT JOIN BookIssuance bi ON b.IDBook = bi.BookId AND bi.ReturnDate IS NULL
                LEFT JOIN GroupIssuanceItems git ON b.IDBook = git.BookId
                LEFT JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId AND gi.ReturnDate IS NULL
                WHERE b.InventaryNumber = @Inv";

            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Проверяем, выдана ли книга
                            bool isIssued = Convert.ToInt32(reader["IsIssued"]) == 1;

                            if (isIssued)
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга {inventoryNumber} находится на руках у читателя. Сначала необходимо вернуть книгу.",
                                    IsIssued = true
                                };
                            }

                            // Проверяем ID_Disposal (уже списана)
                            if (reader["ID_Disposal"] != DBNull.Value)
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга {inventoryNumber} уже списана."
                                };
                            }

                            // Проверяем статус книги
                            string statusName = reader["StatusName"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(statusName) && statusName.ToLower().Contains("списана"))
                            {
                                return new BookStatusCheckResult
                                {
                                    CanBeDisposed = false,
                                    Message = $"Книга имеет статус '{statusName}'."
                                };
                            }

                            return new BookStatusCheckResult { CanBeDisposed = true, Message = "" };
                        }
                    }
                }
            }
            return new BookStatusCheckResult { CanBeDisposed = false, Message = "Книга не найдена" };
        }

        /// <summary>
        /// Получение информации о выдаче книги (кому, когда, срок возврата)
        /// </summary>
        private string GetIssuedInfo(string inventoryNumber)
        {
            const string sql = @"
                SELECT 
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN 
                            'читателю ' + r.LastName + ' ' + r.FirstName + ' ' + ISNULL(r.MiddleName, '')
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN 
                            'группе ' + g.GroupCode + ' (преп. ' + t.LastName + ' ' + t.FirstName + ')'
                        ELSE ''
                    END AS RecipientInfo,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN bi.IssuanceDate
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN gi.IssuanceDate
                        ELSE NULL
                    END AS IssueDate,
                    CASE 
                        WHEN bi.ReaderId IS NOT NULL THEN bi.PlannedReturnDate
                        WHEN gi.GroupIssuanceId IS NOT NULL THEN gi.PlannedReturnDate
                        ELSE NULL
                    END AS PlannedReturnDate
                FROM Books b
                LEFT JOIN BookIssuance bi ON b.IDBook = bi.BookId AND bi.ReturnDate IS NULL
                LEFT JOIN ReaderForm r ON bi.ReaderId = r.ReaderID
                LEFT JOIN GroupIssuanceItems git ON b.IDBook = git.BookId
                LEFT JOIN GroupBookIssuance gi ON git.GroupIssuanceId = gi.GroupIssuanceId AND gi.ReturnDate IS NULL
                LEFT JOIN Groups g ON gi.GroupId = g.GroupID
                LEFT JOIN ReaderForm t ON gi.TeacherId = t.ReaderID
                WHERE b.InventaryNumber = @Inv";

            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Inv", inventoryNumber);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string recipientInfo = reader["RecipientInfo"]?.ToString() ?? "";
                            DateTime? issueDate = reader["IssueDate"] != DBNull.Value ? Convert.ToDateTime(reader["IssueDate"]) : (DateTime?)null;
                            DateTime? plannedReturnDate = reader["PlannedReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["PlannedReturnDate"]) : (DateTime?)null;

                            string result = recipientInfo;
                            if (issueDate.HasValue)
                            {
                                result += $"\nДата выдачи: {issueDate.Value:dd.MM.yyyy}";
                            }
                            if (plannedReturnDate.HasValue)
                            {
                                result += $"\nПлановая дата возврата: {plannedReturnDate.Value:dd.MM.yyyy}";
                                if (plannedReturnDate.Value < DateTime.Today)
                                {
                                    result += "\nВНИМАНИЕ: Просрочена!";
                                }
                            }
                            return result;
                        }
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Кнопка поиска (резервный метод для ручного поиска)
        /// </summary>
        private async void BtnFindBook_Click(object sender, RoutedEventArgs e)
        {
            string searchText = TbSearchBook.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                CustomMessageBox.Show("Введите инвентарный номер или название книги.", "Внимание",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            await SearchBooksAsync(searchText);

            if (_searchResults.Count == 1)
            {
                LoadBookDetails(_searchResults.First());
            }
            else if (_searchResults.Count > 1)
            {
                LbSearchResults.Visibility = Visibility.Visible;
            }
            else
            {
                CustomMessageBox.Show("Книга не найдена.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                PnBookDetails.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Обработчик потери фокуса полем поиска (скрываем выпадающий список)
        /// </summary>
        private void TbSearchBook_LostFocus(object sender, RoutedEventArgs e)
        {
            // Даём время на обработку выбора
            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => LbSearchResults.Visibility = Visibility.Collapsed);
            });
        }

        /// <summary>
        /// Обработчик получения фокуса полем поиска (показываем список при наличии результатов)
        /// </summary>
        private void TbSearchBook_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_searchResults.Any())
            {
                LbSearchResults.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Добавление книг в акт

        /// <summary>
        /// Кнопка добавления книги в акт
        /// </summary>
        private void BtnAddToAct_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedBook == null)
            {
                CustomMessageBox.Show("Сначала найдите книгу.", "Внимание",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка дубликатов в текущем акте
            if (_items.Any(item => item.InventoryNumber == _currentSelectedBook.InventoryNumber))
            {
                CustomMessageBox.Show($"Книга {_currentSelectedBook.InventoryNumber} уже добавлена в акт.");
                return;
            }

            // Добавляем книгу в акт
            AddBookToAct(_currentSelectedBook);
        }

        /// <summary>
        /// Добавление книги в акт списания
        /// </summary>
        private void AddBookToAct(BookSearchResult book)
        {
            // Парсим название и автора
            string author = book.AuthorName;
            string title = book.Title;

            // Парсим год и издательство
            string publishingHouse = "";
            int year = DateTime.Now.Year;

            if (!string.IsNullOrEmpty(book.PublisherYear) && book.PublisherYear.Contains(","))
            {
                var parts = book.PublisherYear.Split(',');
                publishingHouse = parts[0].Trim();
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int parsedYear))
                {
                    year = parsedYear;
                }
            }

            // Парсим цену (рубли и копейки)
            int priceRub = (int)Math.Floor(book.Price);
            int priceKop = (int)((book.Price - priceRub) * 100);

            var item = new BookItem
            {
                RowNum = _nextRowNum++,
                InventoryNumber = book.InventoryNumber,
                Title = title,
                Author = author,
                PublishingHouse = publishingHouse,
                Year = year,
                PriceRub = priceRub,
                PriceKop = priceKop,
                Quantity = 1
            };

            _items.Add(item);

            // Показываем DataGrid и обновляем UI
            DgItems.Visibility = Visibility.Visible;
            DgItems.ItemsSource = _items;

            // Показываем заголовок таблицы и панель итогов
            TableHeader.Visibility = Visibility.Visible;
            TotalPanel.Visibility = Visibility.Visible;

            // Очищаем поле поиска и скрываем детали
            TbSearchBook.Clear();
            PnBookDetails.Visibility = Visibility.Collapsed;
            _currentSelectedBook = null;

            // Обновляем счетчик книг
            BooksCountLabel.Text = $"(всего: {_items.Count} книг)";

            // Обновляем итоговую сумму
            UpdateTotal();

            CustomMessageBox.Show($"Книга '{title}' добавлена в акт списания.", "Успешно",
                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        /// <summary>
        /// Удаление книги из акта
        /// </summary>
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookItem item)
            {
                var result = CustomMessageBox.Show($"Удалить книгу '{item.Title}' из акта?",
                    "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

                if (result == CustomMessageBoxResult.Yes)
                {
                    _items.Remove(item);

                    // Перенумеровываем строки
                    int num = 1;
                    foreach (var i in _items)
                    {
                        i.RowNum = num++;
                    }
                    _nextRowNum = num;

                    // Обновляем счетчик
                    BooksCountLabel.Text = $"(всего: {_items.Count} книг)";

                    // Обновляем итоговую сумму
                    UpdateTotal();

                    // Если книг больше нет, скрываем таблицу
                    if (_items.Count == 0)
                    {
                        DgItems.Visibility = Visibility.Collapsed;
                        TableHeader.Visibility = Visibility.Collapsed;
                        TotalPanel.Visibility = Visibility.Collapsed;
                    }

                    CustomMessageBox.Show("Книга удалена из акта.", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Обновление итоговой суммы списания
        /// </summary>
        private void UpdateTotal()
        {
            decimal total = 0;
            foreach (var i in _items)
            {
                total += i.TotalPrice;
            }
            TbTotal.Text = total.ToString("N2");
        }

        #endregion

        #region Формирование акта и сохранение в БД

        /// <summary>
        /// Кнопка формирования акта списания
        /// </summary>
        private void BtnGenerateAct_Click(object sender, RoutedEventArgs e)
        {
            // Проверка наличия книг в акте
            if (_items.Count == 0)
            {
                CustomMessageBox.Show("Добавьте книги в акт.");
                return;
            }

            // Проверка выбора причины списания
            if (CbReason.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите причину списания.");
                return;
            }

            var selectedReason = (ReasonDisposalBook)CbReason.SelectedItem;
            string actNumber = TbActNumber.Text.Trim();
            string participants = TbParticipants.Text.Trim();

            // Проверка заполнения обязательных полей
            if (string.IsNullOrEmpty(actNumber) || string.IsNullOrEmpty(participants))
            {
                CustomMessageBox.Show("Заполните все поля.");
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_conn))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем запись об акте списания
                            int writeOffId;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO DisposalBooks (DisposalNumber, DisposalDate, Participants, Reason)
                                VALUES (@Number, @Date, @Participants, @Reason);
                                SELECT SCOPE_IDENTITY();", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Number", actNumber);
                                cmd.Parameters.AddWithValue("@Date", DpDate.SelectedDate ?? DateTime.Today);
                                cmd.Parameters.AddWithValue("@Participants", participants);
                                cmd.Parameters.AddWithValue("@Reason", selectedReason.ID_Reason);

                                writeOffId = Convert.ToInt32(cmd.ExecuteScalar());
                                _currentWriteOffId = writeOffId;
                            }

                            // 2. Получаем ID статуса "Списана"
                            int disposedStatusId = GetDisposedStatusId(conn, transaction);

                            // 3. Добавляем книги в DisposalItems и обновляем статус книг в Books
                            foreach (var item in _items)
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO DisposalItems (IDDisposal, InventoryNumber, TitleBook, PriceRub, PriceKop, Quantity, TotalPrice)
                                    VALUES (@ID, @Inv, @Title, @Rub, @Kop, @Qty, @Total);
                                    
                                    UPDATE Books 
                                    SET ID_Disposal = @ID, StatusBook = @StatusId
                                    WHERE InventaryNumber = @Inv;", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", writeOffId);
                                    cmd.Parameters.AddWithValue("@Inv", item.InventoryNumber);
                                    cmd.Parameters.AddWithValue("@Title", item.Title);
                                    cmd.Parameters.AddWithValue("@Rub", item.PriceRub);
                                    cmd.Parameters.AddWithValue("@Kop", item.PriceKop);
                                    cmd.Parameters.AddWithValue("@Qty", item.Quantity);
                                    cmd.Parameters.AddWithValue("@Total", item.TotalPrice);
                                    cmd.Parameters.AddWithValue("@StatusId", disposedStatusId);

                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            // Генерируем PDF-документ
                            byte[] pdfBytes = Services.DisposalActPdfService.GenerateDisposalActPdf(writeOffId);

                            // Сохраняем PDF на рабочий стол
                            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            string fileName = $"Акт_списания_{actNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                            string fullPath = System.IO.Path.Combine(desktop, fileName);
                            System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                            CustomMessageBox.Show($"Акт списания успешно создан!\n\nPDF сохранён:\n{fullPath}", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearForm(); // Очищаем форму для следующего акта
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Получение ID статуса "Списана" из базы данных
        /// </summary>
        private int GetDisposedStatusId(SqlConnection conn, SqlTransaction transaction)
        {
            using (var cmd = new SqlCommand(
                "SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Списана%'",
                conn, transaction))
            {
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 1;
            }
        }

        /// <summary>
        /// Очистка формы после успешного сохранения акта
        /// </summary>
        private void ClearForm()
        {
            TbActNumber.Text = GetNextDisposalNumber();     // Новый номер акта
            DpDate.SelectedDate = DateTime.Today;           // Текущая дата
            TbParticipants.Clear();                         // Очистка участников
            CbReason.SelectedIndex = -1;                    // Сброс причины
            _items.Clear();                                 // Очистка списка книг
            _nextRowNum = 1;                                // Сброс нумерации
            TbTotal.Text = "0.00";                          // Сброс суммы
            TbSearchBook.Clear();                           // Очистка поиска
            _searchResults.Clear();                         // Очистка результатов поиска
            _currentSelectedBook = null;                    // Сброс выбранной книги
            LbSearchResults.ItemsSource = null;
            LbSearchResults.Visibility = Visibility.Collapsed;
            PnBookDetails.Visibility = Visibility.Collapsed;
            DgItems.Visibility = Visibility.Collapsed;
            TableHeader.Visibility = Visibility.Collapsed;
            TotalPanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Навигация

        /// <summary>
        /// Закрытие страницы (возврат назад)
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        #endregion
    }
}