using Dadata.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class Readers : Page
    {
        private string connectionString = DatabaseHelper.ConnectionString ;
        private ObservableCollection<ReaderDisplayItem> _allReaders;
        private ObservableCollection<ReaderDisplayItem> _filteredReaders;
        private int? _currentReaderId = null;

        // Класс для отображения читателя в списке
        public class ReaderDisplayItem
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; } = "";
            public string LastName { get; set; } = "";
            public string FirstName { get; set; } = "";
            public string MiddleName { get; set; } = "";
            public int TypeID { get; set; }
            public string TypeName { get; set; } = "";
            public int? GroupID { get; set; }
            public string GroupCode { get; set; } = "";
            public int? PostID { get; set; }
            public string PostName { get; set; } = "";
            public string Speciality { get; set; } = "";
            public string PhoneNumber { get; set; } = "";
            public string HomeAddress { get; set; } = "";
            public DateTime MembershipStartDate { get; set; }
            public DateTime FormFillDate { get; set; }

            public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
            public string Initials => GetInitials();
            public string MembershipDateString => MembershipStartDate.ToString("dd.MM.yyyy");
            public string GroupOrPostInfo => GetGroupOrPostInfo();

            private string GetInitials()
            {
                string initials = LastName;
                if (!string.IsNullOrEmpty(FirstName))
                    initials += $" {FirstName[0]}.";
                if (!string.IsNullOrEmpty(MiddleName))
                    initials += $" {MiddleName[0]}.";
                return initials;
            }

            private string GetGroupOrPostInfo()
            {
                if (TypeName?.ToLower().Contains("студент") == true && !string.IsNullOrEmpty(GroupCode))
                    return $"Группа: {GroupCode}";
                if (TypeName?.ToLower().Contains("сотрудник") == true && !string.IsNullOrEmpty(PostName))
                    return $"Должность: {PostName}";
                return "";
            }
        }

        // Класс для фильтрации по типу
        public class FilterTypeItem
        {
            public int TypeID { get; set; }
            public string DisplayName { get; set; } = "";
        }

        // Класс для фильтрации по группе
        public class FilterGroupItem
        {
            public int GroupID { get; set; }
            public string GroupCode { get; set; } = "";
        }

        public Readers()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFilterData();
            LoadReaders();
        }

        private void LoadFilterData()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Загружаем типы читателей для фильтра
                    var typeQuery = "SELECT TypeID, TypeName FROM ReaderType ORDER BY TypeName";
                    using (var cmd = new SqlCommand(typeQuery, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var types = new ObservableCollection<FilterTypeItem>();
                        types.Add(new FilterTypeItem { TypeID = 0, DisplayName = "Все типы" });

                        while (reader.Read())
                        {
                            types.Add(new FilterTypeItem
                            {
                                TypeID = Convert.ToInt32(reader["TypeID"]),
                                DisplayName = reader["TypeName"]?.ToString() ?? ""
                            });
                        }

                        FilterTypeCombo.ItemsSource = types;
                        FilterTypeCombo.SelectedValuePath = "TypeID";
                        FilterTypeCombo.DisplayMemberPath = "DisplayName";
                        FilterTypeCombo.SelectedIndex = 0;
                    }
                }

                // Загружаем группы для фильтра
                LoadGroupsForFilter();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных для фильтров: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadGroupsForFilter()
        {
            try
            {
                const string query = "SELECT GroupID, GroupCode FROM Groups ORDER BY GroupCode";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var groups = new ObservableCollection<FilterGroupItem>();
                        groups.Add(new FilterGroupItem { GroupID = 0, GroupCode = "Все группы" });

                        while (reader.Read())
                        {
                            groups.Add(new FilterGroupItem
                            {
                                GroupID = Convert.ToInt32(reader["GroupID"]),
                                GroupCode = reader["GroupCode"]?.ToString() ?? ""
                            });
                        }

                        FilterGroupCombo.ItemsSource = groups;
                        FilterGroupCombo.SelectedValuePath = "GroupID";
                        FilterGroupCombo.DisplayMemberPath = "GroupCode";
                        FilterGroupCombo.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки групп: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadReaders()
        {
            try
            {
                const string query = @"
                    SELECT 
                        r.ReaderID, r.CardNumber, r.LastName, r.FirstName, r.MiddleName,
                        r.TypeID, rt.TypeName,
                        r.GroupID, g.GroupCode,
                        r.Post, p.Post_Name,
                        r.Speciality, r.PhoneNumber, r.HomeAddress,
                        r.MembershipStartDate, r.FormFillDate
                    FROM ReaderForm r
                    LEFT JOIN ReaderType rt ON r.TypeID = rt.TypeID
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN Post p ON r.Post = p.ID_Post
                    ORDER BY r.LastName, r.FirstName";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<ReaderDisplayItem>();

                        while (reader.Read())
                        {
                            items.Add(new ReaderDisplayItem
                            {
                                ReaderID = reader["ReaderID"] != DBNull.Value ? Convert.ToInt32(reader["ReaderID"]) : 0,
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeID = reader["TypeID"] != DBNull.Value ? Convert.ToInt32(reader["TypeID"]) : 0,
                                TypeName = reader["TypeName"]?.ToString() ?? "",
                                GroupID = reader["GroupID"] != DBNull.Value ? Convert.ToInt32(reader["GroupID"]) : (int?)null,
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                PostID = reader["Post"] != DBNull.Value ? Convert.ToInt32(reader["Post"]) : (int?)null,
                                PostName = reader["Post_Name"]?.ToString() ?? "",
                                Speciality = reader["Speciality"]?.ToString() ?? "",
                                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                                HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                                MembershipStartDate = reader["MembershipStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["MembershipStartDate"]) : DateTime.Now,
                                FormFillDate = reader["FormFillDate"] != DBNull.Value ? Convert.ToDateTime(reader["FormFillDate"]) : DateTime.Now
                            });
                        }

                        _allReaders = items;
                        ApplyFilters();
                        UpdateStatistics();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки читателей: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allReaders == null) return;

            var filtered = _allReaders.AsEnumerable();

            // Фильтр по тексту поиска (включая поиск по должности)
            string searchTerm = SearchBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchTerm) && searchTerm != "Поиск по фамилии или номеру билета...")
            {
                filtered = filtered.Where(r =>
                    r.LastName.ToLower().Contains(searchTerm.ToLower()) ||
                    r.FirstName.ToLower().Contains(searchTerm.ToLower()) ||
                    r.CardNumber.ToLower().Contains(searchTerm.ToLower()) ||
                    (r.PostName?.ToLower().Contains(searchTerm.ToLower()) ?? false));
            }

            // Фильтр по типу читателя
            if (FilterTypeCombo.SelectedItem is FilterTypeItem selectedType && selectedType.TypeID > 0)
            {
                filtered = filtered.Where(r => r.TypeID == selectedType.TypeID);
            }

            // Фильтр по группе (только для студентов)
            if (FilterGroupCombo.SelectedItem is FilterGroupItem selectedGroup && selectedGroup.GroupID > 0)
            {
                filtered = filtered.Where(r => r.GroupID == selectedGroup.GroupID);
            }

            _filteredReaders = new ObservableCollection<ReaderDisplayItem>(filtered);
            ReadersListView.ItemsSource = _filteredReaders;

            // Обновляем счетчик результатов поиска
            UpdateSearchCount();
        }

        private void UpdateSearchCount()
        {
            int count = _filteredReaders?.Count ?? 0;
            SearchCountLabel.Text = $"Найдено: {count}";
        }

        private void UpdateStatistics()
        {
            if (_allReaders == null) return;

            TotalReadersCount.Text = _allReaders.Count.ToString();
            StudentsCount.Text = _allReaders.Count(r => r.TypeName?.ToLower().Contains("студент") == true).ToString();
            EmployeesCount.Text = _allReaders.Count(r => r.TypeName?.ToLower().Contains("сотрудник") == true).ToString();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Поиск по фамилии или номеру билета...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Поиск по фамилии или номеру билета...";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterTypeCombo.SelectedIndex = 0;
            FilterGroupCombo.SelectedIndex = 0;
            SearchBox.Text = "";
            ApplyFilters();
        }

        private void AddReader_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddReader());
        }

        private void ImportReadersBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ImportReaders());
        }

        private void ReadersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderDisplayItem selectedReader)
            {
                NavigationService?.Navigate(new AddReader(selectedReader.ReaderID));
            }
        }
    }
}