using Diplom_Bibl.Controls;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Windows
{
    public partial class AutorisationWindow : Window
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        public AutorisationWindow()
        {
            InitializeComponent();
            LoginPlaceholder.Visibility = string.IsNullOrEmpty(LoginBox.Text) ? Visibility.Visible : Visibility.Hidden;
            this.KeyDown += OnWindowKeyDown;
            LoginBox.KeyDown += OnLoginBoxKeyDown;
            PasswordBox.KeyDown += OnPasswordBoxKeyDown;
            VisiblePasswordBox.KeyDown += OnVisiblePasswordBoxKeyDown;
        }

        private void TogglePasswordVisibility_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePasswordBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            EyeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF"));
            VisiblePasswordBox.Focus();
            VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
        }

        private void TogglePasswordVisibility_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Password = VisiblePasswordBox.Text;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            EyeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
            PasswordBox.Focus();
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Hidden;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholder();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholder();
            if (VisiblePasswordBox.Visibility == Visibility.Visible)
            {
                VisiblePasswordBox.Text = PasswordBox.Password;
            }
        }

        private void UpdatePasswordPlaceholder()
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = PerformLoginAsync();
                e.Handled = true;
            }
        }

        private void OnLoginBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (PasswordBox.Visibility == Visibility.Visible)
                    PasswordBox.Focus();
                else
                    VisiblePasswordBox.Focus();
                e.Handled = true;
            }
        }

        private void OnPasswordBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = PerformLoginAsync();
                e.Handled = true;
            }
        }

        private void OnVisiblePasswordBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Password = VisiblePasswordBox.Text;
                _ = PerformLoginAsync();
                e.Handled = true;
            }
        }

        private async Task PerformLoginAsync()
        {
            string login = LoginBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                CustomMessageBox.Show("Введите логин и пароль!", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                SELECT 
                    u.ID_User,
                    u.Surname,
                    u.Name,
                    u.Patronymic,
                    u.Login,
                    u.PasswordHash,
                    u.ReaderID,
                    r.CardNumber,
                    r.LastName,
                    r.FirstName,
                    r.MiddleName,
                    r.TypeID,
                    r.Post,
                    p.Post_Name,
                    rt.TypeName,
                    r.GroupID,
                    g.GroupCode
                FROM Users u
                LEFT JOIN ReaderForm r ON u.ReaderID = r.ReaderID
                LEFT JOIN ReaderType rt ON r.TypeID = rt.TypeID
                LEFT JOIN Post p ON r.Post = p.ID_Post
                LEFT JOIN Groups g ON r.GroupID = g.GroupID
                WHERE u.Login = @Login OR r.CardNumber = @CardNumber";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        // Добавляем оба параметра
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@CardNumber", login);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string storedHash = reader["PasswordHash"].ToString();

                                if (BCrypt.Net.BCrypt.Verify(password, storedHash))
                                {
                                    int userId = Convert.ToInt32(reader["ID_User"]);
                                    int readerId = reader["ReaderID"] != DBNull.Value ? Convert.ToInt32(reader["ReaderID"]) : 0;
                                    int userTypeId = reader["TypeID"] != DBNull.Value ? Convert.ToInt32(reader["TypeID"]) : 1;

                                    // Формируем ФИО (если нет данных из ReaderForm, используем данные из Users)
                                    string lastName = reader["LastName"]?.ToString() ?? reader["Surname"]?.ToString() ?? "";
                                    string firstName = reader["FirstName"]?.ToString() ?? reader["Name"]?.ToString() ?? "";
                                    string middleName = reader["MiddleName"]?.ToString() ?? reader["Patronymic"]?.ToString() ?? "";
                                    string fullName = $"{lastName} {firstName} {middleName}".Trim();

                                    // Если ФИО пустое, используем логин
                                    if (string.IsNullOrWhiteSpace(fullName))
                                    {
                                        fullName = reader["Login"]?.ToString() ?? "Пользователь";
                                    }

                                    reader.Close();

                                    var roles = await GetUserRoles(login);

                                    var mainWindow = new MainWindow(userId, readerId, fullName, roles, userTypeId);
                                    mainWindow.Show();
                                    this.Close();
                                }
                                else
                                {
                                    CustomMessageBox.Show("Неверный пароль!", "Ошибка",
                                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                CustomMessageBox.Show("Пользователь не найден!\n\nПроверьте логин или номер читательского билета.", "Ошибка",
                                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при входе: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }
        //  метод получения ролей
        private async Task<List<string>> GetUserRoles(string login)
        {
            var roles = new List<string>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string getUserIdQuery = @"
                SELECT u.ID_User 
                FROM Users u
                LEFT JOIN ReaderForm r ON u.ReaderID = r.ReaderID
                WHERE u.Login = @Login OR r.CardNumber = @CardNumber";

                    int userId = 0;

                    using (var cmd = new SqlCommand(getUserIdQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@CardNumber", login);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null)
                            userId = Convert.ToInt32(result);
                    }

                    if (userId > 0)
                    {
                        string query = @"
                    SELECT r.RoleCode 
                    FROM UserRolesAssignments ura
                    INNER JOIN UserRoles r ON ura.ID_Role = r.ID_Role
                    WHERE ura.ID_User = @UserId";

                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    roles.Add(reader["RoleCode"].ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки ролей: {ex.Message}");
            }

            if (roles.Count == 0)
            {
                roles.Add("READER");
            }

            return roles;
        }
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLoginAsync();
        }

        private void LoginBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            LoginPlaceholder.Visibility = string.IsNullOrEmpty(LoginBox.Text) ? Visibility.Visible : Visibility.Hidden;
        }
    }
}