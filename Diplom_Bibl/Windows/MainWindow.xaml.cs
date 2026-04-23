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
        private string _currentUserFullName;
        private List<string> _userRoles;
        private NotificationService _notificationService;
        private SoundPlayer _notificationSound;

        public MainWindow(int userId, string fullName, List<string> roles)
        {
            InitializeComponent();
            _currentUserId = userId;
            _currentUserFullName = fullName;
            _userRoles = roles;

            // Загружаем звук уведомления
            try
            {
                _notificationSound = new SoundPlayer();
            }
            catch { }

            InitializeNotificationService();
        }

        private void InitializeNotificationService()
        {
            string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
            _notificationService = new NotificationService(connectionString, Dispatcher.CurrentDispatcher);

            // Подписываемся на события
            _notificationService.NewNotificationReceived += OnNewNotificationReceived;
            _notificationService.NotificationsUpdated += OnNotificationsUpdated;

            // Запускаем опрос уведомлений (каждые 5 секунд)
            _notificationService.StartPolling(5);
        }

        private void OnNewNotificationReceived(object sender, NotificationService.NotificationItem notification)
        {
            // Воспроизводим звук уведомления
            PlayNotificationSound();

            // Обновляем счётчик
            Dispatcher.Invoke(() => UpdateUnreadCount());

            // Выводим в консоль
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
                // Простой системный звук
                System.Media.SystemSounds.Beep.Play();
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
            // Открываем страницу управления заявками
            MainFrame.Navigate(new Pages.BookRequestsManagement(_currentUserId));
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                string tag = clickedButton.Tag as string;

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
                        MainFrame.Navigate(new Pages.RequestBook(_currentUserId, GetUserTypeId()));
                        break;
                    case "Управление заявками":
                        MainFrame.Navigate(new Pages.BookRequestsManagement(_currentUserId));
                        break;
                    case "Настройки":
                        MainFrame.Navigate(new Pages.Settings());
                        break;
                    default:
                        break;
                }
            }
        }

        private int GetUserTypeId()
        {
            // Определяем тип пользователя по ролям
            if (_userRoles.Contains("TEACHER") || _userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
                return 3; // Сотрудник
            return 1; // Студент
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _notificationService?.Dispose();

            AutorisationWindow autorisationWindow = new AutorisationWindow();
            autorisationWindow.Show();
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Pages.InformationServicePage());
        }

        protected override void OnClosed(EventArgs e)
        {
            _notificationService?.Dispose();
            base.OnClosed(e);
        }
    }
}