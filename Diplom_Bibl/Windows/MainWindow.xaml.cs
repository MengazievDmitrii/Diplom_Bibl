// Файл: MainWindow.xaml.cs
// Описание: Код окна главной навигации библиотечной системы НМК
// Язык: C# 7.3

using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
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
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Diplom_Bibl.Windows
{
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
        private SoundPlayer _notificationSound;
        private int? _currentSessionId;
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private bool _isLoggingOut = false;
        private System.Threading.Timer _returnDateTimer;
        private bool _isInitialized = false;

        #endregion

        #region Конструктор и инициализация (БЫСТРЫЙ)

        public MainWindow(int userId, int readerId, string fullName, List<string> roles, int userTypeId)
        {
            InitializeComponent();

            // ТОЛЬКО БЫСТРАЯ ИНИЦИАЛИЗАЦИЯ - без БД и тяжелых операций
            _currentUserId = userId;
            _currentReaderId = readerId;
            _currentUserTypeId = userTypeId;
            _currentUserFullName = fullName;
            _userRoles = roles ?? new List<string>();

            // Быстрая настройка UI
            TbUserName.Text = fullName ?? "Пользователь";
            TbUserRole.Text = GetRoleDisplayName();
            SetupMenuVisibility();

            // Подписка на события окна
            this.Loaded += Window_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
        }

        /// <summary>
        /// Обработчик загрузки окна - асинхронный
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Отписываемся, чтобы не вызвалось повторно
            this.Loaded -= Window_Loaded;
            await OnWindowLoadedAsync();
        }

        /// <summary>
        /// Асинхронная загрузка всех тяжелых операций ПОСЛЕ отображения окна
        /// </summary>
        private async Task OnWindowLoadedAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                // Показываем индикатор загрузки
                await Dispatcher.InvokeAsync(() => ShowLoadingOverlay(true));

                // Запускаем тяжелые операции НЕ в UI потоке
                var loadTasks = new List<Task>
                {
                    Task.Run(() => InitializeNotificationServiceSync()),
                    Task.Run(() => CheckAndHandleComputerSession()),
                    Task.Run(() => StartReturnDateMonitoringBackground())
                };

                // Ждем завершения всех фоновых задач
                await Task.WhenAll(loadTasks);

                // Загружаем начальную страницу в UI потоке
                await Dispatcher.InvokeAsync(() => LoadInitialPage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки: {ex.Message}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => ShowLoadingOverlay(false));
            }
        }

        /// <summary>
        /// Загрузка начальной страницы
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
        /// Синхронная инициализация сервиса уведомлений (запускается в фоновом потоке)
        /// </summary>
        private void InitializeNotificationServiceSync()
        {
            try
            {
                int notificationUserId;
                if (_userRoles.Contains("LIBRARIAN") || _userRoles.Contains("ADMIN"))
                    notificationUserId = _currentUserId;
                else
                    notificationUserId = _currentReaderId;

                // Используем Dispatcher.Invoke для создания объектов на UI потоке
                Dispatcher.Invoke(() =>
                {
                    _notificationService = new NotificationService(
                        _connectionString,
                        Dispatcher.CurrentDispatcher,
                        notificationUserId,
                        _currentUserTypeId,
                        _userRoles
                    );

                    _notificationService.NewNotificationReceived += OnNewNotificationReceived;
                    _notificationService.UnreadCountChanged += OnUnreadCountChanged;
                    _notificationService.StartPolling(10);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка инициализации уведомлений: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка и обработка компьютерной сессии (в фоновом потоке)
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
        /// Запуск фоновой проверки сроков возврата книг
        /// </summary>
        private void StartReturnDateMonitoringBackground()
        {
            try
            {
                // Задержка 30 секунд перед первым запуском
                System.Threading.Thread.Sleep(30000);

                // Запускаем первую проверку
                CheckUpcomingReturnDatesSync();

                // Запускаем таймер для следующей проверки
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
        /// Синхронная проверка приближающихся сроков возврата книг
        /// </summary>
        private void CheckUpcomingReturnDatesSync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT bi.ID_Issuance, bi.ReaderID, bi.PlannedReturnDate, br.RequestNumber, b.TitleBook
                        FROM BookIssuance bi
                        INNER JOIN Books b ON bi.BookID = b.IDBook
                        LEFT JOIN BookRequests br ON bi.RequestID = br.ID_Request
                        WHERE bi.PlannedReturnDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE))
                        AND bi.ReturnDate IS NULL AND (bi.IsNotified IS NULL OR bi.IsNotified = 0)";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int issuanceId = reader.GetInt32(0);
                            DateTime returnDate = reader.GetDateTime(2);
                            string requestNumber = reader.IsDBNull(3) ? "не указан" : reader.GetString(3);
                            string bookTitle = reader.GetString(4);

                            string insertSql = @"
                                INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                                VALUES (ISNULL((SELECT ID_Request FROM BookIssuance WHERE ID_Issuance = @IssuanceId), 0),
                                    (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Скоро возврат'),
                                    @Message, 0, GETDATE())";
                            using (var insertCmd = new SqlCommand(insertSql, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@IssuanceId", issuanceId);
                                insertCmd.Parameters.AddWithValue("@Message", $"⏰ Книга '{bookTitle}' по заявке №{requestNumber} должна быть возвращена до {returnDate:dd.MM.yyyy}");
                                insertCmd.ExecuteNonQuery();
                            }

                            string updateSql = "UPDATE BookIssuance SET IsNotified = 1 WHERE ID_Issuance = @IssuanceId";
                            using (var updateCmd = new SqlCommand(updateSql, conn))
                            {
                                updateCmd.Parameters.AddWithValue("@IssuanceId", issuanceId);
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки сроков возврата: {ex.Message}");
            }
        }

        /// <summary>
        /// Отображение индикатора загрузки
        /// </summary>
        private void ShowLoadingOverlay(bool show)
        {
            var overlay = FindName("LoadingOverlay") as Border;
            if (overlay != null)
            {
                overlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Логика сворачивания меню (БЫСТРАЯ - без БД)

        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenu();
        }

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
            BtnLogoutText.Visibility = visibility;

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
                if (btn != null)
                {
                    var sp = btn.Content as StackPanel;
                    if (sp != null && sp.Children.Count > 1)
                    {
                        var textElement = sp.Children[1] as FrameworkElement;
                        if (textElement != null)
                        {
                            textElement.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth < 1200 && _isMenuExpanded)
            {
                ToggleMenu();
            }
            else if (this.ActualWidth >= 1400 && !_isMenuExpanded)
            {
                ToggleMenu();
            }
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
                default:
                    System.Diagnostics.Debug.WriteLine("⚠️ Не обработан клик по кнопке: " + clickedButton.Name);
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
            var result = CustomMessageBox.Show("Вы точно хотите выйти?", "Подтверждение выхода", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                await EndComputerSessionAsync();
                if (_notificationService != null)
                {
                    _notificationService.StopPolling();
                    _notificationService.Dispose();
                }
                if (_returnDateTimer != null)
                {
                    _returnDateTimer.Dispose();
                }
                var autorisationWindow = new AutorisationWindow();
                autorisationWindow.Show();
                this.Close();
            }
            else _isLoggingOut = false;
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isLoggingOut)
            {
                var result = CustomMessageBox.Show("Вы точно хотите выйти и закрыть приложение?", "Подтверждение выхода", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);
                if (result != CustomMessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    _isLoggingOut = false;
                    return;
                }
            }
            await EndComputerSessionAsync();
            if (_notificationService != null)
            {
                _notificationService.StopPolling();
                _notificationService.Dispose();
            }
            if (_returnDateTimer != null)
            {
                _returnDateTimer.Dispose();
            }
            base.OnClosing(e);
        }

        #endregion

        #region Сервис уведомлений

        private void OnNewNotificationReceived(object sender, NotificationService.NotificationItem notification)
        {
            PlayNotificationSound();
            System.Diagnostics.Debug.WriteLine("🔔 НОВОЕ УВЕДОМЛЕНИЕ: " + notification.Message);

            Dispatcher.Invoke(() =>
            {
                ShowFloatingNotification(notification);
            });
        }

        private void ShowFloatingNotification(NotificationService.NotificationItem notification)
        {
            var notificationWindow = new Window
            {
                Width = 400,
                Height = 180,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Left = SystemParameters.WorkArea.Width - 420,
                Top = SystemParameters.WorkArea.Height - 200
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15),
                Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 3, Opacity = 0.3 }
            };

            var mainStack = new StackPanel();

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            string icon = "🔔";
            if (notification.NotificationType == "Новая заявка") icon = "📋";
            else if (notification.NotificationType == "Готова к выдаче") icon = "✅";
            else if (notification.NotificationType == "Просрочена") icon = "⚠️";
            else if (notification.NotificationType == "Занятие ПК") icon = "💻";

            titlePanel.Children.Add(new TextBlock { Text = icon, FontSize = 24, VerticalAlignment = VerticalAlignment.Center });
            titlePanel.Children.Add(new TextBlock
            {
                Text = $" {notification.NotificationType}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                VerticalAlignment = VerticalAlignment.Center
            });
            mainStack.Children.Add(titlePanel);

            mainStack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34495E")),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var messageText = new TextBlock
            {
                Text = notification.Message,
                FontSize = 12,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainStack.Children.Add(messageText);

            var timeText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            mainStack.Children.Add(timeText);

            border.Child = mainStack;
            notificationWindow.Content = border;
            notificationWindow.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (notificationWindow.IsVisible)
                    notificationWindow.Close();
            };
            timer.Start();
        }

        private void OnUnreadCountChanged(object sender, int unreadCount)
        {
            Dispatcher.Invoke(() => UpdateUnreadCount(unreadCount));
        }

        private void PlayNotificationSound()
        {
            try { SystemSounds.Beep.Play(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("⚠️ Ошибка воспроизведения звука: " + ex.Message); }
        }

        private void UpdateUnreadCount(int count)
        {
            TbUnreadCount.Text = count.ToString();
            TbUnreadCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotificationPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainFrame.Navigate(new NotificationsPage(_notificationService));
        }

        #endregion

        #region Вспомогательные методы (БЫСТРЫЕ)

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

        #endregion

        #region Уведомления о статусе заявок

        public async Task SendStatusChangeNotification(int requestId, string newStatus, string requestNumber)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string insertSql = @"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        VALUES (@RequestID, (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Изменение статуса'), @Message, 0, GETDATE())";
                    using (var cmd = new SqlCommand(insertSql, conn))
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
                    string insertSql = @"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        VALUES (@RequestID, (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Готова к выдаче'), @Message, 0, GETDATE())";
                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@RequestID", requestId);
                        cmd.Parameters.AddWithValue("@Message", $"✅ Заявка №{requestNumber} готова к выдаче! Выдано книг: {booksCount}");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
        }

        public async Task SendComputerSessionNotificationToLibrarian(string userName, string groupCode)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string insertSql = @"
                        INSERT INTO RequestNotifications (RequestID, NotificationTypeID, Message, IsRead, CreatedDate)
                        SELECT 0, (SELECT ID_Type FROM NotificationTypes WHERE TypeName = 'Занятие ПК'), @Message, 0, GETDATE()
                        WHERE EXISTS (SELECT 1 FROM ReaderForm WHERE TypeID = 2)";
                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        string groupInfo = string.IsNullOrEmpty(groupCode) ? "" : $" (группа {groupCode})";
                        cmd.Parameters.AddWithValue("@Message", $"💻 Студент {userName}{groupInfo} начал работу за компьютером");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
        }

        #endregion

        #region Всплывающие уведомления

        public void ShowRequestNotification(NotificationService.NotificationItem notification)
        {
            Dispatcher.Invoke(() =>
            {
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

                // Заголовок
                var titlePanel = new Grid();
                titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                titlePanel.Margin = new Thickness(0, 0, 0, 10);

                var iconText = new TextBlock { Text = "📋", FontSize = 28, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                Grid.SetColumn(iconText, 0);

                var titleText = new TextBlock
                {
                    Text = "НОВАЯ ЗАЯВКА НА КНИГИ",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0056B3")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleText, 1);

                var closeBtn = new Button
                {
                    Content = "✕",
                    Width = 24,
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                closeBtn.Click += (s, e) => notificationWindow.Close();
                Grid.SetColumn(closeBtn, 2);

                titlePanel.Children.Add(iconText);
                titlePanel.Children.Add(titleText);
                titlePanel.Children.Add(closeBtn);
                mainStack.Children.Add(titlePanel);
                mainStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEE2E6")), Margin = new Thickness(0, 0, 0, 12) });

                // Информация о заявителе
                var userPanel = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var userStack = new StackPanel();

                string userName = string.IsNullOrEmpty(notification.UserName) ? "Не указано" : notification.UserName;
                AddInfoRow(userStack, "👤 ЗАЯВИТЕЛЬ:", userName, true);

                string groupName = string.IsNullOrEmpty(notification.GroupName) ? "Не указана" : notification.GroupName;
                AddInfoRow(userStack, "📚 ГРУППА:", groupName, true);

                userPanel.Child = userStack;
                mainStack.Children.Add(userPanel);

                // Информация о занятии
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

                string lessonName = string.IsNullOrEmpty(notification.LessonName) ? "Не указано" : notification.LessonName;
                AddInfoRow(lessonStack, "📖 Занятие:", lessonName, false);

                string lessonDate = string.IsNullOrEmpty(notification.LessonDate) ? "Не указана" : notification.LessonDate;
                AddInfoRow(lessonStack, "📅 Дата:", lessonDate, false);

                string lessonTime = string.IsNullOrEmpty(notification.LessonTime) ? "Не указано" : notification.LessonTime;
                AddInfoRow(lessonStack, "⏰ Время:", lessonTime, false);

                lessonContent.Child = lessonStack;
                mainStack.Children.Add(lessonContent);

                // Информация о книгах
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
                AddInfoRow(booksStack, "📚 Количество книг:", notification.BooksCount.ToString(), false);
                AddInfoRow(booksStack, "🏷️ № заявки:", notification.RequestNumber, false);

                if (!string.IsNullOrEmpty(notification.BookTitles))
                {
                    AddInfoRow(booksStack, "📖 Книги:", notification.BookTitles.Length > 100 ? notification.BookTitles.Substring(0, 100) + "..." : notification.BookTitles, false);
                }

                booksContent.Child = booksStack;
                mainStack.Children.Add(booksContent);

                // Кнопка просмотра
                var viewButton = new Button
                {
                    Content = "📋 Просмотреть заявку",
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0056B3")),
                    Foreground = Brushes.White,
                    Padding = new Thickness(15, 8, 15, 8),
                    Margin = new Thickness(0, 10, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                viewButton.Click += (s, e) =>
                {
                    notificationWindow.Close();
                    MainFrame.Navigate(new BookRequestsManagement(notification.RequestID));
                };
                mainStack.Children.Add(viewButton);

                border.Child = mainStack;
                notificationWindow.Content = border;
                notificationWindow.Show();

                // Автоматическое закрытие через 12 секунд
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (notificationWindow.IsVisible)
                        notificationWindow.Close();
                };
                timer.Start();
            });
        }

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

            var valueText = new TextBlock
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529")),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valueText, 1);

            row.Children.Add(labelText);
            row.Children.Add(valueText);
            parent.Children.Add(row);
        }

        #endregion
    }
}