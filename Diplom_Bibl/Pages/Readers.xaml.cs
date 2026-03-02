using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для Readers.xaml
    /// </summary>
    public partial class Readers : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<ReaderDisplayItem> _allReaders;
        private ObservableCollection<ReaderDisplayItem> _filteredReaders;
        private bool _isPlaceholderActive = true;

        // Класс для типа читателя
        public class ReaderTypeItem
        {
            public int TypeID { get; set; }
            public string TypeName { get; set; }
            public string DisplayName => TypeName;
        }

        // Класс для отображения читателя
        public class ReaderDisplayItem
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public int TypeID { get; set; }
            public string TypeName { get; set; }
            public string GroupCode { get; set; }
            public string PostName { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public DateTime MembershipStartDate { get; set; }

            // Вычисляемые свойства для отображения
            public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();

            public string Initials
            {
                get
                {
                    string initials = LastName.Length > 0 ? LastName[0].ToString() : "";
                    if (FirstName.Length > 0) initials += FirstName[0];
                    return initials.ToUpper();
                }
            }

            public string MembershipDateString => MembershipStartDate.ToString("dd.MM.yyyy");

            public string GroupOrPostInfo
            {
                get
                {
                    if (!string.IsNullOrEmpty(GroupCode))
                        return $"🎓 Группа: {GroupCode}";
                    if (!string.IsNullOrEmpty(PostName))
                        return $"💼 Должность: {PostName}";
                    return "—";
                }
            }

            // Для фильтрации
            public bool IsStudent => !string.IsNullOrEmpty(GroupCode);
            public bool IsEmployee => !string.IsNullOrEmpty(PostName);
        }

        public Readers()
        {
            InitializeComponent();
            SetSearchPlaceholder();
        }

        private void SetSearchPlaceholder()
        {
            SearchBox.Text = "Поиск по фамилии, имени или номеру билета...";
            SearchBox.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
        }

        private void ClearPlaceholder()
        {
            if (_isPlaceholderActive)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void AddReader_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddReader());
        }

        private void LoadReaderTypes()
        {
            try
            {
                const string sql = "SELECT TypeID, TypeName FROM ReaderType ORDER BY TypeName";

                var types = new List<ReaderTypeItem>();
                types.Add(new ReaderTypeItem { TypeID = 0, TypeName = "Все типы" });

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ReaderTypeItem
                            {
                                TypeID = Convert.ToInt32(reader["TypeID"]),
                                TypeName = reader["TypeName"].ToString()
                            });
                        }
                    }
                }

                FilterTypeCombo.ItemsSource = types;
                FilterTypeCombo.DisplayMemberPath = "DisplayName";
                FilterTypeCombo.SelectedValuePath = "TypeID";
                FilterTypeCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типов читателей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReaders()
        {
            try
            {
                const string query = @"
                    SELECT 
                        r.ReaderID, 
                        r.CardNumber, 
                        r.LastName, 
                        r.FirstName, 
                        r.MiddleName,
                        r.TypeID,
                        rt.TypeName,
                        g.GroupCode,
                        p.Post_Name,
                        r.PhoneNumber,
                        r.HomeAddress,
                        r.MembershipStartDate
                    FROM ReaderForm r
                    LEFT JOIN ReaderType rt ON r.TypeID = rt.TypeID
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN Post p ON r.Post = p.ID_Post
                    ORDER BY r.LastName, r.FirstName";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<ReaderDisplayItem>();

                        while (reader.Read())
                        {
                            var item = new ReaderDisplayItem
                            {
                                ReaderID = Convert.ToInt32(reader["ReaderID"]),
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeID = Convert.ToInt32(reader["TypeID"]),
                                TypeName = reader["TypeName"]?.ToString() ?? "Не указан",
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                PostName = reader["Post_Name"]?.ToString() ?? "",
                                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                                HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                                MembershipStartDate = Convert.ToDateTime(reader["MembershipStartDate"])
                            };

                            items.Add(item);
                        }

                        _allReaders = items;
                        _filteredReaders = new ObservableCollection<ReaderDisplayItem>(_allReaders);
                        ReadersListView.ItemsSource = _filteredReaders;

                        UpdateStatistics();
                        UpdateSearchCount();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки читателей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allReaders == null) return;

            string searchTerm = SearchBox.Text?.Trim().ToLower() ?? "";
            if (_isPlaceholderActive) searchTerm = "";

            // Получаем выбранный тип
            int selectedTypeId = 0;
            if (FilterTypeCombo.SelectedValue != null && int.TryParse(FilterTypeCombo.SelectedValue.ToString(), out int typeId))
            {
                selectedTypeId = typeId;
            }

            // Получаем фильтр по группе/должности
            string groupPostFilter = "Все группы/должности";
            if (FilterGroupPostCombo.SelectedItem is ComboBoxItem groupPostItem)
            {
                groupPostFilter = groupPostItem.Content.ToString();
            }

            var filtered = _allReaders.AsEnumerable();

            // Поиск
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(r =>
                    r.LastName.ToLower().Contains(searchTerm) ||
                    r.FirstName.ToLower().Contains(searchTerm) ||
                    (r.MiddleName?.ToLower().Contains(searchTerm) ?? false) ||
                    r.CardNumber.ToLower().Contains(searchTerm) ||
                    r.PhoneNumber.ToLower().Contains(searchTerm)
                );
            }

            // Фильтр по типу
            if (selectedTypeId > 0)
            {
                filtered = filtered.Where(r => r.TypeID == selectedTypeId);
            }

            // Фильтр по группе/должности
            switch (groupPostFilter)
            {
                case "Студенты (с группой)":
                    filtered = filtered.Where(r => r.IsStudent);
                    break;
                case "Сотрудники (с должностью)":
                    filtered = filtered.Where(r => r.IsEmployee);
                    break;
                case "Без группы/должности":
                    filtered = filtered.Where(r => !r.IsStudent && !r.IsEmployee);
                    break;
            }

            var resultList = filtered.ToList();

            _filteredReaders.Clear();
            foreach (var item in resultList)
            {
                _filteredReaders.Add(item);
            }

            UpdateSearchCount();
        }

        private void UpdateSearchCount()
        {
            if (_allReaders != null && _filteredReaders != null)
            {
                SearchCountLabel.Text = $"Найдено: {_filteredReaders.Count} из {_allReaders.Count}";
            }
        }

        private void UpdateStatistics()
        {
            if (_allReaders == null) return;

            TotalReadersCount.Text = _allReaders.Count.ToString();
            StudentsCount.Text = _allReaders.Count(r => r.IsStudent).ToString();
            EmployeesCount.Text = _allReaders.Count(r => r.IsEmployee).ToString();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearPlaceholder();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SetSearchPlaceholder();
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

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            FilterTypeCombo.SelectedIndex = 0;
            FilterGroupPostCombo.SelectedIndex = 0;
            SetSearchPlaceholder();
        }

        private void ReadersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderDisplayItem selectedReader)
            {
                // Переход на страницу редактирования с ID выбранного читателя
                var addReaderPage = new AddReader(selectedReader.ReaderID);
                NavigationService.Navigate(addReaderPage);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReaderTypes();
            LoadReaders();
        }
    }
}