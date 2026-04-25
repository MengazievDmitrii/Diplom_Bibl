using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Diplom_Bibl.Windows
{
    public partial class MainWindow : Window
    {
        private int _currentUserId;
        private int _currentReaderId;
        private int _currentUserTypeId;  // Добавлено поле для TypeID
        private string _currentUserFullName;
        private List<string> _userRoles;
        private NotificationService _notificationService;
        private SoundPlayer _notificationSound;

        // Изменяем конструктор - добавляем userTypeId
        public MainWindow(int userId, int readerId, string fullName, List<string> roles, int userTypeId)
        {
            InitializeComponent();
            _currentUserId = userId;
            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;
            _currentUserFullName = fullName;
            _userRoles = roles;

            // Устанавливаем информацию о пользователе
            TbUserName.Text = fullName;
            TbUserRole.Text = GetRoleDisplayName();

            try
            {
                _notificationSound = new SoundPlayer();
            }
            catch { }

            InitializeNotificationService();
            SetupMenuVisibility();
        }

        private string GetRoleDisplayName()
        {
            // Приоритет 1: Администратор
            if (_userRoles.Contains("ADMIN"))
                return "Администратор";

            // Приоритет 2: Библиотекарь
            if (_userRoles.Contains("LIBRARIAN"))
                return "Библиотекарь";

            // Приоритет 3: Преподаватель (TypeID = 3 и нет роли LIBRARIAN)
            if (_currentUserTypeId == 3)
                return "Преподаватель";

            // Приоритет 4: Обычный читатель
            if (_userRoles.Contains("READER"))
                return "Читатель (Студент)";

            return "Пользователь";
        }
        private void SetupMenuVisibility()
        {
            // Скрываем все меню по умолчанию
            MenuAdmin.Visibility = Visibility.Collapsed;
            MenuLibrarian.Visibility = Visibility.Collapsed;
            MenuReader.Visibility = Visibility.Collapsed;

            if (_userRoles.Contains("ADMIN"))
            {
                MenuAdmin.Visibility = Visibility.Visible;
            }
            else if (_userRoles.Contains("LIBRARIAN"))
            {
                MenuLibrarian.Visibility = Visibility.Visible;
            }
            else
            {
                MenuReader.Visibility = Visibility.Visible;
            }
        }

        private void InitializeNotificationService()
        {
            string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
            _notificationService = new NotificationService(connectionString, Dispatcher.CurrentDispatcher);

            _notificationService.NewNotificationReceived += OnNewNotificationReceived;
            _notificationService.NotificationsUpdated += OnNotificationsUpdated;

            _notificationService.StartPolling(5);
        }

        private void OnNewNotificationReceived(object sender, NotificationService.NotificationItem notification)
        {
            PlayNotificationSound();
            Dispatcher.Invoke(() => UpdateUnreadCount());
            System.Diagnostics.Debug.WriteLine($"🔔 НОВОЕ УВЕДОМЛЕНИЕ: {notification.Message}");
        }

        private void OnNotificationsUpdated(object sender, System.Collections.ObjectModel.ObservableCollection<NotificationService.NotificationItem> notifications)
        {
            Dispatcher.Invoke(() => UpdateUnreadCount());
        }

        private void PlayNotificationSound()
        {
            try
            {
                SystemSounds.Beep.Play();
            }
            catch { }
        }

        private void UpdateUnreadCount()
        {
            int count = _notificationService?.GetUnreadCount() ?? 0;
            TbUnreadCount.Text = count.ToString();
            TbUnreadCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotificationPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainFrame.Navigate(new Pages.BookRequestsManagement(_currentUserId));
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is string tag)
            {
                switch (tag)
                {
                    case "Информационное обслуживание":
                        MainFrame.Navigate(new Pages.InformationServicePage());
                        break;
                    case "Читатели":
                        MainFrame.Navigate(new Pages.Readers());
                        break;
                    case "Библиотечный фонд":
                        MainFrame.Navigate(new Pages.LibraryFundPage());
                        break;
                    case "Отчёты":
                        MainFrame.Navigate(new Pages.ReportsPage());
                        break;
                    case "Подать заявку":
                        // Используем _currentUserTypeId (3 для преподавателя)
                        MainFrame.Navigate(new Pages.RequestBook(_currentReaderId, _currentUserTypeId));
                        break;
                    case "Управление заявками":
                        MainFrame.Navigate(new Pages.BookRequestsManagement(_currentUserId));
                        break;
                    case "Настройки":
                        MainFrame.Navigate(new Pages.Settings());
                        break;
                    case "Мои заявки":
                        MainFrame.Navigate(new Pages.MyRequestsPage(_currentReaderId));
                        break;
                }
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Определяем, какую страницу открывать по умолчанию
            if (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
            {
                // Для библиотекаря и администратора - информационное обслуживание
                MainFrame.Navigate(new Pages.InformationServicePage());
            }
            else
            {
                // Для читателя (студент или преподаватель) - подача заявки
                MainFrame.Navigate(new Pages.RequestBook(_currentReaderId, _currentUserTypeId));
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _notificationService?.Dispose();

            AutorisationWindow autorisationWindow = new AutorisationWindow();
            autorisationWindow.Show();
            this.Close();
        }

        
        protected override void OnClosed(EventArgs e)
        {
            _notificationService?.Dispose();
            base.OnClosed(e);
        }
    }
}