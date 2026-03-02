using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddUser.xaml
    /// </summary>
    public partial class AddUser : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private int? _editingUserId = null;
        private ObservableCollection<UserListItem> _users = new ObservableCollection<UserListItem>();

        // Новый конструктор с параметром
        public AddUser(int userId)
        {
            InitializeComponent();
            LoadRoles();
            LoadUsers();        // ← сначала загрузить список
            LoadUser(userId);   // ← потом данные пользователя
        }

        // Старый конструктор (для создания нового)
        public AddUser()
        {
            InitializeComponent();
            LoadRoles();
            LoadUsers(); 
        }

        // Модель роли с флагом выбора
        private class RoleItem : INotifyPropertyChanged
        {
            public int ID_Role { get; set; }
            public string RoleName { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public class UserListItem
        {
            public int ID_User { get; set; }
            public string Surname { get; set; }
            public string Name { get; set; }
            public string Patronymic { get; set; }
            public string Login { get; set; }
            public string RoleNames { get; set; } // напр. "Администратор, Библиотекарь"
        }



        private void LoadRoles()
        {
            const string sql = "SELECT ID_Role, RoleName FROM UserRoles ORDER BY RoleName";
            var roles = new ObservableCollection<RoleItem>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        roles.Add(new RoleItem
                        {
                            ID_Role = Convert.ToInt32(reader["ID_Role"]),
                            RoleName = reader["RoleName"].ToString(),
                            IsSelected = false
                        });
                    }
                }
            }

            LbRoles.ItemsSource = roles;
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput(out string login, out string password)) return;

            try
            {
                int userId = InsertUser(login, password);
                if (userId > 0)
                {
                    AssignRolesToUser(userId);
                    MessageBox.Show("Пользователь успешно создан.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    RefreshUserList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании пользователя:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_editingUserId == null) return;

            if (!ValidateInput(out string login, out string password)) return;

            try
            {
                UpdateUser(_editingUserId.Value, login, password);
                // Удаляем старые роли
                DeleteUserRoles(_editingUserId.Value);
                // Назначаем новые
                AssignRolesToUser(_editingUserId.Value);

                MessageBox.Show("Данные пользователя обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                RefreshUserList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_editingUserId == null) return;

            var result = MessageBox.Show("Удалить пользователя?\nЭто действие нельзя отменить.", "Подтверждение",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                DeleteUser(_editingUserId.Value);
                MessageBox.Show("Пользователь удалён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                RefreshUserList(); 
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show("Невозможно удалить: на пользователя ссылаются другие записи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

        private bool ValidateInput(out string login, out string password)
        {
            login = TbLogin.Text?.Trim();
            password = PbPassword.Password?.Trim();

            if (string.IsNullOrWhiteSpace(TbSurname.Text) ||
                string.IsNullOrWhiteSpace(TbName.Text) ||
                string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Заполните все обязательные поля (*).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка: хотя бы одна роль выбрана
            var roles = LbRoles.ItemsSource as ObservableCollection<RoleItem>;
            if (roles == null || !roles.Any(r => r.IsSelected))
            {
                MessageBox.Show("Выберите хотя бы одну роль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private int InsertUser(string login, string password)
        {
            const string sql = @"
                INSERT INTO Users (Surname, Name, Patronymic, Login, PasswordHash)
                VALUES (@Surname, @Name, @Patronymic, @Login, @Password);
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Surname", TbSurname.Text.Trim());
                    cmd.Parameters.AddWithValue("@Name", TbName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Patronymic", TbPatronymic.Text.Trim() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@Password", password);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private void UpdateUser(int userId, string login, string password)
        {
            const string sql = @"
                UPDATE Users SET
                    Surname = @Surname,
                    Name = @Name,
                    Patronymic = @Patronymic,
                    Login = @Login,
                    PasswordHash = @Password
                WHERE ID_User = @Id";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.Parameters.AddWithValue("@Surname", TbSurname.Text.Trim());
                    cmd.Parameters.AddWithValue("@Name", TbName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Patronymic", TbPatronymic.Text.Trim() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@Password", password);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteUser(int userId)
        {
            const string sql = "DELETE FROM Users WHERE ID_User = @Id";
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteUserRoles(int userId)
        {
            const string sql = "DELETE FROM UserRolesAssignments WHERE ID_User = @Id";
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void AssignRolesToUser(int userId)
        {
            var roles = LbRoles.ItemsSource as ObservableCollection<RoleItem>;
            if (roles == null) return;

            foreach (var role in roles.Where(r => r.IsSelected))
            {
                const string sql = @"
                    INSERT INTO UserRolesAssignments (ID_User, ID_Role)
                    VALUES (@UserId, @RoleId)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@RoleId", role.ID_Role);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void ClearForm()
        {
            TbSurname.Clear();
            TbName.Clear();
            TbPatronymic.Clear();
            TbLogin.Clear();
            PbPassword.Clear();

            var roles = LbRoles.ItemsSource as ObservableCollection<RoleItem>;
            if (roles != null)
            {
                foreach (var r in roles) r.IsSelected = false;
            }

            _editingUserId = null;
            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
        private void LoadUser(int userId)
        {
            const string sql = @"
        SELECT Surname, Name, Patronymic, Login, PasswordHash
        FROM Users
        WHERE ID_User = @Id";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            TbSurname.Text = reader["Surname"].ToString();
                            TbName.Text = reader["Name"].ToString();
                            TbPatronymic.Text = reader["Patronymic"]?.ToString() ?? "";
                            TbLogin.Text = reader["Login"].ToString();
                            PbPassword.Password = reader["PasswordHash"].ToString();

                            // Загружаем роли пользователя
                            LoadUserRoles(userId);
                          
                        }
                        else
                        {
                            MessageBox.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            NavigationService?.GoBack();
                            return;
                        }
                    }
                }
            }

            // Переключаем интерфейс в режим редактирования
            BtnSaveNew.Visibility = Visibility.Collapsed;
            BtnSaveChanges.Visibility = Visibility.Visible;
            BtnDelete.Visibility = Visibility.Visible;
        }
        private void LoadUserRoles(int userId)
        {
            const string sql = @"
        SELECT ur.ID_Role
        FROM UserRolesAssignments ura
        JOIN UserRoles ur ON ura.ID_Role = ur.ID_Role
        WHERE ura.ID_User = @UserId";

            var assignedRoleIds = new HashSet<int>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assignedRoleIds.Add(Convert.ToInt32(reader["ID_Role"]));
                        }
                    }
                }
            }

            // Устанавливаем IsSelected = true для назначенных ролей
            var roles = LbRoles.ItemsSource as ObservableCollection<RoleItem>;
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    role.IsSelected = assignedRoleIds.Contains(role.ID_Role);
                }
            }
        }
        private void LvUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvUsers.SelectedItem is UserListItem user)
            {
                _editingUserId = user.ID_User;
                LoadUser(user.ID_User);
                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
            }
        }
        private void LoadUsers()
        {
            const string sql = @"
        SELECT u.ID_User, u.Surname, u.Name, u.Patronymic, u.Login,
               STRING_AGG(r.RoleName, ', ') AS RoleNames
        FROM Users u
        LEFT JOIN UserRolesAssignments ura ON u.ID_User = ura.ID_User
        LEFT JOIN UserRoles r ON ura.ID_Role = r.ID_Role
        GROUP BY u.ID_User, u.Surname, u.Name, u.Patronymic, u.Login
        ORDER BY u.Surname, u.Name";

            var users = new ObservableCollection<UserListItem>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new UserListItem
                        {
                            ID_User = Convert.ToInt32(reader["ID_User"]),
                            Surname = reader["Surname"].ToString(),
                            Name = reader["Name"].ToString(),
                            Patronymic = reader["Patronymic"]?.ToString() ?? "",
                            Login = reader["Login"].ToString(),
                            RoleNames = reader["RoleNames"]?.ToString() ?? ""
                        });
                    }
                }
            }

            _users = users; // ← сохраняем
            LvUsers.ItemsSource = _users;
        }
        private void RefreshUserList()
        {
            LoadUsers(); // Перезагружает _users и обновляет LvUsers.ItemsSource
        }

    }

}
