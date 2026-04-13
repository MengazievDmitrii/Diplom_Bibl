using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Windows
{
    /// <summary>
    /// Логика взаимодействия для AutorisationWindow.xaml
    /// </summary>
    public partial class AutorisationWindow : Window
    {
        private readonly string _connectionString =
           "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        public AutorisationWindow()
        {
            InitializeComponent();
        }

        private void TogglePasswordVisibility_Checked(object sender, RoutedEventArgs e)
        {
            // Копируем пароль из PasswordBox в TextBox
            VisiblePasswordBox.Text = PasswordBox.Password; // Копируем текст
                                                            // Переключаем видимость
            PasswordBox.Visibility = Visibility.Collapsed; // Скрываем PasswordBox
            VisiblePasswordBox.Visibility = Visibility.Visible; // Показываем TextBox
                                                                // Меняем цвет иконки глаза (например, на синий)
            EyeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF"));
        }

        private void TogglePasswordVisibility_Unchecked(object sender, RoutedEventArgs e)
        {
            // Копируем пароль из TextBox обратно в PasswordBox
            PasswordBox.Password = VisiblePasswordBox.Text; // Копируем текст обратно
                                                            // Переключаем видимость
            VisiblePasswordBox.Visibility = Visibility.Collapsed; // Скрываем TextBox
            PasswordBox.Visibility = Visibility.Visible; // Показываем PasswordBox
                                                         // Меняем цвет иконки глаза обратно (например, на серый)
            EyeIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
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
        }

        private void UpdatePasswordPlaceholder()
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }


        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text?.Trim();
            string password = PasswordBox.Password?.Trim();

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                CustomMessageBoxHelper.Show("Введите логин и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            try
            {
                // === Шаг 1: Проверка логина и пароля ===
                int userId = -1;
                string fullName = "";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                SELECT ID_User, Surname, Name, Patronymic 
                FROM Users 
                WHERE Login = @Login AND PasswordHash = @Password", conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@Password", password);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userId = Convert.ToInt32(reader["ID_User"]);
                                fullName = $"{reader["Surname"]} {reader["Name"]}" +
                                          (reader["Patronymic"] != DBNull.Value ? $" {reader["Patronymic"]}" : "");
                            }
                        }
                    }
                }

                if (userId == -1)
                {
                    CustomMessageBoxHelper.Show("Неверный логин или пароль.", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // === Шаг 2: Загрузка ролей (в отдельном соединении!) ===
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

                // === Шаг 3: Открытие главного окна ===
                var mainWindow = new MainWindow(userId, fullName, roles);
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                CustomMessageBoxHelper.Show($"Ошибка при входе:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
