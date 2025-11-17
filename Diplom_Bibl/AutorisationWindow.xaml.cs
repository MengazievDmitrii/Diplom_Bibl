using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Diplom_Bibl
{
    /// <summary>
    /// Логика взаимодействия для AutorisationWindow.xaml
    /// </summary>
    public partial class AutorisationWindow : Window
    {
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // --- Примерная логика проверки ---
            // string enteredLogin = LoginBox.Text; // Получаем введённый логин
            // string enteredPassword = PasswordBox.Password; // Получаем введённый пароль

            // Тут должна быть ваша логика проверки логина и пароля
            // Например, вызов метода аутентификации, обращение к базе данных и т.д.
            // bool isAuthenticated = AuthenticateUser(enteredLogin, enteredPassword);

            // Пока что, просто предположим, что аутентификация прошла успешно.
            // Замените этот блок на реальную проверку.
            bool isAuthenticated = true; // Пример: всегда успешная аутентификация

            if (isAuthenticated)
            {
                // 1. Создаём новое окно MainWindow
                MainWindow mainWindow = new MainWindow();

                // 2. Показываем новое окно
                mainWindow.Show();

                // 3. Закрываем текущее окно авторизации
                this.Close(); // 'this' ссылается на текущее окно (LoginWindow)
            }
            else
            {
                // Обработка неудачной аутентификации
                // Например, показать сообщение пользователю
                MessageBox.Show("Неверный логин или пароль.", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
