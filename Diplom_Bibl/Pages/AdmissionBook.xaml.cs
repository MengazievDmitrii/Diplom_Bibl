using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    public partial class AdmissionBook : Page
    {
        private readonly string _connectionString =
            "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        private int? _currentReceiptId = null;
        private ObservableCollection<dynamic> _allReceipts;

        // Модель для ComboBox
        private class UserItem
        {
            public int ID_User { get; set; }
            public string FullName { get; set; } // Surname + Name + Patronymic
        }

        public AdmissionBook()
        {
            InitializeComponent();
            LoadResponsibleUsers(); // ← загружаем список пользователей
            LoadReceipts();
            PrepareNewReceipt();
        }
       
        // ГЕНЕРАЦИЯ НОМЕРА НАКЛАДНОЙ: ПН-0001, ПН-0002, ...
              private string GenerateReceiptNumber(int nextId)
        {
            return $"ПН-{nextId:D4}"; // D4 = 0001, 0002, ...
        }

      // ЗАГРУЗКА СПИСКА ПОЛЬЗОВАТЕЛЕЙ ДЛЯ ComboBox
       private void LoadResponsibleUsers()
        {
            const string sql = @"
                SELECT ID_User,
                       Surname + ' ' + Name + ISNULL(' ' + Patronymic, '') AS FullName
                FROM Users
                ORDER BY Surname, Name";

            var users = new ObservableCollection<UserItem>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new UserItem
                        {
                            ID_User = Convert.ToInt32(reader["ID_User"]),
                            FullName = reader["FullName"].ToString()
                        });
                    }
                }
            }

            CbResponsible.ItemsSource = users;
        }

       // ПОДГОТОВКА ФОРМЫ ДЛЯ НОВОЙ ЗАПИСИ
        private void PrepareNewReceipt()
        {
            // Получаем следующий ID (MAX(IDReceipt) + 1)
            int nextId = 1;
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT ISNULL(MAX(IDReceipt), 0) + 1 FROM ReceiptBooks", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int maxId))
                            nextId = maxId;
                    }
                }
            }
            catch { /* игнорируем ошибку — будет 1 */ }

            TbReceiptNumber.Text = GenerateReceiptNumber(nextId);
            TbReceiptDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            // Устанавливаем первого пользователя (если есть)
            if (CbResponsible.Items.Count > 0)
                CbResponsible.SelectedIndex = 0;

            TbComment.Clear();

            _currentReceiptId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }
               
        // ЗАГРУЗКА СПИСКА АКТОВ
        private void LoadReceipts()
        {
            try
            {
                const string query = @"
                    SELECT r.IDReceipt, r.ReceiptNumber, r.ReceiptDate, 
                           u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS ResponsiblePerson,
                           r.Comment
                    FROM ReceiptBooks r
                    LEFT JOIN Users u ON r.ID_ResponsibleUser = u.ID_User
                    ORDER BY r.ReceiptDate DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var list = new ObservableCollection<dynamic>();
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                IDReceipt = reader["IDReceipt"],
                                ReceiptNumber = reader["ReceiptNumber"].ToString(),
                                ReceiptDate = Convert.ToDateTime(reader["ReceiptDate"]),
                                ResponsiblePerson = reader["ResponsiblePerson"].ToString(),
                                Comment = reader["Comment"]?.ToString() ?? ""
                            });
                        }
                        _allReceipts = list;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        private void ApplyFilter(string term)
        {
            if (_allReceipts == null)
            {
                LvReceipts.ItemsSource = null;
                return;
            }

            var filtered = _allReceipts.Where(r =>
            {
                string resp = r.ResponsiblePerson?.ToString() ?? "";
                string comm = r.Comment?.ToString() ?? "";
                return string.IsNullOrEmpty(term) ||
                       resp.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       comm.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();

            LvReceipts.ItemsSource = new ObservableCollection<dynamic>(filtered);
        }
   // СОХРАНЕНИЕ НОВОЙ НАКЛАДНОЙ
        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            // Проверка выбора ответственного лица
            if (CbResponsible.SelectedValue == null)
            {
                 CustomMessageBox.Show("Выберите ответственное лицо.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            int responsibleId = (int)CbResponsible.SelectedValue;
            string comment = TbComment.Text?.Trim();

            string number = TbReceiptNumber.Text;
            DateTime date = DateTime.Now;

            const string insertQuery = @"
                INSERT INTO ReceiptBooks (ReceiptNumber, ReceiptDate, ID_ResponsibleUser, Comment)
                VALUES (@Number, @Date, @ResponsibleID, @Comment);";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Number", number);
                        cmd.Parameters.AddWithValue("@Date", date);
                        cmd.Parameters.AddWithValue("@ResponsibleID", responsibleId);
                        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);

                        cmd.ExecuteNonQuery();
                         CustomMessageBox.Show("Накладная успешно создана.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        LoadReceipts();
                        PrepareNewReceipt();
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
               
        // РЕДАКТИРОВАНИЕ
        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReceiptId.HasValue)
            {
                 CustomMessageBox.Show("Нет выбранной записи.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (CbResponsible.SelectedValue == null)
            {
                 CustomMessageBox.Show("Выберите ответственное лицо.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            int responsibleId = (int)CbResponsible.SelectedValue;
            string comment = TbComment.Text?.Trim();

            const string updateQuery = @"
                UPDATE ReceiptBooks 
                SET ID_ResponsibleUser = @ResponsibleID, Comment = @Comment
                WHERE IDReceipt = @Id;";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ResponsibleID", responsibleId);
                        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", _currentReceiptId.Value);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                             CustomMessageBox.Show("Изменения сохранены.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            LoadReceipts();
                            PrepareNewReceipt();
                        }
                        else
                        {
                             CustomMessageBox.Show("Запись не найдена.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
               
        // УДАЛЕНИЕ
             private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentReceiptId.HasValue) return;

            var result =  CustomMessageBox.Show("Удалить накладную?", "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);
            if (result != CustomMessageBoxResult.Yes) return;

            const string deleteQuery = "DELETE FROM ReceiptBooks WHERE IDReceipt = @Id;";
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentReceiptId.Value);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                             CustomMessageBox.Show("Накладная удалена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            LoadReceipts();
                            PrepareNewReceipt();
                        }
                        else
                        {
                             CustomMessageBox.Show("Запись уже удалена.", "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                 CustomMessageBox.Show("Невозможно удалить: на эту накладную ссылаются книги.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

       // ДВОЙНОЙ КЛИК ПО СПИСКУ
        private void LvReceipts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            dynamic item = LvReceipts.SelectedItem;
            if (item == null) return;

            _currentReceiptId = item.IDReceipt;
            TbReceiptNumber.Text = item.ReceiptNumber;
            TbReceiptDate.Text = ((DateTime)item.ReceiptDate).ToString("dd.MM.yyyy HH:mm");

            // Установка выбранного пользователя по ID
            CbResponsible.SelectedValue = item.ResponsiblePerson == "" ? null :
                GetUserIdByName(item.ResponsiblePerson); // см. ниже

            TbComment.Text = item.Comment;

            BtnSaveNew.Visibility = Visibility.Collapsed;
            BtnSaveChanges.Visibility = Visibility.Visible;
            BtnDelete.Visibility = Visibility.Visible;
        }

        // Вспомогательный метод: получить ID пользователя по ФИО (для редактирования)
        private int? GetUserIdByName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            const string sql = @"
                SELECT TOP 1 ID_User 
                FROM Users 
                WHERE Surname + ' ' + Name + ISNULL(' ' + Patronymic, '') = @FullName";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FullName", fullName);
                    var result = cmd.ExecuteScalar();
                    return result == null ? null : (int?)Convert.ToInt32(result);
                }
            }
        }
        // ПОИСК
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchBox.Text.Trim());
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddUser addUser = new Pages.AddUser();
            this.NavigationService?.Navigate(addUser);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadResponsibleUsers();
        }
    }
}