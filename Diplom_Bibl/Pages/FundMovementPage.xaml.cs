using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    public partial class FundMovementPage : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;";
        private ObservableCollection<FundMovementItem> _allItems;
        private bool _isPlaceholderActive = true;

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

            // Вычисляемые свойства для отображения
            public string IssuanceTypeDisplay => IssuanceType == "individual" ? "Индивидуальная" : "Групповая";
            public string RecipientDisplay => $"{RecipientName} ({RecipientType})";
            public string GroupTeacherDisplay => !string.IsNullOrEmpty(GroupCode) ? $"Группа: {GroupCode}" :
                                                  !string.IsNullOrEmpty(TeacherName) ? $"Преп: {TeacherName}" : "—";
            public string StatusDisplay => ReturnDate.HasValue ? "Возвращена" :
                                            IsOverdue ? "Просрочена" : "Активна";

            public SolidColorBrush StatusColor
            {
                get
                {
                    if (ReturnDate.HasValue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                    if (IsOverdue)
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                }
            }
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

                await Task.Run(() =>
                {
                    var items = new ObservableCollection<FundMovementItem>();

                    const string sql = @"
                        SELECT 
                            IssuanceType,
                            Id,
                            IssuanceDate,
                            PlannedReturnDate,
                            ReturnDate,
                            Status,
                            RecipientId,
                            RecipientName,
                            RecipientType,
                            GroupCode,
                            TeacherName,
                            StudentName,
                            IDBook,
                            BookTitle,
                            InventaryNumber,
                            Quantity,
                            ReturnedQuantity,
                            IsOverdue,
                            DaysRemaining,
                            Notes
                        FROM vw_FundMovement
                        ORDER BY IsOverdue DESC, PlannedReturnDate";

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
                                    IDBook = Convert.ToInt32(reader["IDBook"]),
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

                    Dispatcher.Invoke(() =>
                    {
                        _allItems = items;
                        ApplyFilters();
                        UpdateStatistics();
                    });
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

        private void ApplyFilters()
        {
            if (_allItems == null) return;

            string searchTerm = SearchBox.Text.Trim();
            if (_isPlaceholderActive || searchTerm == "Поиск по книге, читателю, группе...")
                searchTerm = "";

            string typeFilter = (TypeFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все выдачи";
            string statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

            var filtered = _allItems.AsEnumerable();

            // Поиск
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

            // Фильтр по типу
            if (typeFilter == "Индивидуальные")
                filtered = filtered.Where(i => i.IssuanceType == "individual");
            else if (typeFilter == "Групповые")
                filtered = filtered.Where(i => i.IssuanceType == "group");

            // Фильтр по статусу
            if (statusFilter == "Активные")
                filtered = filtered.Where(i => !i.ReturnDate.HasValue && !i.IsOverdue);
            else if (statusFilter == "Просроченные")
                filtered = filtered.Where(i => !i.ReturnDate.HasValue && i.IsOverdue);
            else if (statusFilter == "Возвращенные")
                filtered = filtered.Where(i => i.ReturnDate.HasValue);

            FundMovementGrid.ItemsSource = filtered.ToList();
        }

        private void UpdateStatistics()
        {
            if (_allItems == null) return;

            var activeItems = _allItems.Where(i => !i.ReturnDate.HasValue);
            var overdueItems = activeItems.Where(i => i.IsOverdue);

            TotalIssuedText.Text = activeItems.Sum(i => i.Quantity).ToString();
            OverdueText.Text = overdueItems.Sum(i => i.Quantity).ToString();

            StudentsCountText.Text = _allItems
                .Where(i => i.RecipientType == "Читатель")
                .Select(i => i.RecipientId)
                .Distinct()
                .Count()
                .ToString();

            GroupIssuancesText.Text = _allItems.Count(i => i.IssuanceType == "group" && !i.ReturnDate.HasValue).ToString();
            IndividualIssuancesText.Text = _allItems.Count(i => i.IssuanceType == "individual" && !i.ReturnDate.HasValue).ToString();
        }

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

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

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
            if (FundMovementGrid.SelectedItem is FundMovementItem selected)
            {
                if (selected.IssuanceType == "individual")
                {
                    // Переход на страницу читателя
                    NavigationService.Navigate(new Readers());
                }
                else
                {
                    // Для групповых выдач можно показать детали
                     CustomMessageBox.Show($"Групповая выдача\n" +
                                   $"Группа: {selected.GroupCode}\n" +
                                   $"Преподаватель: {selected.TeacherName}\n" +
                                   $"Студент: {selected.StudentName}\n" +
                                   $"Книг выдано: {selected.Quantity}\n" +
                                   $"Возвращено: {selected.ReturnedQuantity}",
                                   "Детали выдачи",
                                   CustomMessageBoxButton.OK,
                                   CustomMessageBoxImage.Information);
                }
            }
        }
    }
}