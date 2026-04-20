using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Windows
{
    public partial class AutorisationWindow : Window
    {
        private readonly string _connectionString =
           "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        public AutorisationWindow()
        {
            InitializeComponent();
            LoginPlaceholder.Visibility = string.IsNullOrEmpty(LoginBox.Text)
        ? Visibility.Visible
        : Visibility.Hidden;
            // Подписываемся на событие KeyDown для окна
            this.KeyDown += OnWindowKeyDown;

            // Подписываемся для полей ввода
       
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
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        // ==================== ОБРАБОТЧИКИ КЛАВИШИ ENTER ====================

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformLogin();
                e.Handled = true;
            }
        }

        private void OnLoginBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (PasswordBox.Visibility == Visibility.Visible)
                {
                    PasswordBox.Focus();
                }
                else
                {
                    VisiblePasswordBox.Focus();
                }
                e.Handled = true;
            }
        }

        private void OnPasswordBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformLogin();
                e.Handled = true;
            }
        }

        private void OnVisiblePasswordBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Password = VisiblePasswordBox.Text;
                PerformLogin();
                e.Handled = true;
            }
        }

        private void PerformLogin()
        {
            string login = LoginBox.Text?.Trim();
            string password = PasswordBox.Password?.Trim();

            if (string.IsNullOrEmpty(password) && VisiblePasswordBox.Visibility == Visibility.Visible)
            {
                password = VisiblePasswordBox.Text?.Trim();
                PasswordBox.Password = password;
            }

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                CustomMessageBoxHelper.Show("Введите логин и пароль.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                int userId = -1;
                string fullName = "";
                string storedHash = "";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT ID_User, Surname, Name, Patronymic, PasswordHash 
                        FROM Users 
                        WHERE Login = @Login", conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userId = Convert.ToInt32(reader["ID_User"]);
                                fullName = $"{reader["Surname"]} {reader["Name"]}" +
                                          (reader["Patronymic"] != DBNull.Value ? $" {reader["Patronymic"]}" : "");
                                storedHash = reader["PasswordHash"]?.ToString() ?? "";
                            }
                        }
                    }
                }

                if (userId == -1)
                {
                    CustomMessageBoxHelper.Show("Неверный логин или пароль.", "Ошибка входа",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    return;
                }

                bool isPasswordValid = VerifyPassword(password, storedHash);

                if (!isPasswordValid)
                {
                    CustomMessageBoxHelper.Show("Неверный логин или пароль.", "Ошибка входа",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    return;
                }

                var roles = new List<string>();
                using (var conn2 = new SqlConnection(_connectionString))
                {
                    conn2.Open();
                    using (var cmd2 = new SqlCommand(@"
                        SELECT ur.RoleCode
                        FROM UserRolesAssignments ura
                        JOIN UserRoles ur ON ura.ID_Role = ur.ID_Role
                        WHERE ura.ID_User = @UserId", conn2))
                    {
                        cmd2.Parameters.AddWithValue("@UserId", userId);
                        using (var reader2 = cmd2.ExecuteReader())
                        {
                            while (reader2.Read())
                            {
                                roles.Add(reader2["RoleCode"].ToString());
                            }
                        }
                    }
                }

                var mainWindow = new MainWindow(userId, fullName, roles);
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                CustomMessageBoxHelper.Show($"Ошибка при входе:\n{ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            PerformLogin();
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(enteredPassword) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHash);
            }
            catch
            {
                return enteredPassword == storedHash;
            }
        }
        private void LoginBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            LoginPlaceholder.Visibility = string.IsNullOrEmpty(LoginBox.Text)
                ? Visibility.Visible
                : Visibility.Hidden;
        }
    }
}