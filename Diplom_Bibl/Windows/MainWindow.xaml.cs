// Файл: MainWindow.xaml.cs

using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Pages;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Diplom_Bibl.Windows
{
    /// <summary>
    /// Главное окно приложения - содержит боковое меню навигации и область отображения страниц
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Поля класса

        private bool _isMenuExpanded = true;
        private const double ExpandedWidth = 260;
        private const double CollapsedWidth = 60;

        private int _currentUserId;
        private int _currentReaderId;
        private int _currentUserTypeId;
        private string _currentUserFullName;
        private List<string> _userRoles;

        private NotificationService _notificationService;
        private int? _currentSessionId;
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private bool _isLoggingOut = false;
        private System.Threading.Timer _returnDateTimer;
        private bool _isInitialized = false;
        private System.Threading.Timer _expiredRequestsTimer;

        // ДОБАВИТЬ ЭТИ ПОЛЯ:
        private static int _activeNotificationWindows = 0;
        private static readonly object _notificationLock = new object();
        private static DateTime _lastNotificationTime = DateTime.MinValue;
        private static readonly TimeSpan _minNotificationInterval = TimeSpan.FromSeconds(1.5);

   

        #endregion

        #region Конструктор и инициализация

        public MainWindow(int userId, int readerId, string fullName, List<string> roles, int userTypeId)
        {
            InitializeComponent();

            _currentUserId = userId;
            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;
            _currentUserFullName = fullName;
            _userRoles = roles ?? new List<string>();

            TbUserName.Text = fullName ?? "Пользователь";
            TbUserRole.Text = GetRoleDisplayName();
            SetupMenuVisibility();

            this.Loaded += Window_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
            MainFrame.Navigating += MainFrame_Navigating;
            MainFrame.LoadCompleted += MainFrame_LoadCompleted;
            MainFrame.NavigationFailed += MainFrame_NavigationFailed;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= Window_Loaded;
            await OnWindowLoadedAsync();
        }

        // Вызови этот метод в OnWindowLoadedAsync после инициализации:
        private async Task OnWindowLoadedAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                await Dispatcher.InvokeAsync(() => LoadInitialPage());

                _ = Task.Run(() => InitializeNotificationServiceSync());
                _ = Task.Run(() => CheckAndHandleComputerSession());
                _ = Task.Run(() => StartReturnDateMonitoringBackground());
                _ = Task.Run(() => StartExpiredRequestsMonitoring());
                _ = Task.Run(async () => await ForceShowAllNotifications());
                await Task.Delay(2000);
                if (_notificationService != null)
                {
                    var count = _notificationService.GetUnreadCount();
                    UpdateUnreadCount(count);
                    System.Diagnostics.Debug.WriteLine($"🔔 Первоначальный счетчик уведомлений: {count}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки: {ex.Message}");
            }
        }
            
       
        /// <summary>
        /// Загрузка начальной страницы в зависимости от роли пользователя
        /// </summary>
        private void LoadInitialPage()
        {
            if (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
            {
                MainFrame.Navigate(new InformationServicePage());
            }
            else
            {
                MainFrame.Navigate(new RequestBook(_currentReaderId, _currentUserTypeId));
            }
        }

        /// <summary>
        /// Инициализация сервиса уведомлений в фоновом потоке
        /// </summary>
        private void InitializeNotificationServiceSync()
        {
            try
            {
                int notificationUserId = (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
                    ? _currentUserId
                    : _currentReaderId;

                System.Diagnostics.Debug.WriteLine($"🔧 Инициализация сервиса уведомлений для UserId: {notificationUserId}, Roles: {string.Join(", ", _userRoles)}");

                var notificationService = new NotificationService(
                    _connectionString,
                    Dispatcher.CurrentDispatcher,
                    notificationUserId,
                    _currentUserTypeId,
                    _userRoles
                );

                notificationService.NewNotificationReceived += OnNewNotificationReceived;
                notificationService.UnreadCountChanged += OnUnreadCountChanged;
                notificationService.NotificationsUpdated += OnNotificationsUpdated;

                int intervalSeconds = (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN")) ? 3 : 10;
                notificationService.StartPolling(intervalSeconds);

                System.Diagnostics.Debug.WriteLine($"✅ Сервис уведомлений инициализирован, интервал: {intervalSeconds} сек");

                Dispatcher.Invoke(() => _notificationService = notificationService);

                Task.Delay(1000).ContinueWith(_ =>
                {
                    _ = Task.Run(() => notificationService.CheckNewNotifications());
                    System.Diagnostics.Debug.WriteLine("🔍 Принудительная проверка уведомлений после инициализации");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка инициализации уведомлений: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Обработчик обновления коллекции уведомлений
        /// </summary>
        private void OnNotificationsUpdated(object sender, ObservableCollection<NotificationService.NotificationItem> notifications)
        {
            System.Diagnostics.Debug.WriteLine($"📢 ОБНОВЛЕНИЕ УВЕДОМЛЕНИЙ: получено {notifications.Count} уведомлений");
            foreach (var n in notifications)
            {
                System.Diagnostics.Debug.WriteLine($"  - ID={n.ID_Notification}, Тип={n.NotificationType}, Сообщение={n.Message}");
            }
        }

        /// <summary>
        /// Проверка и обработка активной сессии работы за ПК
        /// </summary>
        private void CheckAndHandleComputerSession()
        {
            bool isLibrarianOrAdmin = _userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN");
            if (isLibrarianOrAdmin) return;

            try
            {
                var sessionService = new ComputerSessionService();
                var activeSessions = sessionService.GetActiveSessionsAsync().GetAwaiter().GetResult();
                var existingSession = activeSessions.FirstOrDefault(s => s.UserID == _currentUserId);

                if (existingSession != null)
                {
                    _currentSessionId = existingSession.ID_Session;
                    return;
                }

                string groupCode = string.Empty;
                if (_currentUserTypeId == 1)
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(
                            "SELECT ISNULL(g.GroupCode, '') FROM ReaderForm r LEFT JOIN Groups g ON r.GroupID = g.GroupID WHERE r.ReaderID = @ReaderId",
                            conn))
                        {
                            cmd.Parameters.AddWithValue("@ReaderId", _currentReaderId);
                            var result = cmd.ExecuteScalar();
                            groupCode = result != null ? result.ToString() : string.Empty;
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    var computerWindow = new ComputerSessionWindow(_currentUserId, _currentReaderId, _currentUserFullName, groupCode);
                    computerWindow.Owner = this;
                    computerWindow.ShowDialog();
                });

                var updatedSessions = sessionService.GetActiveSessionsAsync().GetAwaiter().GetResult();
                var newSession = updatedSessions.FirstOrDefault(s => s.UserID == _currentUserId);
                if (newSession != null) _currentSessionId = newSession.ID_Session;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Ошибка при работе с сессией ПК: " + ex.Message);
            }
        }

        /// <summary>
        /// Запуск фонового мониторинга сроков возврата книг (каждые 24 часа)
        /// </summary>
        private void StartReturnDateMonitoringBackground()
        {
            try
            {
                System.Threading.Thread.Sleep(30000);
                CheckUpcomingReturnDatesSync();

                _returnDateTimer = new System.Threading.Timer(_ =>
                {
                    CheckUpcomingReturnDatesSync();
                }, null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка запуска мониторинга: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка книг с приближающимся сроком возврата и отправка уведомлений
        /// Уведомления могут быть без привязки к заявке (RequestID = NULL)
        /// </summary>
        private void CheckUpcomingReturnDatesSync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            bi.IssuanceId,
                            bi.ReaderId,
                            bi.PlannedReturnDate,
                            b.TitleBook,
                            rf.ReaderID
                        FROM BookIssuance bi
                        INNER JOIN Books b ON bi.BookID = b.IDBook
                        LEFT JOIN ReaderForm rf ON bi.ReaderId = rf.ReaderID
                        WHERE 
                            bi.PlannedReturnDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE))
                            AND bi.ReturnDate IS NULL
                            AND (bi.IsNotified IS NULL OR bi.IsNotified = 0)
                        ORDER BY bi.PlannedReturnDate";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int issuanceId = reader.GetInt32(0);
                            int readerId = reader.GetInt32(1);
                            DateTime returnDate = reader.GetDateTime(2);
                            string bookTitle = reader.GetString(3);

                            int notificationTypeId;
                            using (var typeCmd = new SqlCommand("SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Скоро возврат'", conn))
                            {
                                var result = typeCmd.ExecuteScalar();
                                if (result == null)
                                {
                                    using (var insertTypeCmd = new SqlCommand(@"
                                        INSERT INTO NotificationTypes (TypeName, Description) 
                                        VALUES ('Скоро возврат', 'Уведомление о приближающемся сроке возврата');
                                        SELECT SCOPE_IDENTITY();", conn))
                                    {
                                        notificationTypeId = Convert.ToInt32(insertTypeCmd.ExecuteScalar());
                                    }
                                }
                                else
                                {
                                    notificationTypeId = Convert.ToInt32(result);
                                }
                            }

                            string insertSql = @"
                                INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                                VALUES (NULL, @NotificationTypeId, @Message, 0, GETDATE())";

                            using (var insertCmd = new SqlCommand(insertSql, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@NotificationTypeId", notificationTypeId);
                                insertCmd.Parameters.AddWithValue("@Message", $"⏰ Уважаемый читатель! Книга '{bookTitle}' должна быть возвращена до {returnDate:dd.MM.yyyy}");
                                insertCmd.ExecuteNonQuery();
                            }

                            string updateSql = "UPDATE BookIssuance SET IsNotified = 1 WHERE IssuanceId = @IssuanceId";
                            using (var updateCmd = new SqlCommand(updateSql, conn))
                            {
                                updateCmd.Parameters.AddWithValue("@IssuanceId", issuanceId);
                                updateCmd.ExecuteNonQuery();
                            }

                            System.Diagnostics.Debug.WriteLine($"✅ Отправлено уведомление о возврате книги '{bookTitle}' читателю {readerId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки сроков возврата: {ex.Message}");
            }
        }

        #endregion

        #region Прогресс загрузки страницы

        private void ShowLoadingOverlay(string message = "Загрузка...")
        {
            if (LoadingOverlay != null)
            {
                LoadingMessageText.Text = message;
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingOverlay.Opacity = 0;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void MainFrame_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            ShowLoadingOverlay("Загрузка страницы...");
        }

        private void MainFrame_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            HideLoadingOverlay();
        }

        private void MainFrame_NavigationFailed(object sender, System.Windows.Navigation.NavigationFailedEventArgs e)
        {
            HideLoadingOverlay();
            System.Diagnostics.Debug.WriteLine($"❌ Ошибка навигации: {e.Exception?.Message}");
            e.Handled = true;
        }

        #endregion

        #region Логика сворачивания меню

        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e) => ToggleMenu();

        private void ToggleMenu()
        {
            _isMenuExpanded = !_isMenuExpanded;

            var storyboard = (Storyboard)FindResource("CollapseMenuStoryboard");
            var animation = (DoubleAnimation)storyboard.Children[0];
            animation.To = _isMenuExpanded ? ExpandedWidth : CollapsedWidth;
            storyboard.Begin();

            SetMenuTextVisibility(_isMenuExpanded);
            UpdateToggleIcon();
        }

        private void UpdateToggleIcon()
        {
            ToggleIcon.Data = Geometry.Parse(_isMenuExpanded
                ? "M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z"
                : "M3 18h13v-2H3v2zm0-5h10v-2H3v2zm0-7v2h13V6H3z");
        }

        private void SetMenuTextVisibility(bool isVisible)
        {
            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

            MenuHeaderText.Visibility = visibility;
            UserInfoText.Visibility = visibility;
            NotificationText.Visibility = visibility;

            var logoutContent = BtnLogout.Content as StackPanel;
            if (logoutContent != null && logoutContent.Children.Count > 1)
            {
                var textElement = logoutContent.Children[1] as FrameworkElement;
                if (textElement != null) textElement.Visibility = visibility;
            }

            SetButtonTextVisibility(MenuAdmin, isVisible);
            SetButtonTextVisibility(MenuLibrarian, isVisible);
            SetButtonTextVisibility(MenuReader, isVisible);
        }

        private void SetButtonTextVisibility(Panel panel, bool isVisible)
        {
            if (panel == null) return;

            foreach (var child in panel.Children)
            {
                var btn = child as Button;
                if (btn?.Content is StackPanel sp && sp.Children.Count > 1)
                {
                    var textElement = sp.Children[1] as FrameworkElement;
                    if (textElement != null)
                        textElement.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth < 1200 && _isMenuExpanded) ToggleMenu();
            else if (ActualWidth >= 1400 && !_isMenuExpanded) ToggleMenu();
        }

        #endregion

        #region Навигация по приложению

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as Button;
            if (clickedButton == null) return;

            switch (clickedButton.Name)
            {
                case "BtnRequestBookAdmin":
                case "BtnRequestBookLib":
                case "BtnRequestBookReader":
                    MainFrame.Navigate(new RequestBook(_currentReaderId, _currentUserTypeId));
                    break;
                case "BtnRequestsManagementAdmin":
                case "BtnRequestsManagementLib":
                    MainFrame.Navigate(new BookRequestsManagement(_currentReaderId));
                    break;
                case "BtnMyRequestsReader":
                    MainFrame.Navigate(new MyRequestsPage(_currentReaderId));
                    break;
                case "BtnMyBooksReader":
                    MainFrame.Navigate(new MyBooksPage(_currentReaderId));
                    break;
                case "BtnInfoServiceAdmin":
                case "BtnInfoServiceLib":
                    MainFrame.Navigate(new InformationServicePage());
                    break;
                case "BtnReadersAdmin":
                case "BtnReadersLib":
                    MainFrame.Navigate(new Readers());
                    break;
                case "BtnLibraryFundAdmin":
                case "BtnLibraryFundLib":
                    MainFrame.Navigate(new LibraryFundPage());
                    break;
                case "BtnReportsAdmin":
                case "BtnReportsLib":
                    MainFrame.Navigate(new ReportsPage());
                    break;
                case "BtnSettingsAdmin":
                    MainFrame.Navigate(new Settings());
                    break;
                case "BtnPcManagementLib":
                    MainFrame.Navigate(new ComputerSessionsManagement());
                    break;
            }
        }

        #endregion

        #region Управление сессией работы за ПК

        private async Task EndComputerSessionAsync()
        {
            if (!_currentSessionId.HasValue) return;
            try
            {
                var sessionService = new ComputerSessionService();
                await sessionService.EndSessionAsync(_currentSessionId.Value);
                _currentSessionId = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Ошибка завершения сессии ПК: " + ex.Message);
            }
        }

        #endregion

        #region Выход из системы

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            var result = CustomMessageBox.Show("Вы точно хотите выйти?", "Подтверждение выхода",
                CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                await EndComputerSessionAsync();
                _notificationService?.StopPolling();
                _notificationService?.Dispose();
                _returnDateTimer?.Dispose();

                var authWindow = new AutorisationWindow();
                authWindow.Show();
                Close();
            }
            else _isLoggingOut = false;
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isLoggingOut)
            {
                var result = CustomMessageBox.Show("Вы точно хотите выйти и закрыть приложение?", "Подтверждение выхода",
                    CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);
                if (result != CustomMessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            await EndComputerSessionAsync();
            _notificationService?.StopPolling();
            _notificationService?.Dispose();
            _returnDateTimer?.Dispose();
            base.OnClosing(e);
        }

        #endregion

        #region Сервис уведомлений

        /// <summary>
        /// Обработчик получения нового уведомления
        /// Показывает всплывающее окно в правом нижнем углу
        /// </summary>
        private async void OnNewNotificationReceived(object sender, NotificationService.NotificationItem notification)
        {
            System.Diagnostics.Debug.WriteLine($"🔔 OnNewNotificationReceived: Тип={notification.NotificationType}, Message={notification.Message}");

            // Проверяем, что уведомление не null
            if (notification == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ Уведомление null");
                return;
            }

            // Для читателя - показываем все, кроме "Новая заявка"
            if (!(_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN")))
            {
                if (notification.NotificationType == "Новая заявка")
                {
                    System.Diagnostics.Debug.WriteLine($"⏭️ Пропущено уведомление для читателя: {notification.NotificationType}");
                    return;
                }
            }

            // Воспроизводим звук (в отдельном потоке, чтобы не блокировать)
            try
            {
                await Task.Run(() => SystemSounds.Beep.Play());
            }
            catch { }

            System.Diagnostics.Debug.WriteLine($"📨 ПОЛУЧЕНО УВЕДОМЛЕНИЕ В MAINWINDOW: {notification.NotificationType} - {notification.Message}");

            // Ограничение частоты показа уведомлений (не чаще чем раз в 1.5 секунды)
            var now = DateTime.Now;
            var timeSinceLast = now - _lastNotificationTime;
            if (timeSinceLast < _minNotificationInterval)
            {
                int delayMs = (int)(_minNotificationInterval - timeSinceLast).TotalMilliseconds;
                System.Diagnostics.Debug.WriteLine($"⏱️ Задержка показа уведомления: {delayMs} мс");
                await Task.Delay(delayMs);
            }
            _lastNotificationTime = DateTime.Now;

            // Обновляем счетчик в UI-потоке
            await Dispatcher.InvokeAsync(() =>
            {
                int unreadCount = _notificationService?.GetUnreadCount() ?? 0;
                UpdateUnreadCount(unreadCount);
                System.Diagnostics.Debug.WriteLine($"📊 Обновлен счетчик уведомлений: {unreadCount}");
            });

            // Небольшая задержка перед показом окна (даём время UI обновиться)
            await Task.Delay(100);

            // Показываем уведомление в UI-потоке
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🪟 Попытка показа уведомления, Тип={notification.NotificationType}, RequestID={notification.RequestID}");

                    if (notification.NotificationType == "Новая заявка" && notification.RequestID > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"📋 Показываем расширенное уведомление");
                        ShowRequestNotification(notification);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"💬 Показываем простое уведомление");
                        ShowFloatingNotification(notification);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка показа уведомления: {ex.Message}\n{ex.StackTrace}");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }


        /// <summary>
        /// Показ простого всплывающего уведомления
        /// </summary>
        private void ShowFloatingNotification(NotificationService.NotificationItem notification)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💬 ShowFloatingNotification вызван: {notification.NotificationType} - {notification.Message}");

                if (notification == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Уведомление null");
                    return;
                }

                // Создаём окно уведомления
                var notificationWindow = new Window
                {
                    Width = 380,
                    Height = 100,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    Left = SystemParameters.WorkArea.Width - 400,
                    Top = SystemParameters.WorkArea.Height - 120
                };

                // Основная панель
                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 }
                };

                // Содержимое
                var mainStack = new StackPanel();

                // Заголовок
                var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                string icon = GetNotificationIcon(notification.NotificationType);
                titlePanel.Children.Add(new TextBlock { Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = $" {notification.NotificationType}",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                    VerticalAlignment = VerticalAlignment.Center
                });
                mainStack.Children.Add(titlePanel);

                // Сообщение (обрезаем длинные сообщения)
                string shortMessage = notification.Message.Length > 80
                    ? notification.Message.Substring(0, 77) + "..."
                    : notification.Message;
                mainStack.Children.Add(new TextBlock
                {
                    Text = shortMessage,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                // Время
                mainStack.Children.Add(new TextBlock
                {
                    Text = DateTime.Now.ToString("HH:mm:ss"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                    HorizontalAlignment = HorizontalAlignment.Right
                });

                border.Child = mainStack;
                notificationWindow.Content = border;

                // Показываем окно
                notificationWindow.Show();
                System.Diagnostics.Debug.WriteLine("✅ Всплывающее окно показано");

                // Автоматическое закрытие через 5 секунд
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    notificationWindow.Close();
                    System.Diagnostics.Debug.WriteLine("🔄 Всплывающее окно закрыто по таймеру");
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка ShowFloatingNotification: {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает иконку (emoji) для типа уведомления
        /// </summary>
        private string GetNotificationIcon(string notificationType)
        {
            switch (notificationType)
            {
                case "Новая заявка": return "📋";
                case "Просрочена": return "⏰";
                case "Скоро возврат": return "📚";
                case "Занятие ПК": return "💻";
                default: return "🔔";
            }
        }
        /// <summary>
        /// Обработчик изменения количества непрочитанных уведомлений
        /// </summary>
        private void OnUnreadCountChanged(object sender, int unreadCount)
        {
            System.Diagnostics.Debug.WriteLine($"📊 OnUnreadCountChanged: {unreadCount}");
            Dispatcher.Invoke(() => UpdateUnreadCount(unreadCount));
        }

        /// <summary>
        /// Обновление счетчика непрочитанных уведомлений
        /// </summary>
        private void UpdateUnreadCount(int count)
        {
            TbUnreadCount.Text = count.ToString();
            NotificationBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"🔔 Обновлен счетчик уведомлений: {count}");
        }

        /// <summary>
        /// Обработчик клика по панели уведомлений - открытие страницы уведомлений
        /// </summary>
        private void NotificationPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_notificationService != null)
                MainFrame.Navigate(new NotificationsPage(_notificationService));
        }
       

        #endregion

        #region Вспомогательные методы

        private string GetRoleDisplayName()
        {
            if (_userRoles.Contains("ADMIN")) return "Администратор";
            if (_userRoles.Contains("LIBRARIAN")) return "Библиотекарь";
            if (_currentUserTypeId == 3) return "Преподаватель";
            if (_userRoles.Contains("READER")) return "Читатель (Студент)";
            return "Пользователь";
        }

        private void SetupMenuVisibility()
        {
            MenuAdmin.Visibility = Visibility.Collapsed;
            MenuLibrarian.Visibility = Visibility.Collapsed;
            MenuReader.Visibility = Visibility.Collapsed;

            if (_userRoles.Contains("ADMIN")) MenuAdmin.Visibility = Visibility.Visible;
            else if (_userRoles.Contains("LIBRARIAN")) MenuLibrarian.Visibility = Visibility.Visible;
            else MenuReader.Visibility = Visibility.Visible;
        }

        private int GetOrCreateNotificationTypeId(SqlConnection conn, string typeName, string description)
        {
            using (var cmd = new SqlCommand("SELECT ID_Type FROM NotificationTypes WHERE TypeName = @TypeName", conn))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var existingId = cmd.ExecuteScalar();
                if (existingId != null && existingId != DBNull.Value)
                    return Convert.ToInt32(existingId);
            }

            using (var insertCmd = new SqlCommand(@"
                INSERT INTO NotificationTypes (TypeName, Description)
                VALUES (@TypeName, @Description);
                SELECT SCOPE_IDENTITY();", conn))
            {
                insertCmd.Parameters.AddWithValue("@TypeName", typeName);
                insertCmd.Parameters.AddWithValue("@Description", description ?? string.Empty);
                var newId = insertCmd.ExecuteScalar();
                return Convert.ToInt32(newId);
            }
        }

        #endregion

        #region Уведомления о статусе заявок

        public async Task SendStatusChangeNotification(int requestId, string newStatus, string requestNumber)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        VALUES (@RequestID, (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Изменение статуса'), @Message, 0, GETDATE())", conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestID", requestId);
                        cmd.Parameters.AddWithValue("@Message", $"📋 Статус заявки №{requestNumber} изменён на '{newStatus}'");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
        }

        public async Task SendBooksReadyNotification(int requestId, string requestNumber, int booksCount)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        VALUES (@RequestID, (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Готова к выдаче'), @Message, 0, GETDATE())", conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestID", requestId);
                        cmd.Parameters.AddWithValue("@Message", $"✅ Заявка №{requestNumber} готова к выдаче! Выдано книг: {booksCount}");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
        }

        #endregion

        #region Всплывающие уведомления для заявок

        /// <summary>
        /// Показ расширенного уведомления для новой заявки
        /// </summary>
        /// <param name="notification">Объект уведомления для отображения</param>
        public void ShowRequestNotification(NotificationService.NotificationItem notification)
        {
            // Проверяем количество активных окон уведомлений (не более 3)
            lock (_notificationLock)
            {
                if (_activeNotificationWindows >= 3)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Слишком много активных уведомлений, пропускаем");
                    return;
                }
                _activeNotificationWindows++;
            }

            // Выполняем в UI-потоке
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Создаём окно уведомления
                    var notificationWindow = new Window
                    {
                        Width = 450,
                        Height = 420,
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Background = Brushes.Transparent,
                        Topmost = true,
                        ShowInTaskbar = false,
                        ResizeMode = ResizeMode.NoResize,
                        Left = SystemParameters.WorkArea.Width - 470,
                        Top = SystemParameters.WorkArea.Height - 440
                    };

                    // При закрытии окна уменьшаем счётчик активных окон
                    notificationWindow.Closed += (s, e) =>
                    {
                        lock (_notificationLock)
                        {
                            _activeNotificationWindows--;
                        }
                    };

                    // Основной контейнер с тенью
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Colors.White),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(15),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEE2E6")),
                        BorderThickness = new Thickness(1),
                        Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 3, Opacity = 0.15 }
                    };

                    var mainStack = new StackPanel();

                    // ========== ЗАГОЛОВОК ==========
                    var titlePanel = new Grid();
                    titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titlePanel.Margin = new Thickness(0, 0, 0, 10);

                    // Иконка уведомления
                    var iconText = new TextBlock
                    {
                        Text = GetNotificationIcon(notification.NotificationType),
                        FontSize = 28,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(iconText, 0);
                    titlePanel.Children.Add(iconText);

                    // Заголовок (тип уведомления)
                    var titleText = new TextBlock
                    {
                        Text = notification.NotificationType.ToUpper(),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0056B3")),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(titleText, 1);
                    titlePanel.Children.Add(titleText);

                    // Кнопка закрытия
                    var closeBtn = new Button
                    {
                        Content = "✕",
                        Width = 24,
                        Height = 24,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                        FontSize = 14,
                        Cursor = Cursors.Hand
                    };
                    closeBtn.Click += (s, ev) => notificationWindow.Close();
                    Grid.SetColumn(closeBtn, 2);
                    titlePanel.Children.Add(closeBtn);

                    mainStack.Children.Add(titlePanel);
                    mainStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEE2E6")), Margin = new Thickness(0, 0, 0, 12) });

                    // ========== ИНФОРМАЦИЯ О ЗАЯВИТЕЛЕ ==========
                    var userPanel = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var userStack = new StackPanel();
                    AddInfoRow(userStack, "👤 ЗАЯВИТЕЛЬ:", notification.UserName ?? "Не указано", true);
                    AddInfoRow(userStack, "📚 ГРУППА:", notification.GroupName ?? "Не указана", true);
                    userPanel.Child = userStack;
                    mainStack.Children.Add(userPanel);

                    // ========== ИНФОРМАЦИЯ О ЗАНЯТИИ ==========
                    mainStack.Children.Add(new TextBlock
                    {
                        Text = "ИНФОРМАЦИЯ О ЗАНЯТИИ",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                        Margin = new Thickness(0, 0, 0, 8)
                    });

                    var lessonContent = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var lessonStack = new StackPanel();
                    AddInfoRow(lessonStack, "📖 Занятие:", notification.LessonName ?? "Не указано", false);
                    AddInfoRow(lessonStack, "📅 Дата:", notification.LessonDate ?? "Не указана", false);
                    AddInfoRow(lessonStack, "⏰ Время:", notification.LessonTime ?? "Не указано", false);
                    lessonContent.Child = lessonStack;
                    mainStack.Children.Add(lessonContent);

                    // ========== ИНФОРМАЦИЯ О КНИГАХ ==========
                    mainStack.Children.Add(new TextBlock
                    {
                        Text = "ИНФОРМАЦИЯ О КНИГАХ",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                        Margin = new Thickness(0, 0, 0, 8)
                    });

                    var booksContent = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    var booksStack = new StackPanel();
                    AddInfoRow(booksStack, "📊 Количество книг:", notification.BooksCount.ToString(), false);
                    AddInfoRow(booksStack, "🔢 № заявки:", notification.RequestNumber, false);
                    if (!string.IsNullOrEmpty(notification.BookTitles))
                        AddInfoRow(booksStack, "📚 Книги:", notification.BookTitles.Length > 100 ? notification.BookTitles.Substring(0, 100) + "..." : notification.BookTitles, false);

                    booksContent.Child = booksStack;
                    mainStack.Children.Add(booksContent);

                    // ========== КНОПКА ПРОСМОТРА ==========
                    var viewButton = new Button
                    {
                        Content = "👁 Просмотреть заявку",
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0056B3")),
                        Foreground = Brushes.White,
                        Padding = new Thickness(15, 8, 15, 8),
                        Margin = new Thickness(0, 10, 0, 0),
                        Cursor = Cursors.Hand,
                        BorderThickness = new Thickness(0),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    viewButton.Click += (s, ev) =>
                    {
                        notificationWindow.Close();
                        MainFrame.Navigate(new BookRequestsManagement(_currentReaderId));
                    };
                    mainStack.Children.Add(viewButton);

                    // Собираем окно
                    border.Child = mainStack;
                    notificationWindow.Content = border;
                    notificationWindow.Show();

                    // Автоматическое закрытие через 10 секунд
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        notificationWindow.Close();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка показа уведомления: {ex.Message}");
                    lock (_notificationLock)
                    {
                        _activeNotificationWindows--;
                    }
                }
            });
        }

        #endregion

        /// <summary>
        /// Вспомогательный метод для добавления строки в уведомление
        /// </summary>
        private void AddInfoRow(StackPanel parent, string label, string value, bool isBold)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Margin = new Thickness(0, 0, 0, 5);

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var valueText = new TextBlock
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529")),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);

            parent.Children.Add(row);
        }


        #region Мониторинг просроченных заявок

        /// <summary>
        /// Запуск фонового мониторинга просроченных заявок
        /// Первая проверка через 1 минуту, затем каждый час
        /// </summary>
        private void StartExpiredRequestsMonitoring()
        {
            try
            {
                System.Threading.Thread.Sleep(60000);
                CheckExpiredRequestsSync();

                _expiredRequestsTimer = new System.Threading.Timer(_ =>
                {
                    CheckExpiredRequestsSync();
                }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

                System.Diagnostics.Debug.WriteLine("✅ Мониторинг просроченных заявок запущен");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка запуска мониторинга просроченных заявок: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка просроченных заявок и автоматическая их отмена
        /// Правила просрочки:
        /// - Для групповой заявки (преподаватель): +3 дня от даты занятия
        /// - Для индивидуальной заявки (студент): +1 день от даты создания
        /// </summary>
        private void CheckExpiredRequestsSync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                        -- Объявляем переменные со статусами
                        DECLARE @ExpiredStatusId INT = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Просрочена');
                        DECLARE @ProcessingStatusId INT = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'В обработке');
                        DECLARE @NewStatusId INT = (SELECT ID_Status FROM RequestStatuses WHERE StatusName = 'Новая');
                        DECLARE @CancelledItemStatusId INT = (SELECT ID_Status FROM RequestItemStatuses WHERE StatusName = 'Отменена');
                        DECLARE @AvailableBookStatusId INT = (SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%В наличии%');
                        DECLARE @ReservedBookStatusId INT = (SELECT ID_Status FROM StatusBook WHERE Title_Status LIKE '%Зарезервирована%');
                        
                        -- Если статусы не найдены, выходим
                        IF @ExpiredStatusId IS NULL OR @CancelledItemStatusId IS NULL 
                           OR @AvailableBookStatusId IS NULL OR @ReservedBookStatusId IS NULL
                        BEGIN
                            PRINT 'Не найдены необходимые статусы в БД';
                            RETURN;
                        END
                        
                        -- Таблица с просроченными заявками
                        DECLARE @ExpiredRequests TABLE (RequestId INT, RequestNumber NVARCHAR(30), UserTypeId INT, ExpirationDate DATE);
                        
                        INSERT INTO @ExpiredRequests (RequestId, RequestNumber, UserTypeId, ExpirationDate)
                        SELECT 
                            r.ID_Request,
                            r.RequestNumber,
                            r.UserTypeID,
                            CASE 
                                WHEN r.UserTypeID = 3 THEN DATEADD(DAY, 3, r.LessonDate)
                                ELSE DATEADD(DAY, 1, r.RequestDate)
                            END AS ExpirationDate
                        FROM BookRequests r
                        WHERE r.StatusID IN (@NewStatusId, @ProcessingStatusId)
                        AND (
                            CASE 
                                WHEN r.UserTypeID = 3 THEN DATEADD(DAY, 3, r.LessonDate)
                                ELSE DATEADD(DAY, 1, r.RequestDate)
                            END < GETDATE()
                        );
                        
                        -- Обрабатываем каждую просроченную заявку
                        DECLARE @RequestId INT, @RequestNumber NVARCHAR(30), @UserTypeId INT, @ExpDate DATE;
                        
                        DECLARE cur CURSOR FOR 
                            SELECT RequestId, RequestNumber, UserTypeId, ExpirationDate FROM @ExpiredRequests;
                        
                        OPEN cur;
                        FETCH NEXT FROM cur INTO @RequestId, @RequestNumber, @UserTypeId, @ExpDate;
                        
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            -- Возвращаем книги в статус 'В наличии'
                            UPDATE b
                            SET b.StatusBook = @AvailableBookStatusId
                            FROM Books b
                            WHERE b.StatusBook = @ReservedBookStatusId
                              AND EXISTS(
                                  SELECT 1 FROM RequestItems ri
                                  WHERE ri.RequestID = @RequestId AND ri.BookID = b.IDBook
                              );
                    
                            -- Отменяем позиции заявки
                            UPDATE RequestItems
                            SET StatusID = @CancelledItemStatusId
                            WHERE RequestID = @RequestId AND StatusID IN (1, 2);
                    
                            -- Меняем статус заявки на 'Просрочена'
                            UPDATE BookRequests
                            SET StatusID = @ExpiredStatusId
                            WHERE ID_Request = @RequestId;
                    
                            -- Добавляем уведомление о просрочке
                            INSERT INTO RequestNotifications(RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                            VALUES(
                                @RequestId,
                                (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Просрочена'),
                                CONCAT('⏰ Заявка №', @RequestNumber, ' просрочена! Вы не забрали книги в срок (', FORMAT(@ExpDate, 'dd.MM.yyyy'), '). Книги возвращены в доступные.'),
                                0,
                                GETDATE()
                            );
                    
                            FETCH NEXT FROM cur INTO @RequestId, @RequestNumber, @UserTypeId, @ExpDate;
                        END
                        
                        CLOSE cur;
                        DEALLOCATE cur;
                        
                        SELECT COUNT(*) AS ProcessedCount FROM @ExpiredRequests;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        int processedCount = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;

                        if (processedCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ Обработано просроченных заявок: {processedCount}");

                            Dispatcher.Invoke(() =>
                            {
                                _notificationService?.CheckNewNotifications();
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("📭 Просроченных заявок не найдено");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки просроченных заявок: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"📚 Стек вызова: {ex.StackTrace}");
            }
        }
        // Добавь этот метод в класс MainWindow
        private async Task ForceShowAllNotifications()
        {
            // Ждем, пока сервис уведомлений инициализируется
            int attempts = 0;
            while (_notificationService == null && attempts < 30)
            {
                await Task.Delay(500);
                attempts++;
            }

            if (_notificationService != null)
            {
                var notifications = _notificationService.GetNotifications();
                System.Diagnostics.Debug.WriteLine($"🔔 ПРИНУДИТЕЛЬНЫЙ ПОКАЗ ВСЕХ УВЕДОМЛЕНИЙ: {notifications.Count} шт.");

                foreach (var notification in notifications)
                {
                    if (!notification.IsRead)
                    {
                        System.Diagnostics.Debug.WriteLine($"🔔 ПОКАЗЫВАЕМ: ID={notification.ID_Notification}, Тип={notification.NotificationType}");
                        OnNewNotificationReceived(this, notification);
                    }
                }
            }
        }

        
        #endregion
    }
}