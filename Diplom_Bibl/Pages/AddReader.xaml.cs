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

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddReader.xaml
    /// </summary>
    public partial class AddReader : Page
    {
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
        private System.Collections.ObjectModel.ObservableCollection<ReaderItem> _allReaders;
        private int? _currentReaderId = null;

        // Классы для представления данных
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
        private int? _pendingReaderId = null;
        private bool _isLoadingReader = false;

        public AddReader(int? readerId = null)
        {
            InitializeComponent();

            // Сохраняем ID для отложенной загрузки
            _pendingReaderId = readerId;

            // Загружаем данные для комбобоксов
            LoadReaderType();
            LoadGroups();
            LoadSpecialities();
            LoadPosts();
            LoadReaders();

            // Подписываемся на изменение выбора типа читателя
            TRCb.SelectionChanged += TRCb_SelectionChanged;

            // Устанавливаем даты по умолчанию
            MembershipStartDatePicker.SelectedDate = DateTime.Today;
            FormFillDatePicker.SelectedDate = DateTime.Today;

            // Изначально скрываем поля группы и должности
            UpdateVisibilityByReaderType();

            // Подписываемся на события загрузки комбобоксов
            TRCb.Loaded += (s, e) => TryLoadPendingReader();
            SpCb.Loaded += (s, e) => TryLoadPendingReader();
            GrCb.Loaded += (s, e) => TryLoadPendingReader();
            PostCb.Loaded += (s, e) => TryLoadPendingReader();
        }

        private void TryLoadPendingReader()
        {
            // Если есть ожидающий ID и все комбобоксы загружены
            if (_pendingReaderId.HasValue && !_isLoadingReader)
            {
                // Проверяем, что все комбобоксы имеют элементы
                if (TRCb.Items.Count > 0 && SpCb.Items.Count > 0)
                {
                    _isLoadingReader = true;
                    LoadReaderById(_pendingReaderId.Value);
                    _pendingReaderId = null;
                    _isLoadingReader = false;
                }
            }
        }
        
        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.GoBack();
            }
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
                                // Сохраняем ID
                                _currentReaderId = readerId;

                                // Заполняем основные поля
                                CardNumberTb.Text = reader["CardNumber"]?.ToString() ?? "";
                                LastNameTb.Text = reader["LastName"]?.ToString() ?? "";
                                FirstNameTb.Text = reader["FirstName"]?.ToString() ?? "";
                                MiddleNameTb.Text = reader["MiddleName"]?.ToString() ?? "";

                                // Номер телефона и адрес
                                NumPhTb.Text = reader["PhoneNumber"]?.ToString() ?? "";
                                HomeAdressTb.Text = reader["HomeAddress"]?.ToString() ?? "";

                                // Даты
                                if (reader["MembershipStartDate"] != DBNull.Value)
                                    MembershipStartDatePicker.SelectedDate = Convert.ToDateTime(reader["MembershipStartDate"]);

                                if (reader["FormFillDate"] != DBNull.Value)
                                    FormFillDatePicker.SelectedDate = Convert.ToDateTime(reader["FormFillDate"]);

                                // Выбираем тип читателя
                                int typeId = Convert.ToInt32(reader["TypeID"]);
                                SelectComboBoxItemById(TRCb, typeId);

                                // Выбираем специальность
                                string speciality = reader["Speciality"]?.ToString() ?? "";
                                SelectSpecialityByDisplay(speciality);

                                // Обновляем видимость полей после выбора типа
                                UpdateVisibilityByReaderType();

                                // Выбираем группу
                                if (reader["GroupID"] != DBNull.Value)
                                {
                                    int groupId = Convert.ToInt32(reader["GroupID"]);
                                    SelectComboBoxItemById(GrCb, groupId);
                                }

                                // Выбираем должность
                                if (reader["Post"] != DBNull.Value)
                                {
                                    int postId = Convert.ToInt32(reader["Post"]);
                                    SelectComboBoxItemById(PostCb, postId);
                                }

                                // Переключаем кнопки
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
                MessageBox.Show($"Ошибка загрузки читателя: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectComboBoxItemById(ComboBox comboBox, int id)
        {
            if (comboBox.ItemsSource != null)
            {
                foreach (var item in comboBox.Items)
                {
                    // Получаем значение ID через рефлексию или приведение типа
                    int itemId = 0;

                    if (item is ReaderTypeItem readerType)
                        itemId = readerType.ID;
                    else if (item is GroupItem group)
                        itemId = group.ID;
                    else if (item is PostItem post)
                        itemId = post.ID;
                    else if (item is ComboBoxItem comboItem && comboItem.Tag != null)
                        itemId = Convert.ToInt32(comboItem.Tag);

                    if (itemId == id)
                    {
                        comboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SelectSpecialityByDisplay(string displayText)
        {
            if (string.IsNullOrEmpty(displayText) || SpCb.ItemsSource == null)
                return;

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
                const string query = @"
                    SELECT TypeID, TypeName, Description 
                    FROM ReaderType 
                    ORDER BY TypeID";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<ReaderTypeItem>();

                        while (reader.Read())
                        {
                            int id = reader["TypeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TypeID"]);
                            string name = reader["TypeName"]?.ToString() ?? "";
                            string description = reader["Description"]?.ToString() ?? "";

                            items.Add(new ReaderTypeItem
                            {
                                ID = id,
                                DisplayTr = name,
                                TypeName = name
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
                MessageBox.Show($"Ошибка загрузки типа читателя:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGroups()
        {
            try
            {
                const string query = @"
            SELECT GroupID, GroupCode, SpecialityID 
            FROM Groups 
            ORDER BY GroupCode";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<GroupItem>();

                        while (reader.Read())
                        {
                            int id = reader["GroupID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["GroupID"]);
                            string code = reader["GroupCode"]?.ToString() ?? "";

                            // Исправление для C# 7.3
                            int? specId = null;
                            if (reader["SpecialityID"] != DBNull.Value)
                            {
                                specId = Convert.ToInt32(reader["SpecialityID"]);
                            }

                            items.Add(new GroupItem
                            {
                                ID = id,
                                DisplayGr = code,
                                SpecialityID = specId
                            });
                        }

                        GrCb.ItemsSource = items;
                        GrCb.DisplayMemberPath = "DisplayGr";
                        GrCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSpecialities()
        {
            try
            {
                const string query = @"
            SELECT SpecialityID, SpecialityCode, NameSpeciality 
            FROM Speciality 
            ORDER BY SpecialityCode";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<SpecialityItem>();

                        while (reader.Read())
                        {
                            // Исправление для C# 7.3
                            int id = 0;
                            if (reader["SpecialityID"] != DBNull.Value)
                            {
                                id = Convert.ToInt32(reader["SpecialityID"]);
                            }

                            string code = reader["SpecialityCode"]?.ToString() ?? "";
                            string name = reader["NameSpeciality"]?.ToString() ?? "";

                            items.Add(new SpecialityItem
                            {
                                ID = id,
                                DisplaySp = $"{code} {name}",
                                SpecialityCode = code,
                                NameSpeciality = name
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
                MessageBox.Show($"Ошибка загрузки специальностей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadPosts()
        {
            try
            {
                const string query = @"
            SELECT ID_Post, Post_Name 
            FROM Post 
            ORDER BY Post_Name";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<PostItem>();

                        while (reader.Read())
                        {
                            int id = 0;
                            if (reader["ID_Post"] != DBNull.Value)
                            {
                                id = Convert.ToInt32(reader["ID_Post"]);
                            }

                            string name = reader["Post_Name"]?.ToString() ?? "";

                            if (id > 0 && !string.IsNullOrEmpty(name))
                            {
                                items.Add(new PostItem
                                {
                                    ID = id,
                                    DisplayPost = name
                                });
                            }
                        }

                        PostCb.ItemsSource = items;
                        PostCb.DisplayMemberPath = "DisplayPost";
                        PostCb.SelectedValuePath = "ID";

                        // Проверяем, есть ли элементы
                        if (items.Count == 0)
                        {
                            // Можно показать сообщение или добавить тестовые данные
                            System.Diagnostics.Debug.WriteLine("Таблица Post пуста");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки должностей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            // Безопасное чтение ReaderID
                            int readerId = 0;
                            if (reader["ReaderID"] != DBNull.Value)
                            {
                                readerId = Convert.ToInt32(reader["ReaderID"]);
                            }

                            // Безопасное чтение TypeID
                            int typeId = 0;
                            if (reader["TypeID"] != DBNull.Value)
                            {
                                typeId = Convert.ToInt32(reader["TypeID"]);
                            }

                            // Безопасное чтение GroupID (может быть NULL)
                            int? groupId = null;
                            if (reader["GroupID"] != DBNull.Value)
                            {
                                groupId = Convert.ToInt32(reader["GroupID"]);
                            }

                            // Безопасное чтение Post (может быть NULL)
                            int? postId = null;
                            if (reader["Post"] != DBNull.Value)
                            {
                                postId = Convert.ToInt32(reader["Post"]);
                            }

                            // Безопасное чтение MembershipStartDate
                            DateTime membershipStartDate = DateTime.MinValue;
                            if (reader["MembershipStartDate"] != DBNull.Value)
                            {
                                membershipStartDate = Convert.ToDateTime(reader["MembershipStartDate"]);
                            }

                            // Безопасное чтение FormFillDate
                            DateTime formFillDate = DateTime.MinValue;
                            if (reader["FormFillDate"] != DBNull.Value)
                            {
                                formFillDate = Convert.ToDateTime(reader["FormFillDate"]);
                            }

                            items.Add(new ReaderItem
                            {
                                ReaderID = readerId,
                                CardNumber = reader["CardNumber"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleName = reader["MiddleName"]?.ToString() ?? "",
                                TypeID = typeId,
                                TypeName = reader["TypeName"]?.ToString() ?? "",
                                GroupID = groupId,
                                GroupCode = reader["GroupCode"]?.ToString() ?? "",
                                PostID = postId,
                                PostName = reader["Post_Name"]?.ToString() ?? "",
                                Speciality = reader["Speciality"]?.ToString() ?? "",
                                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                                HomeAddress = reader["HomeAddress"]?.ToString() ?? "",
                                MembershipStartDate = membershipStartDate,
                                FormFillDate = formFillDate
                            });
                        }

                        _allReaders = items;
                        ReadersListView.ItemsSource = items;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки читателей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TRCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisibilityByReaderType();
        }

        private void UpdateVisibilityByReaderType()
        {
            if (TRCb.SelectedItem is ReaderTypeItem selectedType)
            {
                // Проверяем тип читателя (предполагаем, что "Студент" и "Сотрудник" - это названия)
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                // Показываем/скрываем соответствующие элементы
                GrPanel.Visibility = isStudent ? Visibility.Visible : Visibility.Collapsed;
                PostPanel.Visibility = isEmployee ? Visibility.Visible : Visibility.Collapsed;

                // Очищаем значения, если они не нужны
                if (!isStudent)
                {
                    GrCb.SelectedIndex = -1;
                }
                if (!isEmployee)
                {
                    PostCb.SelectedIndex = -1;
                }
            }
            else
            {
                GrPanel.Visibility = Visibility.Collapsed;
                PostPanel.Visibility = Visibility.Collapsed;
                GrCb.SelectedIndex = -1;
                PostCb.SelectedIndex = -1;
            }
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

            // Переключаем кнопки
            AddReaderBtn.Visibility = Visibility.Visible;
            UpdateReaderBtn.Visibility = Visibility.Collapsed;
            DeleteReaderBtn.Visibility = Visibility.Collapsed;

            // Обновляем видимость полей
            UpdateVisibilityByReaderType();
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CardNumberTb.Text))
            {
                MessageBox.Show("Введите номер читательского билета.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CardNumberTb.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(LastNameTb.Text))
            {
                MessageBox.Show("Введите фамилию читателя.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                LastNameTb.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FirstNameTb.Text))
            {
                MessageBox.Show("Введите имя читателя.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                FirstNameTb.Focus();
                return false;
            }

            if (TRCb.SelectedItem == null)
            {
                MessageBox.Show("Выберите тип читателя.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TRCb.Focus();
                return false;
            }

            if (!MembershipStartDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите дату начала членства в библиотеке.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                MembershipStartDatePicker.Focus();
                return false;
            }

            if (!FormFillDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите дату оформления читательского билета.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                FormFillDatePicker.Focus();
                return false;
            }

            return true;
        }

        private void AddReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                var selectedType = (ReaderTypeItem)TRCb.SelectedItem;
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                string query = @"
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
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTb.Text) ? DBNull.Value : (object)MiddleNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@TypeID", selectedType.ID);

                        // Группа (только для студентов)
                        if (isStudent && GrCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@GroupID", GrCb.SelectedValue);
                        else
                            command.Parameters.AddWithValue("@GroupID", DBNull.Value);

                        // Должность (только для сотрудников)
                        if (isEmployee && PostCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@Post", PostCb.SelectedValue);
                        else
                            command.Parameters.AddWithValue("@Post", DBNull.Value);

                        // Специальность (для студентов)
                        if (isStudent && SpCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@Speciality", ((SpecialityItem)SpCb.SelectedItem)?.DisplaySp);
                        else
                            command.Parameters.AddWithValue("@Speciality", DBNull.Value);

                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(NumPhTb.Text) ? DBNull.Value : (object)NumPhTb.Text);
                        command.Parameters.AddWithValue("@HomeAddress", string.IsNullOrWhiteSpace(HomeAdressTb.Text) ? DBNull.Value : (object)HomeAdressTb.Text.Trim());
                        command.Parameters.AddWithValue("@MembershipStartDate", MembershipStartDatePicker.SelectedDate.Value);
                        command.Parameters.AddWithValue("@FormFillDate", FormFillDatePicker.SelectedDate.Value);

                        int result = command.ExecuteNonQuery();

                        if (result > 0)
                        {
                            MessageBox.Show("Читатель успешно добавлен.", "Успешно",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearForm();
                            LoadReaders();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) // Нарушение уникальности
                {
                    MessageBox.Show("Читатель с таким номером билета уже существует.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка базы данных: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении читателя: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReaderId.HasValue)
            {
                MessageBox.Show("Выберите читателя для редактирования.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm())
                return;

            try
            {
                var selectedType = (ReaderTypeItem)TRCb.SelectedItem;
                bool isStudent = selectedType.TypeName?.ToLower().Contains("студент") ?? false;
                bool isEmployee = selectedType.TypeName?.ToLower().Contains("сотрудник") ?? false;

                string query = @"
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
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTb.Text) ? DBNull.Value : (object)MiddleNameTb.Text.Trim());
                        command.Parameters.AddWithValue("@TypeID", selectedType.ID);

                        // Группа (только для студентов)
                        if (isStudent && GrCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@GroupID", GrCb.SelectedValue);
                        else
                            command.Parameters.AddWithValue("@GroupID", DBNull.Value);

                        // Должность (только для сотрудников)
                        if (isEmployee && PostCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@Post", PostCb.SelectedValue);
                        else
                            command.Parameters.AddWithValue("@Post", DBNull.Value);

                        // Специальность (для студентов)
                        if (isStudent && SpCb.SelectedValue != null)
                            command.Parameters.AddWithValue("@Speciality", ((SpecialityItem)SpCb.SelectedItem)?.DisplaySp);
                        else
                            command.Parameters.AddWithValue("@Speciality", DBNull.Value);

                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(NumPhTb.Text) ? DBNull.Value : (object)NumPhTb.Text);
                        command.Parameters.AddWithValue("@HomeAddress", string.IsNullOrWhiteSpace(HomeAdressTb.Text) ? DBNull.Value : (object)HomeAdressTb.Text.Trim());
                        command.Parameters.AddWithValue("@MembershipStartDate", MembershipStartDatePicker.SelectedDate.Value);
                        command.Parameters.AddWithValue("@FormFillDate", FormFillDatePicker.SelectedDate.Value);

                        int result = command.ExecuteNonQuery();

                        if (result > 0)
                        {
                            MessageBox.Show("Данные читателя успешно обновлены.", "Успешно",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearForm();
                            LoadReaders();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627)
                {
                    MessageBox.Show("Читатель с таким номером билета уже существует.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка базы данных: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении читателя: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteReaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReaderId.HasValue)
            {
                MessageBox.Show("Выберите читателя для удаления.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить этого читателя?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string query = "DELETE FROM ReaderForm WHERE ReaderID = @ReaderID";

                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@ReaderID", _currentReaderId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Читатель успешно удален.", "Успешно",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                ClearForm();
                                LoadReaders();
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 547) // Нарушение внешнего ключа
                    {
                        MessageBox.Show("Невозможно удалить читателя, так как у него есть связанные записи (выданные книги и т.д.).",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка базы данных: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении читателя: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReadersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderItem selectedReader)
            {
                _currentReaderId = selectedReader.ReaderID;

                // Заполняем форму данными выбранного читателя
                CardNumberTb.Text = selectedReader.CardNumber;
                LastNameTb.Text = selectedReader.LastName;
                FirstNameTb.Text = selectedReader.FirstName;
                MiddleNameTb.Text = selectedReader.MiddleName;

                // Выбираем тип читателя
                foreach (ReaderTypeItem item in TRCb.Items)
                {
                    if (item.ID == selectedReader.TypeID)
                    {
                        TRCb.SelectedItem = item;
                        break;
                    }
                }

                // Выбираем специальность (если есть)
                if (!string.IsNullOrEmpty(selectedReader.Speciality))
                {
                    foreach (SpecialityItem item in SpCb.Items)
                    {
                        if (item.DisplaySp == selectedReader.Speciality)
                        {
                            SpCb.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Выбираем группу (если есть)
                if (selectedReader.GroupID.HasValue)
                {
                    GrCb.SelectedValue = selectedReader.GroupID.Value;
                }

                // Выбираем должность (если есть)
                if (selectedReader.PostID.HasValue)
                {
                    PostCb.SelectedValue = selectedReader.PostID.Value;
                }

                // Заполняем остальные поля
                NumPhTb.Text = selectedReader.PhoneNumber;
                HomeAdressTb.Text = selectedReader.HomeAddress;
                MembershipStartDatePicker.SelectedDate = selectedReader.MembershipStartDate;
                FormFillDatePicker.SelectedDate = selectedReader.FormFillDate;

                // Обновляем видимость полей
                UpdateVisibilityByReaderType();

                // Переключаем кнопки
                AddReaderBtn.Visibility = Visibility.Collapsed;
                UpdateReaderBtn.Visibility = Visibility.Visible;
                DeleteReaderBtn.Visibility = Visibility.Visible;
            }
        }

        private void AddSp_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddSpeciality());
        }

        private void AddGr_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddGroup());
        }

        private void AddPost_Click(object sender, RoutedEventArgs e)
        {
            // Добавьте навигацию на страницу добавления должности, если она существует
            NavigationService.Navigate(new AddPost());
            //MessageBox.Show("Функция добавления должности будет доступна позже.",
            //    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecialities();
            LoadGroups();
            LoadReaderType();
            LoadPosts();
            LoadReaders();
        }
        // Добавьте эти методы в класс AddReader

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
            if (SearchReaderTb.Text == "Поиск по фамилии или номеру билета...")
                return;

            string searchTerm = SearchReaderTb.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchTerm))
            {
                ReadersListView.ItemsSource = _allReaders;
            }
            else
            {
                var filtered = _allReaders.Cast<ReaderItem>()
                    .Where(r => r.LastName.ToLower().Contains(searchTerm) ||
                               r.FirstName.ToLower().Contains(searchTerm) ||
                               r.CardNumber.ToLower().Contains(searchTerm))
                    .ToList();

                ReadersListView.ItemsSource = filtered;
            }
        }

        private void PrintCardBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReaderId.HasValue)
            {
                MessageBox.Show("Выберите читателя для печати билета.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Создаем диалог выбора количества копий
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

                panel.Children.Add(new TextBlock
                {
                    Text = "Выберите количество копий:",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                var comboBox = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Height = 30,
                    SelectedIndex = 0
                };
                comboBox.Items.Add("1 копия");
                comboBox.Items.Add("2 копии");
                panel.Children.Add(comboBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var okButton = new Button
                {
                    Content = "Печать",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new Button
                {
                    Content = "Отмена",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                panel.Children.Add(buttonPanel);

                dialog.Content = panel;

                okButton.Click += (s, args) =>
                {
                    try
                    {
                        byte[] pdfBytes;

                        // Получаем данные читателя для формирования имени файла
                        string readerFullName = "";
                        string readerCardNumber = "";

                        try
                        {
                            using (var conn = new SqlConnection(connectionString))
                            {
                                conn.Open();
                                using (var cmd = new SqlCommand(
                                    "SELECT LastName, FirstName, MiddleName, CardNumber FROM ReaderForm WHERE ReaderID = @ReaderId",
                                    conn))
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

                                            // Формируем ФИО (Фамилия Имя Отчество)
                                            readerFullName = $"{lastName}_{firstName}_{middleName}".Replace(" ", "_").Replace("__", "_").Trim('_');
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Если не удалось получить данные, используем ID
                            readerFullName = $"reader_{_currentReaderId}";
                        }

                        if (comboBox.SelectedIndex == 1) // 2 копии
                        {
                            pdfBytes = Services.ReaderCardPdfService.GenerateReaderCardPdfTwoCopies(_currentReaderId.Value);
                        }
                        else // 1 копия
                        {
                            pdfBytes = Services.ReaderCardPdfService.GenerateReaderCardPdf(_currentReaderId.Value);
                        }

                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string fileName = $"Читательский_билет_{readerFullName}_{readerCardNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                        string fullPath = System.IO.Path.Combine(desktop, fileName);

                        System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                        MessageBox.Show($"Читательский билет сохранён:\n{fullPath}",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при печати: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                cancelButton.Click += (s, args) => dialog.Close();

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Добавьте в класс AddReader этот обработчик

        private void ReadersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReadersListView.SelectedItem is ReaderItem selectedReader)
            {
                try
                {
                    // Сохраняем ID выбранного читателя
                    _currentReaderId = selectedReader.ReaderID;

                    // Заполняем основные поля
                    CardNumberTb.Text = selectedReader.CardNumber;
                    LastNameTb.Text = selectedReader.LastName;
                    FirstNameTb.Text = selectedReader.FirstName;
                    MiddleNameTb.Text = selectedReader.MiddleName ?? "";
                    NumPhTb.Text = selectedReader.PhoneNumber ?? "";
                    HomeAdressTb.Text = selectedReader.HomeAddress ?? "";

                    // Заполняем даты
                    MembershipStartDatePicker.SelectedDate = selectedReader.MembershipStartDate;
                    FormFillDatePicker.SelectedDate = selectedReader.FormFillDate;

                    // Выбираем тип читателя в комбобоксе
                    foreach (ReaderTypeItem item in TRCb.Items)
                    {
                        if (item.ID == selectedReader.TypeID)
                        {
                            TRCb.SelectedItem = item;
                            break;
                        }
                    }

                    // Выбираем специальность в комбобоксе
                    if (!string.IsNullOrEmpty(selectedReader.Speciality))
                    {
                        foreach (SpecialityItem item in SpCb.Items)
                        {
                            if (item.DisplaySp == selectedReader.Speciality)
                            {
                                SpCb.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        SpCb.SelectedIndex = -1;
                    }

                    // Обновляем видимость полей в зависимости от типа читателя
                    UpdateVisibilityByReaderType();

                    // Выбираем группу (если есть и если это студент)
                    if (selectedReader.GroupID.HasValue)
                    {
                        GrCb.SelectedValue = selectedReader.GroupID.Value;
                    }
                    else
                    {
                        GrCb.SelectedIndex = -1;
                    }

                    // Выбираем должность (если есть и если это сотрудник)
                    if (selectedReader.PostID.HasValue)
                    {
                        PostCb.SelectedValue = selectedReader.PostID.Value;
                    }
                    else
                    {
                        PostCb.SelectedIndex = -1;
                    }

                    // Переключаем видимость кнопок
                    AddReaderBtn.Visibility = Visibility.Collapsed;
                    UpdateReaderBtn.Visibility = Visibility.Visible;
                    DeleteReaderBtn.Visibility = Visibility.Visible;
                    PrintCardBtn.Visibility = Visibility.Visible;

                    // Показываем ID читателя (если есть лейбл для этого)
                    // IdLabel.Text = $"ID: {selectedReader.ReaderID}";

                    // Опционально: подсвечиваем выбранный элемент
                    ReadersListView.SelectedItem = selectedReader;

                    // Спрашиваем про печать
                    var result = MessageBox.Show(
                        $"Выбран читатель: {selectedReader.LastName} {selectedReader.FirstName}\n\nХотите распечатать читательский билет?",
                        "Печать билета",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        PrintCardBtn_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке данных читателя: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
   
}