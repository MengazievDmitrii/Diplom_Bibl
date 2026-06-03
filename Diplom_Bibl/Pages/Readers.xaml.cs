using Dadata.Model;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class Readers : Page
    {
        private string connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<ReaderDisplayItem> _allReaders;
        private ObservableCollection<ReaderDisplayItem> _filteredReaders;
        private ObservableCollection<ReaderDisplayItem> _searchResults;

        private int? _currentReaderId = null;
        private bool _isSearchProgrammatic = false;


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
            _searchResults = new ObservableCollection<ReaderDisplayItem>();

            // Устанавливаем плейсхолдеры
            SetPlaceholders();
        }

        private void SetPlaceholders()
        {
            SearchBox.Text = "Поиск по фамилии, имени или номеру билета...";
            SearchBox.Foreground = Brushes.Gray;
            SearchBoxPost.Text = "Поиск по должности...";
            SearchBoxPost.Foreground = Brushes.Gray;
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

            // Фильтр по тексту поиска (ФИО, номер билета)
            string searchTerm = SearchBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchTerm) &&
                searchTerm != "Поиск по фамилии, имени или номеру билета...")
            {
                filtered = filtered.Where(r =>
                    r.LastName.ToLower().Contains(searchTerm.ToLower()) ||
                    r.FirstName.ToLower().Contains(searchTerm.ToLower()) ||
                    r.CardNumber.ToLower().Contains(searchTerm.ToLower()));
            }

            // Фильтр по должности
            string searchPostTerm = SearchBoxPost.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchPostTerm) &&
                searchPostTerm != "Поиск по должности...")
            {
                filtered = filtered.Where(r =>
                    (r.PostName?.ToLower().Contains(searchPostTerm.ToLower()) ?? false));
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

        // Обработчики SearchBox (ФИО/билет)
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSearchProgrammatic) return;

            string searchText = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText) || searchText == "Поиск по фамилии, имени или номеру билета...")
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                ApplyFilters();  // Показываем всех (фильтр по другим параметрам)
                return;
            }

            PerformSearch(searchText);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Поиск по фамилии, имени или номеру билета...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Поиск по фамилии, имени или номеру билета...";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        // Обработчики SearchBoxPost (должность)
        private void SearchBoxPost_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBoxPost_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBoxPost.Text == "Поиск по должности...")
            {
                SearchBoxPost.Text = "";
                SearchBoxPost.Foreground = Brushes.Black;
            }
        }

        private void SearchBoxPost_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBoxPost.Text))
            {
                SearchBoxPost.Text = "Поиск по должности...";
                SearchBoxPost.Foreground = Brushes.Gray;
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
            SearchBox.Text = "Поиск по фамилии, имени или номеру билета...";
            SearchBox.Foreground = Brushes.Gray;
            SearchBoxPost.Text = "Поиск по должности...";
            SearchBoxPost.Foreground = Brushes.Gray;
            ApplyFilters();
        }
        private void PerformSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText) ||
                searchText == "Поиск по фамилии, имени или номеру билета...")
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                return;
            }

            _searchResults.Clear();

            var filtered = _allReaders.Where(r =>
                r.LastName.ToLower().Contains(searchText.ToLower()) ||
                r.FirstName.ToLower().Contains(searchText.ToLower()) ||
                r.CardNumber.ToLower().Contains(searchText.ToLower()))
                .Take(10);

            foreach (var item in filtered)
                _searchResults.Add(item);

            SearchResultsList.ItemsSource = _searchResults;
            SearchResultsList.Visibility = _searchResults.Any() ? Visibility.Visible : Visibility.Collapsed;
        }
        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is ReaderDisplayItem selected)
            {
                _isSearchProgrammatic = true;
                SearchBox.Text = selected.FullName;
                SearchResultsList.Visibility = Visibility.Collapsed;

                // Применяем фильтр по выбранному читателю (очищаем другие фильтры поиска)
                FilterTypeCombo.SelectedIndex = 0;
                FilterGroupCombo.SelectedIndex = 0;
                SearchBoxPost.Text = "Поиск по должности...";
                SearchBoxPost.Foreground = Brushes.Gray;

                // Фильтруем по ID выбранного читателя
                var filtered = _allReaders.Where(r => r.ReaderID == selected.ReaderID);
                _filteredReaders = new ObservableCollection<ReaderDisplayItem>(filtered);
                ReadersListView.ItemsSource = _filteredReaders;
                UpdateSearchCount();

                _isSearchProgrammatic = false;
                SearchBox.SelectAll();  // Выделить текст для удобства
            }
        }
        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SearchResultsList.Visibility != Visibility.Visible) return;

            switch (e.Key)
            {
                case Key.Down:
                    if (SearchResultsList.SelectedIndex < SearchResultsList.Items.Count - 1)
                        SearchResultsList.SelectedIndex++;
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (SearchResultsList.SelectedIndex > 0)
                        SearchResultsList.SelectedIndex--;
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (SearchResultsList.SelectedItem != null)
                    {
                        SearchResultsList_SelectionChanged(null, null);
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    SearchResultsList.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    break;
            }
        }
        private async void ExportReadersBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportReadersBtn.IsEnabled = false;
                ExportReadersBtn.Content = "⏳ Экспорт...";

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel файлы (*.xlsx)|*.xlsx|CSV файлы (*.csv)|*.csv",
                    Title = "Сохранить список читателей",
                    FileName = $"Читатели_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    var result = await Task.Run(() => ExportReadersToFile(filePath, extension));

                    CustomMessageBox.Show(result, "Экспорт завершен",
                        CustomMessageBoxButton.OK,
                        result.Contains("ошибка") ? CustomMessageBoxImage.Error : CustomMessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                ExportReadersBtn.IsEnabled = true;
                ExportReadersBtn.Content = "📥 Экспортировать читателей";
            }
        }

        private string ExportReadersToFile(string filePath, string extension)
        {
            try
            {
                // Получаем данные читателей с логинами и паролями
                var readers = GetReadersWithCredentials();

                if (extension == ".csv")
                {
                    ExportToCsv(readers, filePath);
                }
                else
                {
                    ExportToExcel(readers, filePath);
                }

                return $"Экспорт успешно завершен!\nСохранено: {readers.Count} читателей\nФайл: {filePath}";
            }
            catch (Exception ex)
            {
                return $"Ошибка при экспорте: {ex.Message}";
            }
        }

        private List<ReaderExportModel> GetReadersWithCredentials()
        {
            var readers = new List<ReaderExportModel>();

            string query = @"
        SELECT 
            r.ReaderID,
            r.CardNumber,
            r.LastName,
            r.FirstName,
            r.MiddleName,
            rt.TypeName,
            g.GroupCode,
            p.Post_Name,
            r.Speciality,
            r.PhoneNumber,
            r.HomeAddress,
            r.MembershipStartDate,
            r.FormFillDate,
            u.Login,
            '' AS Password  -- Пароль не хранится в открытом виде, показываем сообщение
        FROM ReaderForm r
        LEFT JOIN ReaderType rt ON r.TypeID = rt.TypeID
        LEFT JOIN Groups g ON r.GroupID = g.GroupID
        LEFT JOIN Post p ON r.Post = p.ID_Post
        LEFT JOIN Users u ON r.ReaderID = u.ReaderID
        ORDER BY r.LastName, r.FirstName";

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new ReaderExportModel
                        {
                            ReaderID = reader["ReaderID"] != DBNull.Value ? Convert.ToInt32(reader["ReaderID"]) : 0,
                            CardNumber = reader["CardNumber"]?.ToString() ?? "",
                            LastName = reader["LastName"]?.ToString() ?? "",
                            FirstName = reader["FirstName"]?.ToString() ?? "",
                            MiddleName = reader["MiddleName"]?.ToString() ?? "",
                            TypeName = reader["TypeName"]?.ToString() ?? "",
                            GroupCode = reader["GroupCode"]?.ToString() ?? "",
                            PostName = reader["Post_Name"]?.ToString() ?? "",
                            Speciality = reader["Speciality"]?.ToString() ?? "",
                            PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                            HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                            MembershipStartDate = reader["MembershipStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["MembershipStartDate"]) : DateTime.MinValue,
                            FormFillDate = reader["FormFillDate"] != DBNull.Value ? Convert.ToDateTime(reader["FormFillDate"]) : DateTime.MinValue,
                            Login = reader["Login"]?.ToString() ?? "",
                            Password = "******"  // Пароль скрыт для безопасности
                        };
                        readers.Add(item);
                    }
                }
            }

            return readers;
        }

        private void ExportToExcel(List<ReaderExportModel> readers, string filePath)
        {
            var excelApp = new Microsoft.Office.Interop.Excel.Application();
            Microsoft.Office.Interop.Excel.Workbook workbook = null;
            Microsoft.Office.Interop.Excel.Worksheet worksheet = null;

            try
            {
                excelApp.Visible = false;
                workbook = excelApp.Workbooks.Add();
                worksheet = workbook.ActiveSheet;
                worksheet.Name = "Читатели";

                // Заголовки
                string[] headers = {
            "ID", "№ билета", "Фамилия", "Имя", "Отчество",
            "Тип читателя", "Группа", "Должность", "Специальность",
            "Телефон", "Адрес", "Дата начала членства", "Дата оформления",
            "Логин", "Пароль"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1] = headers[i];
                    var range = (Microsoft.Office.Interop.Excel.Range)worksheet.Cells[1, i + 1];
                    range.Font.Bold = true;
                    range.Interior.Color = System.Drawing.Color.LightGray.ToArgb();
                }

                // Данные
                int row = 2;
                foreach (var r in readers)
                {
                    worksheet.Cells[row, 1] = r.ReaderID;
                    worksheet.Cells[row, 2] = r.CardNumber;
                    worksheet.Cells[row, 3] = r.LastName;
                    worksheet.Cells[row, 4] = r.FirstName;
                    worksheet.Cells[row, 5] = r.MiddleName;
                    worksheet.Cells[row, 6] = r.TypeName;
                    worksheet.Cells[row, 7] = r.GroupCode;
                    worksheet.Cells[row, 8] = r.PostName;
                    worksheet.Cells[row, 9] = r.Speciality;
                    worksheet.Cells[row, 10] = r.PhoneNumber;
                    worksheet.Cells[row, 11] = r.HomeAddress;
                    worksheet.Cells[row, 12] = r.MembershipStartDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 13] = r.FormFillDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 14] = r.Login;
                    worksheet.Cells[row, 15] = r.Password;
                    row++;
                }

                worksheet.Columns.AutoFit();
                workbook.SaveAs(filePath, Microsoft.Office.Interop.Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                if (worksheet != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
            }
        }

        private void ExportToCsv(List<ReaderExportModel> readers, string filePath)
        {
            var sb = new StringBuilder();

            // Заголовки
            sb.AppendLine("ID;Номер билета;Фамилия;Имя;Отчество;Тип читателя;Группа;Должность;Специальность;Телефон;Адрес;Дата начала членства;Дата оформления;Логин;Пароль");

            // Данные
            foreach (var r in readers)
            {
                sb.AppendLine($"{r.ReaderID};{EscapeCsv(r.CardNumber)};{EscapeCsv(r.LastName)};{EscapeCsv(r.FirstName)};{EscapeCsv(r.MiddleName)};{EscapeCsv(r.TypeName)};{EscapeCsv(r.GroupCode)};{EscapeCsv(r.PostName)};{EscapeCsv(r.Speciality)};{EscapeCsv(r.PhoneNumber)};{EscapeCsv(r.HomeAddress)};{r.MembershipStartDate:dd.MM.yyyy};{r.FormFillDate:dd.MM.yyyy};{EscapeCsv(r.Login)};{EscapeCsv(r.Password)}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Экранируем точку с запятой и кавычки
            if (value.Contains(";") || value.Contains("\""))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }
            return value;
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
// Модель для экспорта
        public class ReaderExportModel
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string TypeName { get; set; }
            public string GroupCode { get; set; }
            public string PostName { get; set; }
            public string Speciality { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public DateTime MembershipStartDate { get; set; }
            public DateTime FormFillDate { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }
