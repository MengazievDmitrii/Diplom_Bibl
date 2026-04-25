using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class MyRequestsPage : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private readonly int _readerId;
        private ObservableCollection<RequestItem> _allRequests;

        // Класс для отображения заявки
        public class RequestItem
        {
            public int ID_Request { get; set; }
            public string RequestNumber { get; set; } = "";
            public DateTime RequestDate { get; set; }
            public string LessonName { get; set; } = "";
            public DateTime LessonDate { get; set; }
            public string StatusName { get; set; } = "";
            public int TotalBooks { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public string GroupCode { get; set; } = "";
            public int StatusID { get; set; }

            public SolidColorBrush StatusColor
            {
                get
                {
                    switch (StatusName)
                    {
                        case "Новая": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
                        case "В обработке": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
                        case "Готова": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                        case "Выдана": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C"));
                        case "Отменена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                        case "Просрочена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                        default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
                    }
                }
            }
        }

        // Класс для книг в заявке
        public class RequestBookDetail
        {
            public string BookTitle { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public int Quantity { get; set; }
            public string StatusName { get; set; } = "";
        }

        public MyRequestsPage(int readerId)
        {
            InitializeComponent();
            _readerId = readerId;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadRequests();
        }

        private async System.Threading.Tasks.Task LoadRequests()
        {
            try
            {
                string query = @"
                    SELECT 
                        r.ID_Request,
                        r.RequestNumber,
                        r.RequestDate,
                        r.LessonName,
                        r.LessonDate,
                        rs.StatusName,
                        rs.ID_Status AS StatusID,
                        (SELECT SUM(Quantity) FROM RequestItems WHERE RequestID = r.ID_Request) AS TotalBooks,
                        r.ExpirationDate,
                        g.GroupCode
                    FROM BookRequests r
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
                    WHERE r.UserID = @ReaderId
                    ORDER BY r.RequestDate DESC";

                var requests = new ObservableCollection<RequestItem>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", _readerId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                requests.Add(new RequestItem
                                {
                                    ID_Request = Convert.ToInt32(reader["ID_Request"]),
                                    RequestNumber = reader["RequestNumber"].ToString(),
                                    RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                                    LessonName = reader["LessonName"].ToString(),
                                    LessonDate = Convert.ToDateTime(reader["LessonDate"]),
                                    StatusName = reader["StatusName"].ToString(),
                                    StatusID = Convert.ToInt32(reader["StatusID"]),
                                    TotalBooks = Convert.ToInt32(reader["TotalBooks"]),
                                    ExpirationDate = reader["ExpirationDate"] != DBNull.Value ? Convert.ToDateTime(reader["ExpirationDate"]) : (DateTime?)null,
                                    GroupCode = reader["GroupCode"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }

                _allRequests = requests;
                DgRequests.ItemsSource = _allRequests;
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки заявок: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            if (_allRequests == null) return;

            int total = _allRequests.Count;
            int active = _allRequests.Count(r => r.StatusName == "Новая" || r.StatusName == "В обработке");
            int ready = _allRequests.Count(r => r.StatusName == "Готова");
            int issued = _allRequests.Count(r => r.StatusName == "Выдана");

            // Можно добавить отображение статистики, если есть элементы в XAML
            // TbTotalCount.Text = total.ToString();
            // TbActiveCount.Text = active.ToString();
            // TbReadyCount.Text = ready.ToString();
            // TbIssuedCount.Text = issued.ToString();
        }

        private void ApplyFilters()
        {
            if (_allRequests == null) return;

            string searchTerm = TbSearch.Text?.Trim().ToLower() ?? "";
            string statusFilter = (CbStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все заявки";

            var filtered = _allRequests.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(r =>
                    r.RequestNumber.ToLower().Contains(searchTerm) ||
                    r.LessonName.ToLower().Contains(searchTerm) ||
                    r.GroupCode.ToLower().Contains(searchTerm));
            }

            if (statusFilter != "Все заявки")
            {
                filtered = filtered.Where(r => r.StatusName == statusFilter);
            }

            DgRequests.ItemsSource = filtered.ToList();
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private async void ViewRequest_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                int requestId = (int)btn.Tag;
                await ShowRequestDetails(requestId);
            }
        }

        private void CloseDetailsPanel_Click(object sender, RoutedEventArgs e)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            DgRequestItems.ItemsSource = null;
        }

        private async System.Threading.Tasks.Task ShowRequestDetails(int requestId)
        {
            try
            {
                string query = @"
                    SELECT 
                        r.RequestNumber,
                        r.LessonName,
                        r.LessonDate,
                        r.RequestDate,
                        g.GroupCode,
                        rs.StatusName,
                        r.ExpirationDate,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        ri.Quantity,
                        ris.StatusName AS ItemStatusName
                    FROM BookRequests r
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
                    LEFT JOIN RequestItems ri ON r.ID_Request = ri.RequestID
                    LEFT JOIN Books b ON ri.BookID = b.IDBook
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN RequestItemStatuses ris ON ri.StatusID = ris.ID_Status
                    WHERE r.ID_Request = @RequestId";

                var items = new ObservableCollection<RequestBookDetail>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            bool hasData = false;

                            while (await reader.ReadAsync())
                            {
                                if (!hasData)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        TbRequestNumber.Text = $"Заявка №: {reader["RequestNumber"]}";
                                        TbLessonInfo.Text = $"Занятие: {reader["LessonName"]}";
                                        TbRequestDate.Text = $"Дата создания: {Convert.ToDateTime(reader["RequestDate"]):dd.MM.yyyy HH:mm}";
                                        TbGroupInfo.Text = $"Группа: {reader["GroupCode"]?.ToString() ?? "Не указана"}";
                                        TbStatus.Text = $"Статус: {reader["StatusName"]}";
                                        if (reader["ExpirationDate"] != DBNull.Value)
                                            TbExpirationDate.Text = $"Срок брони: {Convert.ToDateTime(reader["ExpirationDate"]):dd.MM.yyyy}";
                                        else
                                            TbExpirationDate.Text = "";
                                    });
                                    hasData = true;
                                }

                                if (reader["TitleBook"] != DBNull.Value)
                                {
                                    items.Add(new RequestBookDetail
                                    {
                                        BookTitle = reader["TitleBook"].ToString(),
                                        AuthorName = reader["AuthorName"].ToString(),
                                        Quantity = Convert.ToInt32(reader["Quantity"]),
                                        StatusName = reader["ItemStatusName"]?.ToString() ?? "Неизвестен"
                                    });
                                }
                            }
                        }
                    }
                }

                DgRequestItems.ItemsSource = items;
                DetailsPanel.Visibility = Visibility.Visible;

                if (items.Count == 0)
                {
                    var emptyList = new ObservableCollection<RequestBookDetail>();
                    emptyList.Add(new RequestBookDetail
                    {
                        BookTitle = "Нет книг в заявке",
                        AuthorName = "",
                        Quantity = 0,
                        StatusName = ""
                    });
                    DgRequestItems.ItemsSource = emptyList;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки деталей: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DgRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRequests.SelectedItem is RequestItem selected)
            {
                _ = ShowRequestDetails(selected.ID_Request);
            }
        }
    }
}