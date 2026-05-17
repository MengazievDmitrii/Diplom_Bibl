
using Dadata.Model;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Pages;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Diplom_Bibl.Windows
{
    public partial class MainWindow : Window
    {
        private int _currentUserId;
        private int _currentReaderId;
        private int _currentUserTypeId;
        private string _currentUserFullName;
        private List<string> _userRoles;
        private NotificationService _notificationService;
        private SoundPlayer _notificationSound;
        private int? _currentSessionId; // Храним ID активной сессии ПК

        public MainWindow(int userId, int readerId, string fullName, List<string> roles, int userTypeId)
        {
            InitializeComponent();
            _currentUserId = userId;
            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;
            _currentUserFullName = fullName;
            _userRoles = roles;
            _currentSessionId = null;

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

        private async Task ShowComputerSessionIfNeeded()
        {
            // Показываем окно регистрации ТОЛЬКО для читателей (не библиотекарей и не администраторов)
            bool isLibrarianOrAdmin = _userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN");

            if (isLibrarianOrAdmin)
            {
                System.Diagnostics.Debug.WriteLine("Библиотекарь/администратор - окно регистрации ПК не показывается");
                return;
            }

            try
            {
                var sessionService = new ComputerSessionService(
                    "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;");

                // Проверяем активную сессию
                var activeSessions = await sessionService.GetActiveSessionsAsync();
                var existingSession = activeSessions.FirstOrDefault(s => s.UserID == _currentUserId);

                if (existingSession != null)
                {
                    // Если есть активная сессия, сохраняем её ID
                    _currentSessionId = existingSession.ID_Session;
                    System.Diagnostics.Debug.WriteLine($"Найдена активная сессия ID={_currentSessionId}");
                    return;
                }

                // Определяем группу для студентов
                string groupCode = "";
                if (_currentUserTypeId == 1) // Студент
                {
                    using (var conn = new SqlConnection("Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(
                            "SELECT ISNULL(g.GroupCode, '') FROM ReaderForm r LEFT JOIN Groups g ON r.GroupID = g.GroupID WHERE r.ReaderID = @ReaderId",
                            conn))
                        {
                            cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId);
                            var result = await cmd.ExecuteScalarAsync();
                            groupCode = result?.ToString() ?? "";
                        }
                    }
                }

                var computerWindow = new ComputerSessionWindow(_currentUserId, _currentReaderId,
                    _currentUserFullName, groupCode);
                computerWindow.Owner = this;
                computerWindow.ShowDialog();

                // После закрытия окна регистрации получаем ID сессии (если нужно, через статическое поле или событие)
                // В текущей реализации сессия создается внутри окна, но ID не передается обратно
                // Добавим получение активной сессии после закрытия окна
                var updatedSessions = await sessionService.GetActiveSessionsAsync();
                var newSession = updatedSessions.FirstOrDefault(s => s.UserID == _currentUserId);
                if (newSession != null)
                {
                    _currentSessionId = newSession.ID_Session;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при показе окна ПК: {ex.Message}");
            }
        }

        // Метод для завершения активной сессии ПК
        private async Task EndComputerSessionAsync()
        {
            if (_currentSessionId.HasValue)
            {
                try
                {
                    var sessionService = new ComputerSessionService(
                        "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;");
                    await sessionService.EndSessionAsync(_currentSessionId.Value);
                    System.Diagnostics.Debug.WriteLine($"Сессия ПК {_currentSessionId} завершена");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка завершения сессии ПК: {ex.Message}");
                }
            }
        }

        private string GetRoleDisplayName()
        {
            if (_userRoles.Contains("ADMIN"))
                return "Администратор";

            if (_userRoles.Contains("LIBRARIAN"))
                return "Библиотекарь";

            if (_currentUserTypeId == 3)
                return "Преподаватель";

            if (_userRoles.Contains("READER"))
                return "Читатель (Студент)";

            return "Пользователь";
        }

        private void SetupMenuVisibility()
        {
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
                    case "Управление ПК":
                        MainFrame.Navigate(new Pages.ComputerSessionsManagement());
                        break;
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
            {
                MainFrame.Navigate(new Pages.InformationServicePage());
            }
            else
            {
                MainFrame.Navigate(new Pages.RequestBook(_currentReaderId, _currentUserTypeId));
            }

            await ShowComputerSessionIfNeeded();
        }

        private bool _isLoggingOut = false;
        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            // Сначала показываем сообщение с подтверждением
            var result = CustomMessageBox.Show("Вы точно хотите выйти?",
                "Подтверждение выхода",
                CustomMessageBoxButton.YesNo,
                CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                // Только после подтверждения завершаем сессию ПК
                await EndComputerSessionAsync();

                // Завершаем сервис уведомлений
                _notificationService?.Dispose();

                // Открываем окно авторизации
                AutorisationWindow autorisationWindow = new AutorisationWindow();
                autorisationWindow.Show();

                // Закрываем текущее окно
                this.Close();
            }
            else
            {
                // Если пользователь отменил, сбрасываем флаг
                _isLoggingOut = false;
            }
        }
        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Если выход инициирован через кнопку, показываем другое сообщение
            if (!_isLoggingOut)
            {
                // Показываем сообщение только при закрытии через крестик
                var result = CustomMessageBox.Show("Вы точно хотите выйти и закрыть приложение?",
                    "Подтверждение выхода",
                    CustomMessageBoxButton.YesNo,
                    CustomMessageBoxImage.Question);

                if (result != CustomMessageBoxResult.Yes)
                {
                    e.Cancel = true; // Отменяем закрытие
                    return;
                }
            }

            // Завершаем сессию ПК
            await EndComputerSessionAsync();

            // Завершаем сервис уведомлений
            _notificationService?.Dispose();

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}