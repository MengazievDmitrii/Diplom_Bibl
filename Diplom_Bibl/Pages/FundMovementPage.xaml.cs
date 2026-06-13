using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class FundMovementPage : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<FundMovementItem> _allItems;
        private bool _isPlaceholderActive = true;

        // Цвета статусов (создаются в UI-потоке)
        private static readonly SolidColorBrush ActiveColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
        private static readonly SolidColorBrush OverdueColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
        private static readonly SolidColorBrush ReturnedColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF"));

        /// <summary>
        /// Модель элемента движения фонда
        /// </summary>
        public class FundMovementItem
        {
            public string IssuanceType { get; set; }
            public int Id { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ReturnDate { get; set; }
            public string Status { get; set; }
            public int RecipientId { get; set; }
            public string RecipientName { get; set; }
            public string RecipientType { get; set; }
            public string GroupCode { get; set; }
            public string TeacherName { get; set; }
            public string StudentName { get; set; }
            public int IDBook { get; set; }
            public string BookTitle { get; set; }
            public string InventaryNumber { get; set; }
            public int Quantity { get; set; }
            public int ReturnedQuantity { get; set; }
            public bool IsOverdue { get; set; }
            public int DaysRemaining { get; set; }
            public string Notes { get; set; }

            public string IssuanceTypeDisplay => IssuanceType == "individual" ? "Индивидуальная" : "Групповая";
            public string RecipientDisplay => $"{RecipientName} ({RecipientType})";
            public string GroupTeacherDisplay => !string.IsNullOrEmpty(GroupCode) ? $"Группа: {GroupCode}" :
                                                  !string.IsNullOrEmpty(TeacherName) ? $"Преп: {TeacherName}" : "—";
            public string StatusDisplay => ReturnDate.HasValue ? "Возвращена" :
                                            IsOverdue ? "Просрочена" : "Активна";
        }

        /// <summary>
        /// Группированный элемент движения фонда
        /// </summary>
        public class GroupedFundMovementItem
        {
            public string GroupKey { get; set; }
            public string IssuanceType { get; set; }
            public string IssuanceTypeDisplay { get; set; }
            public int RecipientId { get; set; }
            public string RecipientName { get; set; }
            public string RecipientType { get; set; }
            public string RecipientDisplay { get; set; }
            public string GroupCode { get; set; }
            public string TeacherName { get; set; }
            public string StudentName { get; set; }
            public string GroupTeacherDisplay { get; set; }
            public DateTime IssuanceDate { get; set; }
            public DateTime PlannedReturnDate { get; set; }
            public DateTime? ReturnDate { get; set; }
            public string Status { get; set; }
            public string StatusDisplay { get; set; }
            public bool IsOverdue { get; set; }
            public int TotalQuantity { get; set; }
            public string BookTitles { get; set; }
            public SolidColorBrush StatusColor { get; set; }
        }

        public FundMovementPage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                FundMovementGrid.Visibility = Visibility.Collapsed;

                var rawItems = await Task.Run(() => LoadRawData());

                await Dispatcher.InvokeAsync(() =>
                {
                    _allItems = rawItems;
                    var grouped = GroupAndSortItems(rawItems);
                    FundMovementGrid.ItemsSource = grouped;
                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                FundMovementGrid.Visibility = Visibility.Visible;
            }
        }

        private ObservableCollection<FundMovementItem> LoadRawData()
        {
            var items = new ObservableCollection<FundMovementItem>();

            const string sql = @"
                SELECT 
                    IssuanceType, Id, IssuanceDate, PlannedReturnDate, ReturnDate, Status,
                    RecipientId, RecipientName, RecipientType, GroupCode, TeacherName, StudentName,
                    BookId, BookTitle, InventaryNumber, Quantity, ReturnedQuantity,
                    IsOverdue, DaysRemaining, Notes
                FROM vw_FundMovement
                ORDER BY 
                    CASE WHEN ReturnDate IS NULL THEN 0 ELSE 1 END,
                    IsOverdue DESC, 
                    PlannedReturnDate";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new FundMovementItem
                        {
                            IssuanceType = reader["IssuanceType"]?.ToString() ?? "",
                            Id = Convert.ToInt32(reader["Id"]),
                            IssuanceDate = Convert.ToDateTime(reader["IssuanceDate"]),
                            PlannedReturnDate = Convert.ToDateTime(reader["PlannedReturnDate"]),
                            ReturnDate = reader["ReturnDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReturnDate"]) : (DateTime?)null,
                            Status = reader["Status"]?.ToString() ?? "",
                            RecipientId = Convert.ToInt32(reader["RecipientId"]),
                            RecipientName = reader["RecipientName"]?.ToString() ?? "",
                            RecipientType = reader["RecipientType"]?.ToString() ?? "",
                            GroupCode = reader["GroupCode"]?.ToString() ?? "",
                            TeacherName = reader["TeacherName"]?.ToString() ?? "",
                            StudentName = reader["StudentName"]?.ToString() ?? "",
                            IDBook = Convert.ToInt32(reader["BookId"]),
                            BookTitle = reader["BookTitle"]?.ToString() ?? "",
                            InventaryNumber = reader["InventaryNumber"]?.ToString() ?? "",
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            ReturnedQuantity = reader["ReturnedQuantity"] != DBNull.Value ? Convert.ToInt32(reader["ReturnedQuantity"]) : 0,
                            IsOverdue = Convert.ToBoolean(reader["IsOverdue"]),
                            DaysRemaining = Convert.ToInt32(reader["DaysRemaining"]),
                            Notes = reader["Notes"]?.ToString() ?? ""
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Группировка и сортировка: сначала активные, потом просроченные, потом возвращённые
        /// </summary>
        private ObservableCollection<GroupedFundMovementItem> GroupAndSortItems(ObservableCollection<FundMovementItem> items)
        {
            var grouped = items
                .GroupBy(x => new
                {
                    x.IssuanceType,
                    x.RecipientId,
                    x.RecipientName,
                    x.RecipientType,
                    x.GroupCode,
                    x.TeacherName,
                    x.StudentName,
                    x.IssuanceDate,
                    x.PlannedReturnDate,
                    x.ReturnDate,
                    x.Status,
                    x.IsOverdue
                })
                .Select(g =>
                {
                    // Внутри GroupAndSortItems(), в Select для каждой группы
                    DateTime? returnDate = g.Key.ReturnDate;
                    DateTime plannedReturnDate = g.Key.PlannedReturnDate;

                    SolidColorBrush color;
                    string statusDisplay;

                    if (returnDate.HasValue)
                    {
                        color = ReturnedColor;
                        statusDisplay = "Возвращена";
                    }
                    else
                    {
                        // Пересчитываем просроченность: просрочено, если сегодня > plannedReturnDate
                        bool isOverdue = DateTime.Today > plannedReturnDate;
                        if (isOverdue)
                        {
                            color = OverdueColor;
                            statusDisplay = "Просрочена";
                        }
                        else
                        {
                            color = ActiveColor;
                            statusDisplay = "Активна";
                        }
                    }


                    return new GroupedFundMovementItem
                    {
                        GroupKey = $"{g.Key.IssuanceType}_{g.Key.RecipientId}",
                        IssuanceType = g.Key.IssuanceType,
                        IssuanceTypeDisplay = g.Key.IssuanceType == "individual" ? "Индивидуальная" : "Групповая",
                        RecipientId = g.Key.RecipientId,
                        RecipientName = g.Key.RecipientName,
                        RecipientType = g.Key.RecipientType,
                        RecipientDisplay = $"{g.Key.RecipientName} ({g.Key.RecipientType})",
                        GroupCode = g.Key.GroupCode ?? "",
                        TeacherName = g.Key.TeacherName ?? "",
                        StudentName = g.Key.StudentName ?? "",
                        GroupTeacherDisplay = !string.IsNullOrEmpty(g.Key.GroupCode) ? $"Группа: {g.Key.GroupCode}" :
                                              !string.IsNullOrEmpty(g.Key.TeacherName) ? $"Преп: {g.Key.TeacherName}" : "—",
                        IssuanceDate = g.Key.IssuanceDate,
                        PlannedReturnDate = g.Key.PlannedReturnDate,
                        ReturnDate = g.Key.ReturnDate,
                        Status = g.Key.Status,
                        StatusDisplay = statusDisplay,
                        IsOverdue = g.Key.IsOverdue,
                        TotalQuantity = g.Sum(x => x.Quantity),
                        BookTitles = string.Join(", ", g.Select(x => x.BookTitle).Distinct()),
                        StatusColor = color
                    };
                })
                // Сортировка: Активные → Просроченные → Возвращённые
                .OrderBy(x => x.ReturnDate.HasValue ? 2 : x.IsOverdue ? 1 : 0)
                .ThenByDescending(x => x.IsOverdue)
                .ThenBy(x => x.PlannedReturnDate)
                .ToList();

            return new ObservableCollection<GroupedFundMovementItem>(grouped);
        }

        private void ApplyFilters()
        {
            if (_allItems == null) return;

            string searchTerm = SearchBox.Text.Trim();
            if (_isPlaceholderActive || searchTerm == "Поиск по книге, читателю, группе...")
                searchTerm = "";

            string typeFilter = (TypeFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все выдачи";
            string statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

            var filtered = _allItems.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(i =>
                    i.BookTitle.ToLower().Contains(searchTerm.ToLower()) ||
                    i.RecipientName.ToLower().Contains(searchTerm.ToLower()) ||
                    i.InventaryNumber.ToLower().Contains(searchTerm.ToLower()) ||
                    (i.GroupCode?.ToLower().Contains(searchTerm.ToLower()) ?? false) ||
                    (i.TeacherName?.ToLower().Contains(searchTerm.ToLower()) ?? false)
                );
            }

            if (typeFilter == "Индивидуальные")
                filtered = filtered.Where(i => i.IssuanceType == "individual");
            else if (typeFilter == "Групповые")
                filtered = filtered.Where(i => i.IssuanceType == "group");

            if (statusFilter == "Активные")
                filtered = filtered.Where(i => !i.ReturnDate.HasValue && !i.IsOverdue);
            else if (statusFilter == "Просроченные")
                filtered = filtered.Where(i => !i.ReturnDate.HasValue && DateTime.Today > i.PlannedReturnDate);

            else if (statusFilter == "Возвращенные")
                filtered = filtered.Where(i => i.ReturnDate.HasValue);

            var filteredList = filtered.ToList();
            var grouped = GroupAndSortItems(new ObservableCollection<FundMovementItem>(filteredList));
            FundMovementGrid.ItemsSource = grouped;
        }

        private void UpdateStatistics()
        {
            if (_allItems == null) return;

            var activeItems = _allItems.Where(i => !i.ReturnDate.HasValue);
            // Пересчитываем просроченные: сегодня > PlannedReturnDate
            var overdueItems = activeItems.Where(i => DateTime.Today > i.PlannedReturnDate);

            TotalIssuedText.Text = activeItems.Sum(i => i.Quantity).ToString();
            OverdueText.Text = overdueItems.Sum(i => i.Quantity).ToString();
            StudentsCountText.Text = _allItems.Where(i => i.RecipientType == "Читатель").Select(i => i.RecipientId).Distinct().Count().ToString();
            GroupIssuancesText.Text = _allItems.Count(i => i.IssuanceType == "group" && !i.ReturnDate.HasValue).ToString();
            IndividualIssuancesText.Text = _allItems.Count(i => i.IssuanceType == "individual" && !i.ReturnDate.HasValue).ToString();
        }


        #region Обработчики событий

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderActive && SearchBox.Text == "Поиск по книге, читателю, группе...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Поиск по книге, читателю, группе...";
                SearchBox.Foreground = Brushes.Gray;
                _isPlaceholderActive = true;
                ApplyFilters();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPlaceholderActive) return;
            ApplyFilters();
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "Поиск по книге, читателю, группе...";
            SearchBox.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
            TypeFilterCombo.SelectedIndex = 0;
            StatusFilterCombo.SelectedIndex = 0;
            ApplyFilters();
        }

        private void FundMovementGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FundMovementGrid.SelectedItem is GroupedFundMovementItem selected)
            {
                if (selected.IssuanceType == "individual")
                {
                    NavigationService?.Navigate(new Readers());
                }
                else
                {
                    CustomMessageBox.Show(
                        $"Групповая выдача\nГруппа: {selected.GroupCode}\nПреподаватель: {selected.TeacherName}\nСтудент: {selected.StudentName}\nКниг выдано: {selected.TotalQuantity}\nКниги: {selected.BookTitles}",
                        "Детали выдачи", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
            }
        }

        #endregion
    }
}