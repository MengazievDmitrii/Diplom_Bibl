using Dadata.Model;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class AddReader : Page
    {
        private readonly string connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<ReaderItem> _allReaders;
        private int? _currentReaderId;

        // Хранилище всех групп для фильтрации
        private ObservableCollection<GroupItem> _allGroups;

        public class ReaderTypeItem
        {
            public int ID { get; set; }
            public string DisplayTr { get; set; }
            public string TypeName { get; set; }
        }

        public class GroupItem
        {
            public int ID { get; set; }
            public string DisplayGr { get; set; }
            public int? SpecialityID { get; set; }
        }

        public class SpecialityItem
        {
            public int ID { get; set; }
            public string DisplaySp { get; set; }
            public string SpecialityCode { get; set; }
            public string NameSpeciality { get; set; }
        }

        public class PostItem
        {
            public int ID { get; set; }
            public string DisplayPost { get; set; }
        }

        public class ReaderItem
        {
            public int ReaderID { get; set; }
            public string CardNumber { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public int TypeID { get; set; }
            public string TypeName { get; set; }
            public int? GroupID { get; set; }
            public string GroupCode { get; set; }
            public int? PostID { get; set; }
            public string PostName { get; set; }
            public string Speciality { get; set; }
            public string PhoneNumber { get; set; }
            public string HomeAddress { get; set; }
            public DateTime MembershipStartDate { get; set; }
            public DateTime FormFillDate { get; set; }

            public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
            public string DisplayInfo => $"{CardNumber} - {FullName}";
        }

        private int? _pendingReaderId;
        private bool _isLoadingReader;

        public AddReader(int? readerId = null)
        {
            InitializeComponent();
            _pendingReaderId = readerId;

            LoadReaderType();
            LoadGroups();
            LoadSpecialities();
            LoadPosts();
            LoadReaders();

            TRCb.SelectionChanged += TRCb_SelectionChanged;
            SpCb.SelectionChanged += SpCb_SelectionChanged;
            MembershipStartDatePicker.SelectedDate = DateTime.Today;
            FormFillDatePicker.SelectedDate = DateTime.Today;
            UpdateVisibilityByReaderType();

            TRCb.Loaded += (s, e) => TryLoadPendingReader();
            SpCb.Loaded += (s, e) => TryLoadPendingReader();
            GrCb.Loaded += (s, e) => TryLoadPendingReader();
            PostCb.Loaded += (s, e) => TryLoadPendingReader();

            // Маска ввода ФИО
            LastNameTb.PreviewTextInput += NameField_PreviewTextInput;
            FirstNameTb.PreviewTextInput += NameField_PreviewTextInput;
            MiddleNameTb.PreviewTextInput += NameField_PreviewTextInput;
            LastNameTb.PreviewKeyDown += NameField_PreviewKeyDown;
            FirstNameTb.PreviewKeyDown += NameField_PreviewKeyDown;
            MiddleNameTb.PreviewKeyDown += NameField_PreviewKeyDown;

            // Генерация номера для нового читателя
            if (_pendingReaderId == null)
            {
                Loaded += async (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(CardNumberTb.Text))
                    {
                        string newNumber = await GetNextCardNumberAsync();
                        CardNumberTb.Text = newNumber;
                    }
                };
            }
        }

        // ==================== ОБРАБОТЧИК ВЫБОРА СПЕЦИАЛЬНОСТИ ====================
        private void SpCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpCb.SelectedItem is SpecialityItem selectedSpeciality)
            {
                FilterGroupsBySpeciality(selectedSpeciality.ID);
            }
            else
            {
                LoadGroups();
            }
        }

        private void FilterGroupsBySpeciality(int specialityId)
        {
            if (_allGroups == null) return;

            var filteredGroups = _allGroups.Where(g => g.SpecialityID == specialityId).ToList();
            GrCb.ItemsSource = filteredGroups;
            GrCb.DisplayMemberPath = "DisplayGr";
            GrCb.SelectedValuePath = "ID";

            if (filteredGroups.Count == 0)
            {
                GrCb.SelectedIndex = -1;
            }
        }

        // ==================== МАСКА ВВОДА ДЛЯ ФИО ====================
        private void NameField_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-ЯёЁ\s\-']$");
        }

        private void NameField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!Regex.IsMatch(text, @"^[a-zA-Zа-яА-ЯёЁ\s\-']+$"))
                    {
                        e.Handled = true;
                        CustomMessageBox.Show("Поле ФИО может содержать только буквы, пробелы и дефисы.", "Недопустимые символы",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                    }
                }
            }
        }

        // ==================== ГЕНЕРАЦИЯ НОМЕРА БИЛЕТА ====================
        private async Task<string> GetNextCardNumberAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    const string query = "SELECT MAX(CAST(CardNumber AS INT)) FROM ReaderForm WHERE ISNUMERIC(CardNumber) = 1";
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            var result = cmd.ExecuteScalar();
                            int maxNumber = (result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                            int nextNumber = maxNumber + 1;
                            return nextNumber.ToString();
                        }
                    }
                }
                catch
                {
                    return DateTime.Now.Ticks.ToString().Substring(0, 6);
                }
            });
        }

        // ==================== ПРОВЕРКА АКТИВНЫХ ВЫДАЧ ====================
        private async Task<bool> HasActiveLoansAsync(int readerId)
        {
            return await Task.Run(() =>
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string queryInd = "SELECT COUNT(*) FROM BookIssuance WHERE ReaderId = @ReaderId AND Status = 'Выдана'";
                    using (var cmd = new SqlCommand(queryInd, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", readerId);
                        if ((int)cmd.ExecuteScalar() > 0) return true;
                    }
                    string queryTeach = "SELECT COUNT(*) FROM GroupBookIssuance WHERE TeacherId = @ReaderId AND Status = 'Выдана'";
                    using (var cmd = new SqlCommand(queryTeach, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", readerId);
                        if ((int)cmd.ExecuteScalar() > 0) return true;
                    }
                    string queryStud = "SELECT COUNT(*) FROM GroupBookIssuance WHERE StudentId = @ReaderId AND Status = 'Выдана'";
                    using (var cmd = new SqlCommand(queryStud, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", readerId);
                        if ((int)cmd.ExecuteScalar() > 0) return true;
                    }
                    return false;
                }
            });
        }

        private void TryLoadPendingReader()
        {
            if (_pendingReaderId.HasValue && !_isLoadingReader && TRCb.Items.Count > 0)
            {
                _isLoadingReader = true;
                LoadReaderById(_pendingReaderId.Value);
                _pendingReaderId = null;
                _isLoadingReader = false;
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void LoadReaderById(int readerId)
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
                    WHERE r.ReaderID = @ReaderId";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ReaderId", readerId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                _currentReaderId = readerId;
                                CardNumberTb.Text = reader["CardNumber"]?.ToString() ?? "";
                                LastNameTb.Text = reader["LastName"]?.ToString() ?? "";
                                FirstNameTb.Text = reader["FirstName"]?.ToString() ?? "";
                                MiddleNameTb.Text = reader["MiddleName"]?.ToString() ?? "";
                                NumPhTb.Text = reader["PhoneNumber"]?.ToString() ?? "";
                                HomeAdressTb.Text = reader["HomeAddress"]?.ToString() ?? "";

                                if (reader["MembershipStartDate"] != DBNull.Value)
                                    MembershipStartDatePicker.SelectedDate = Convert.ToDateTime(reader["MembershipStartDate"]);
                                if (reader["FormFillDate"] != DBNull.Value)
                                    FormFillDatePicker.SelectedDate = Convert.ToDateTime(reader["FormFillDate"]);

                                int typeId = Convert.ToInt32(reader["TypeID"]);
                                SelectComboBoxItemById(TRCb, typeId);

                                string speciality = reader["Speciality"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(speciality))
                                {
                                    SelectSpecialityByDisplay(speciality);
                                }

                                UpdateVisibilityByReaderType();

                                if (reader["GroupID"] != DBNull.Value)
                                {
                                    int groupId = Convert.ToInt32(reader["GroupID"]);
                                    SelectComboBoxItemById(GrCb, groupId);
                                }
                                if (reader["Post"] != DBNull.Value)
                                {
                                    int postId = Convert.ToInt32(reader["Post"]);
                                    SelectComboBoxItemById(PostCb, postId);
                                }

                                AddReaderBtn.Visibility = Visibility.Collapsed;
                                UpdateReaderBtn.Visibility = Visibility.Visible;
                                DeleteReaderBtn.Visibility = Visibility.Visible;
                                PrintCardBtn.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки читателя: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SelectComboBoxItemById(ComboBox comboBox, int id)
        {
            if (comboBox.ItemsSource == null) return;
            foreach (var item in comboBox.Items)
            {
                int itemId = 0;
                if (item is ReaderTypeItem rt) itemId = rt.ID;
                else if (item is GroupItem g) itemId = g.ID;
                else if (item is SpecialityItem s) itemId = s.ID;
                else if (item is PostItem p) itemId = p.ID;

                if (itemId == id)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SelectSpecialityByDisplay(string displayText)
        {
            if (string.IsNullOrEmpty(displayText) || SpCb.ItemsSource == null) return;
            foreach (SpecialityItem item in SpCb.Items)
            {
                if (item.DisplaySp == displayText)
                {
                    SpCb.SelectedItem = item;
                    break;
                }
            }
        }

        private void LoadReaderType()
        {
            try
            {
                const string query = "SELECT TypeID, TypeName, Description FROM ReaderType ORDER BY TypeID";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<ReaderTypeItem>();
                        while (reader.Read())
                        {
                            items.Add(new ReaderTypeItem
                            {
                                ID = reader["TypeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TypeID"]),
                                DisplayTr = reader["TypeName"]?.ToString() ?? "",
                                TypeName = reader["TypeName"]?.ToString() ?? ""
                            });
                        }
                        TRCb.ItemsSource = items;
                        TRCb.DisplayMemberPath = "DisplayTr";
                        TRCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки типа читателя:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadGroups()
        {
            try
            {
                const string query = "SELECT GroupID, GroupCode, SpecialityID FROM Groups ORDER BY GroupCode";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<GroupItem>();
                        while (reader.Read())
                        {
                            int? specId = reader["SpecialityID"] != DBNull.Value ? Convert.ToInt32(reader["SpecialityID"]) : (int?)null;
                            items.Add(new GroupItem
                            {
                                ID = reader["GroupID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["GroupID"]),
                                DisplayGr = reader["GroupCode"]?.ToString() ?? "",
                                SpecialityID = specId
                            });
                        }
                        _allGroups = items;
                        GrCb.ItemsSource = items;
                        GrCb.DisplayMemberPath = "DisplayGr";
                        GrCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadSpecialities()
        {
            try
            {
                const string query = "SELECT SpecialityID, SpecialityCode, NameSpeciality FROM Speciality ORDER BY SpecialityCode";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<SpecialityItem>();
                        while (reader.Read())
                        {
                            items.Add(new SpecialityItem
                            {
                                ID = reader["SpecialityID"] != DBNull.Value ? Convert.ToInt32(reader["SpecialityID"]) : 0,
                                DisplaySp = $"{reader["SpecialityCode"]} {reader["NameSpeciality"]}",
                                SpecialityCode = reader["SpecialityCode"]?.ToString() ?? "",
                                NameSpeciality = reader["NameSpeciality"]?.ToString() ?? ""
                            });
                        }
                        SpCb.ItemsSource = items;
                        SpCb.DisplayMemberPath = "DisplaySp";
                        SpCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки специальностей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoadPosts()
        {
            try
            {
                const string query = "SELECT ID_Post, Post_Name FROM Post ORDER BY Post_Name";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<PostItem>();
                        while (reader.Read())
                        {
                            int id = reader["ID_Post"] != DBNull.Value ? Convert.ToInt32(reader["ID_Post"]) : 0;
                            string name = reader["Post_Name"]?.ToString() ?? "";
                            if (id > 0 && !string.IsNullOrEmpty(name))
                                items.Add(new PostItem { ID = id, DisplayPost = name });
                        }
                        PostCb.ItemsSource = items;
                        PostCb.DisplayMemberPath = "DisplayPost";
                        PostCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки должностей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
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
                        var items = new ObservableCollection<ReaderItem>();
                        while (reader.Read())
                        {
                            items.Add(new ReaderItem
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
                                MembershipStartDate = reader["MembershipStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["MembershipStartDate"]) : DateTime.MinValue,
                                FormFillDate = reader["FormFillDate"] != DBNull.Value ? Convert.ToDateTime(reader["FormFillDate"]) : DateTime.MinValue
                            });
                        }
                        _allReaders = items;
                        ReadersListView.ItemsSource = items;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки читателей: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void TRCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisibilityByReaderType();

            if (TRCb.SelectedItem is ReaderTypeItem selectedType)
            {
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                if (isStudent && SpCb.SelectedItem != null)
                {
                    FilterGroupsBySpeciality(((SpecialityItem)SpCb.SelectedItem).ID);
                }
                else if (!isStudent)
                {
                    GrCb.ItemsSource = null;
                    GrCb.SelectedIndex = -1;
                }
            }
        }

        private void UpdateVisibilityByReaderType()
        {
            if (TRCb.SelectedItem is ReaderTypeItem selectedType)
            {
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                // Видимость панели группы
                GrPanel.Visibility = isStudent ? Visibility.Visible : Visibility.Collapsed;

                // Видимость панели должности
                PostPanel.Visibility = isEmployee ? Visibility.Visible : Visibility.Collapsed;

                // Видимость панели специальности (только для студентов)
                // Используем FindVisualChildByName для поиска SpecialityPanel по имени
                var specialityPanel = FindVisualChildByName<StackPanel>(this, "SpecialityPanel");
                if (specialityPanel != null)
                {
                    specialityPanel.Visibility = isStudent ? Visibility.Visible : Visibility.Collapsed;
                }

                if (!isStudent) GrCb.SelectedIndex = -1;
                if (!isEmployee) PostCb.SelectedIndex = -1;
            }
            else
            {
                GrPanel.Visibility = Visibility.Collapsed;
                PostPanel.Visibility = Visibility.Collapsed;

                var specialityPanel = FindVisualChildByName<StackPanel>(this, "SpecialityPanel");
                if (specialityPanel != null)
                {
                    specialityPanel.Visibility = Visibility.Collapsed;
                }

                GrCb.SelectedIndex = -1;
                PostCb.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// Вспомогательный метод для поиска элемента по имени в визуальном дереве
        /// </summary>
        private T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void ClearForm()
        {
            _currentReaderId = null;
            CardNumberTb.Clear();
            LastNameTb.Clear();
            FirstNameTb.Clear();
            MiddleNameTb.Clear();
            TRCb.SelectedIndex = -1;
            SpCb.SelectedIndex = -1;
            GrCb.SelectedIndex = -1;
            PostCb.SelectedIndex = -1;
            NumPhTb.Text = "";
            HomeAdressTb.Clear();
            MembershipStartDatePicker.SelectedDate = DateTime.Today;
            FormFillDatePicker.SelectedDate = DateTime.Today;

            AddReaderBtn.Visibility = Visibility.Visible;
            UpdateReaderBtn.Visibility = Visibility.Collapsed;
            DeleteReaderBtn.Visibility = Visibility.Collapsed;
            UpdateVisibilityByReaderType();

            _ = Task.Run(async () =>
            {
                string newNumber = await GetNextCardNumberAsync();
                Dispatcher.Invoke(() => CardNumberTb.Text = newNumber);
            });
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CardNumberTb.Text))
            {
                CustomMessageBox.Show("Введите номер читательского билета.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                CardNumberTb.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(LastNameTb.Text))
            {
                CustomMessageBox.Show("Введите фамилию читателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                LastNameTb.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(FirstNameTb.Text))
            {
                CustomMessageBox.Show("Введите имя читателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                FirstNameTb.Focus();
                return false;
            }
            if (TRCb.SelectedItem == null)
            {
                CustomMessageBox.Show("Выберите тип читателя.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TRCb.Focus();
                return false;
            }
            if (!MembershipStartDatePicker.SelectedDate.HasValue)
            {
                CustomMessageBox.Show("Укажите дату начала членства в библиотеке.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                MembershipStartDatePicker.Focus();
                return false;
            }
            if (!FormFillDatePicker.SelectedDate.HasValue)
            {
                CustomMessageBox.Show("Укажите дату оформления читательского билета.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                FormFillDatePicker.Focus();
                return false;
            }
            return true;
        }

        private void AddReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;
            try
            {
                var selectedType = (ReaderTypeItem)TRCb.SelectedItem;
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                const string query = @"
                    INSERT INTO ReaderForm (
                        CardNumber, LastName, FirstName, MiddleName,
                        TypeID, GroupID, Post, Speciality,
                        PhoneNumber, HomeAddress, MembershipStartDate, FormFillDate
                    ) VALUES (
                        @CardNumber, @LastName, @FirstName, @MiddleName,
                        @TypeID, @GroupID, @Post, @Speciality,
                        @PhoneNumber, @HomeAddress, @MembershipStartDate, @FormFillDate
                    )";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CardNumber", CardNumberTb.Text.Trim());
                        command.Parameters.AddWithValue("@LastName", LastNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@FirstName", FirstNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTb.Text) ? (object)DBNull.Value : MiddleNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@TypeID", selectedType.ID);
                        command.Parameters.AddWithValue("@GroupID", (isStudent && GrCb.SelectedValue != null) ? GrCb.SelectedValue : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Post", (isEmployee && PostCb.SelectedValue != null) ? PostCb.SelectedValue : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Speciality", (isStudent && SpCb.SelectedValue != null) ? ((SpecialityItem)SpCb.SelectedItem)?.DisplaySp : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(NumPhTb.Text) ? (object)DBNull.Value : NumPhTb.Text);
                        command.Parameters.AddWithValue("@HomeAddress", string.IsNullOrWhiteSpace(HomeAdressTb.Text) ? (object)DBNull.Value : HomeAdressTb.Text.Trim());
                        command.Parameters.AddWithValue("@MembershipStartDate", MembershipStartDatePicker.SelectedDate.Value);
                        command.Parameters.AddWithValue("@FormFillDate", FormFillDatePicker.SelectedDate.Value);

                        int result = command.ExecuteNonQuery();
                        if (result > 0)
                        {
                            CustomMessageBox.Show("Читатель успешно добавлен.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearForm();
                            LoadReaders();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                CustomMessageBox.Show("Читатель с таким номером билета уже существует.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при добавлении читателя: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void UpdateReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReaderId.HasValue)
            {
                CustomMessageBox.Show("Выберите читателя для редактирования.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }
            if (!ValidateForm()) return;

            try
            {
                var selectedType = (ReaderTypeItem)TRCb.SelectedItem;
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                const string query = @"
                    UPDATE ReaderForm 
                    SET CardNumber = @CardNumber,
                        LastName = @LastName,
                        FirstName = @FirstName,
                        MiddleName = @MiddleName,
                        TypeID = @TypeID,
                        GroupID = @GroupID,
                        Post = @Post,
                        Speciality = @Speciality,
                        PhoneNumber = @PhoneNumber,
                        HomeAddress = @HomeAddress,
                        MembershipStartDate = @MembershipStartDate,
                        FormFillDate = @FormFillDate
                    WHERE ReaderID = @ReaderID";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ReaderID", _currentReaderId.Value);
                        command.Parameters.AddWithValue("@CardNumber", CardNumberTb.Text.Trim());
                        command.Parameters.AddWithValue("@LastName", LastNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@FirstName", FirstNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTb.Text) ? (object)DBNull.Value : MiddleNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@TypeID", selectedType.ID);
                        command.Parameters.AddWithValue("@GroupID", (isStudent && GrCb.SelectedValue != null) ? GrCb.SelectedValue : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Post", (isEmployee && PostCb.SelectedValue != null) ? PostCb.SelectedValue : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Speciality", (isStudent && SpCb.SelectedValue != null) ? ((SpecialityItem)SpCb.SelectedItem)?.DisplaySp : (object)DBNull.Value);
                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(NumPhTb.Text) ? (object)DBNull.Value : NumPhTb.Text);
                        command.Parameters.AddWithValue("@HomeAddress", string.IsNullOrWhiteSpace(HomeAdressTb.Text) ? (object)DBNull.Value : HomeAdressTb.Text.Trim());
                        command.Parameters.AddWithValue("@MembershipStartDate", MembershipStartDatePicker.SelectedDate.Value);
                        command.Parameters.AddWithValue("@FormFillDate", FormFillDatePicker.SelectedDate.Value);

                        int result = command.ExecuteNonQuery();
                        if (result > 0)
                        {
                            CustomMessageBox.Show("Данные читателя успешно обновлены.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearForm();
                            LoadReaders();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                CustomMessageBox.Show("Читатель с таким номером билета уже существует.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при обновлении читателя: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async Task NullifyGroupLoansReferencesAsync(int readerId)
        {
            await Task.Run(() =>
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("UPDATE GroupBookIssuance SET TeacherId = NULL WHERE TeacherId = @ReaderId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", readerId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SqlCommand("UPDATE GroupBookIssuance SET StudentId = NULL WHERE StudentId = @ReaderId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ReaderId", readerId);
                        cmd.ExecuteNonQuery();
                    }
                }
            });
        }

        private async void DeleteReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE GroupBookIssuance SET TeacherId = NULL WHERE TeacherId = @ReaderId", conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId.Value);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SqlCommand("UPDATE GroupBookIssuance SET StudentId = NULL WHERE StudentId = @ReaderId", conn))
                {
                    cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            if (!_currentReaderId.HasValue)
            {
                CustomMessageBox.Show("Выберите читателя для удаления.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            bool hasActive = await HasActiveLoansAsync(_currentReaderId.Value);
            if (hasActive)
            {
                CustomMessageBox.Show("Невозможно удалить читателя: у него есть невозвращённые книги.\n" +
                    "Сначала примите возврат всех выданных экземпляров.",
                    "Удаление запрещено", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить этого читателя?\n" +
                "Все исторические записи о его выдачах сохранятся (без привязки к читателю).",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM ReaderForm WHERE ReaderID = @ReaderID";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ReaderID", _currentReaderId.Value);
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            CustomMessageBox.Show("Читатель успешно удалён. Записи о выдачах сохранены.", "Успешно",
                                CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearForm();
                            LoadReaders();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                CustomMessageBox.Show("Невозможно удалить читателя из-за связанных записей. Возможно, есть активные выдачи.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ReadersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderItem selectedReader)
            {
                _currentReaderId = selectedReader.ReaderID;
                CardNumberTb.Text = selectedReader.CardNumber;
                LastNameTb.Text = selectedReader.LastName;
                FirstNameTb.Text = selectedReader.FirstName;
                MiddleNameTb.Text = selectedReader.MiddleName;
                NumPhTb.Text = selectedReader.PhoneNumber;
                HomeAdressTb.Text = selectedReader.HomeAddress;
                MembershipStartDatePicker.SelectedDate = selectedReader.MembershipStartDate;
                FormFillDatePicker.SelectedDate = selectedReader.FormFillDate;

                foreach (ReaderTypeItem item in TRCb.Items)
                    if (item.ID == selectedReader.TypeID) { TRCb.SelectedItem = item; break; }

                if (!string.IsNullOrEmpty(selectedReader.Speciality))
                {
                    foreach (SpecialityItem item in SpCb.Items)
                        if (item.DisplaySp == selectedReader.Speciality) { SpCb.SelectedItem = item; break; }
                }
                else SpCb.SelectedIndex = -1;

                UpdateVisibilityByReaderType();

                if (selectedReader.GroupID.HasValue) GrCb.SelectedValue = selectedReader.GroupID.Value;
                else GrCb.SelectedIndex = -1;

                if (selectedReader.PostID.HasValue) PostCb.SelectedValue = selectedReader.PostID.Value;
                else PostCb.SelectedIndex = -1;

                AddReaderBtn.Visibility = Visibility.Collapsed;
                UpdateReaderBtn.Visibility = Visibility.Visible;
                DeleteReaderBtn.Visibility = Visibility.Visible;
                PrintCardBtn.Visibility = Visibility.Visible;
            }
        }

        private void AddSp_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddSpeciality());
        private void AddGr_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddGroup());
        private void AddPost_Click(object sender, RoutedEventArgs e) => NavigationService.Navigate(new AddPost());

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecialities();
            LoadGroups();
            LoadReaderType();
            LoadPosts();
            LoadReaders();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchReaderTb.Text == "Поиск по фамилии или номеру билета...")
            {
                SearchReaderTb.Text = "";
                SearchReaderTb.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchReaderTb.Text))
            {
                SearchReaderTb.Text = "Поиск по фамилии или номеру билета...";
                SearchReaderTb.Foreground = Brushes.Gray;
            }
        }

        private void SearchReader_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchReaderTb.Text == "Поиск по фамилии или номеру билета...") return;
            string searchTerm = SearchReaderTb.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(searchTerm))
                ReadersListView.ItemsSource = _allReaders;
            else
                ReadersListView.ItemsSource = _allReaders.Where(r => r.LastName.ToLower().Contains(searchTerm) ||
                                                                      r.FirstName.ToLower().Contains(searchTerm) ||
                                                                      r.CardNumber.ToLower().Contains(searchTerm)).ToList();
        }

        private void PrintCardBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReaderId.HasValue)
            {
                CustomMessageBox.Show("Выберите читателя для печати билета.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new Window
                {
                    Title = "Печать читательского билета",
                    Width = 300,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    WindowStyle = WindowStyle.ToolWindow,
                };

                var panel = new StackPanel { Margin = new Thickness(20) };
                panel.Children.Add(new TextBlock { Text = "Выберите количество копий:", FontSize = 14, Margin = new Thickness(0, 0, 0, 15) });

                var comboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 15), Height = 30, SelectedIndex = 0 };
                comboBox.Items.Add("1 копия");
                comboBox.Items.Add("2 копии");
                panel.Children.Add(comboBox);

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var okButton = new Button { Content = "Печать", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
                var cancelButton = new Button { Content = "Отмена", Width = 80, Height = 30, IsCancel = true };
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                panel.Children.Add(buttonPanel);
                dialog.Content = panel;

                okButton.Click += async (s, args) =>
                {
                    try
                    {
                        byte[] pdfBytes;
                        string readerFullName = "";
                        string readerCardNumber = "";

                        try
                        {
                            using (var conn = new SqlConnection(connectionString))
                            {
                                conn.Open();
                                using (var cmd = new SqlCommand("SELECT LastName, FirstName, MiddleName, CardNumber FROM ReaderForm WHERE ReaderID = @ReaderId", conn))
                                {
                                    cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId.Value);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            string lastName = reader["LastName"]?.ToString() ?? "";
                                            string firstName = reader["FirstName"]?.ToString() ?? "";
                                            string middleName = reader["MiddleName"]?.ToString() ?? "";
                                            readerCardNumber = reader["CardNumber"]?.ToString() ?? "";
                                            readerFullName = $"{lastName}_{firstName}_{middleName}".Replace(" ", "_").Replace("__", "_").Trim('_');
                                        }
                                    }
                                }
                            }
                        }
                        catch { readerFullName = $"reader_{_currentReaderId}"; }

                        if (comboBox.SelectedIndex == 1)
                            pdfBytes = Services.ReaderCardPdfService.GenerateReaderCardPdfTwoCopies(_currentReaderId.Value);
                        else
                            pdfBytes = Services.ReaderCardPdfService.GenerateReaderCardPdf(_currentReaderId.Value);

                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string fileName = $"Читательский_билет_{readerFullName}_{readerCardNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                        string fullPath = System.IO.Path.Combine(desktop, fileName);
                        System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                        CustomMessageBox.Show($"Читательский билет сохранён:\n{fullPath}", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                };
                cancelButton.Click += (s, args) => dialog.Close();
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ReadersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderItem selectedReader)
            {
                _currentReaderId = selectedReader.ReaderID;
                CardNumberTb.Text = selectedReader.CardNumber;
                LastNameTb.Text = selectedReader.LastName;
                FirstNameTb.Text = selectedReader.FirstName;
                MiddleNameTb.Text = selectedReader.MiddleName ?? "";
                NumPhTb.Text = selectedReader.PhoneNumber ?? "";
                HomeAdressTb.Text = selectedReader.HomeAddress ?? "";
                MembershipStartDatePicker.SelectedDate = selectedReader.MembershipStartDate;
                FormFillDatePicker.SelectedDate = selectedReader.FormFillDate;

                foreach (ReaderTypeItem item in TRCb.Items)
                    if (item.ID == selectedReader.TypeID) { TRCb.SelectedItem = item; break; }

                if (!string.IsNullOrEmpty(selectedReader.Speciality))
                {
                    foreach (SpecialityItem item in SpCb.Items)
                        if (item.DisplaySp == selectedReader.Speciality) { SpCb.SelectedItem = item; break; }
                }
                else SpCb.SelectedIndex = -1;

                UpdateVisibilityByReaderType();

                if (selectedReader.GroupID.HasValue) GrCb.SelectedValue = selectedReader.GroupID.Value;
                else GrCb.SelectedIndex = -1;

                if (selectedReader.PostID.HasValue) PostCb.SelectedValue = selectedReader.PostID.Value;
                else PostCb.SelectedIndex = -1;

                AddReaderBtn.Visibility = Visibility.Collapsed;
                UpdateReaderBtn.Visibility = Visibility.Visible;
                DeleteReaderBtn.Visibility = Visibility.Visible;
                PrintCardBtn.Visibility = Visibility.Visible;

                var result = CustomMessageBox.Show($"Выбран читатель: {selectedReader.LastName} {selectedReader.FirstName}\n\nХотите распечатать читательский билет?",
                    "Печать билета", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);
                if (result == CustomMessageBoxResult.Yes)
                    PrintCardBtn_Click(sender, e);
            }
        }
    }
}