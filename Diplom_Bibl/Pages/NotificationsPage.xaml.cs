using Diplom_Bibl.Controls;
using Diplom_Bibl.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Diplom_Bibl.Pages
{
    public partial class NotificationsPage : Page
    {
        private readonly NotificationService _notificationService;
        private ObservableCollection<NotificationDisplayItem> _notifications;
        private bool _isLoading = false;

        public NotificationsPage(NotificationService notificationService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _notifications = new ObservableCollection<NotificationDisplayItem>();

            System.Diagnostics.Debug.WriteLine("📱 СТРАНИЦА УВЕДОМЛЕНИЙ ОТКРЫТА");

            // Асинхронная загрузка
            this.Loaded += async (s, e) => await LoadNotificationsAsync();

            _notificationService.NotificationsUpdated += (s, n) =>
            {
                System.Diagnostics.Debug.WriteLine("🔄 Событие NotificationsUpdated");
                Dispatcher.Invoke(async () => await LoadNotificationsAsync());
            };
            _notificationService.UnreadCountChanged += (s, count) =>
            {
                System.Diagnostics.Debug.WriteLine($"📊 UnreadCountChanged: {count}");
                Dispatcher.Invoke(() => UpdateUnreadBadge(count));
            };
        }

        private async Task LoadNotificationsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            System.Diagnostics.Debug.WriteLine("📥 ЗАГРУЗКА УВЕДОМЛЕНИЙ");

            try
            {
                // Показываем индикатор загрузки
                await Dispatcher.InvokeAsync(() => ShowLoading(true));

                // Загружаем уведомления в фоновом потоке
                await Task.Run(() =>
                {
                    var items = _notificationService.GetNotifications();
                    Dispatcher.Invoke(() =>
                    {
                        _notifications.Clear();
                        System.Diagnostics.Debug.WriteLine($"Получено уведомлений из сервиса: {items.Count}");

                        foreach (var item in items)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - ID={item.ID_Notification}: {item.Message}");
                            _notifications.Add(new NotificationDisplayItem(item));
                        }

                        NotificationsList.ItemsSource = _notifications;
                        UpdateUnreadBadge(_notificationService.GetUnreadCount());
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки: {ex.Message}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(false));
                _isLoading = false;
            }
        }

        private void ShowLoading(bool show)
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (MainContentGrid != null)
                MainContentGrid.IsEnabled = !show;
        }

        private void UpdateUnreadBadge(int count)
        {
            UnreadCountText.Text = count.ToString();
            UnreadBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void MarkAllReadBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show("Отметить все уведомления как прочитанные?",
                "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                try
                {
                    ShowLoading(true);
                    await _notificationService.MarkAllAsReadAsync();
                    await LoadNotificationsAsync();
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadNotificationsAsync();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private async void Notification_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is NotificationDisplayItem notification)
            {
                // Отмечаем как прочитанное (асинхронно, без ожидания)
                if (!notification.IsRead)
                {
                    await _notificationService.MarkAsReadAsync(notification.ID_Notification);
                    notification.MarkAsRead();
                    await LoadNotificationsAsync(); // Обновляем список
                }

                // Показываем индикатор загрузки перед навигацией
                ShowLoading(true, "Загрузка страницы...");

                try
                {
                    // Используем Dispatcher с низким приоритетом для навигации
                    await Dispatcher.InvokeAsync(() =>
                    {
                        NavigationService?.Navigate(new BookRequestsManagement(notification.RequestID));
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка навигации: {ex.Message}");
                    CustomMessageBox.Show($"Ошибка открытия заявки: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }
        private void ShowLoading(bool show, string message = "Загрузка...")
        {
            Dispatcher.Invoke(() =>
            {
                if (LoadingOverlay != null)
                {
                    if (LoadingText != null)
                        LoadingText.Text = message;
                    LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
                if (MainContentGrid != null)
                    MainContentGrid.IsEnabled = !show;
            });
        }
        private async void ViewRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int requestId)
            {
                ShowLoading(true, "Загрузка заявки...");
                try
                {
                    await Task.Delay(10);
                    NavigationService?.Navigate(new BookRequestsManagement(requestId));
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshNotifications();
        }

        private async Task RefreshNotifications()
        {
            if (_notificationService != null)
            {
                await _notificationService.CheckNewNotifications();
                System.Diagnostics.Debug.WriteLine("📱 Страница уведомлений принудительно обновлена");
                await LoadNotificationsAsync();
            }
        }
    }

    /// <summary>
    /// Класс для отображения уведомления с дополнительными свойствами
    /// </summary>
    public class NotificationDisplayItem
    {
        private NotificationService.NotificationItem _source;
        private bool _isRead;

        public NotificationDisplayItem(NotificationService.NotificationItem source)
        {
            _source = source;
            _isRead = source.IsRead;
        }

        public int ID_Notification => _source.ID_Notification;
        public int RequestID => _source.RequestID;
        public string Message => _source.Message;
        public string NotificationType => _source.NotificationType;
        public DateTime CreatedDate => _source.CreatedDate;
        public bool IsRead => _isRead;
        public string RequestNumber => _source.RequestNumber;
        public string FormattedDate => CreatedDate.ToString("dd.MM.yyyy HH:mm");

        public string BackgroundColor => IsRead ? "White" : "#E3F2FD";
        public string FontWeight => IsRead ? "Normal" : "Bold";

        public string IconText
        {
            get
            {
                if (NotificationType == "Новая заявка") return "📋";
                if (NotificationType == "Готова к выдаче") return "✅";
                if (NotificationType == "Просрочена") return "⚠️";
                if (NotificationType == "Отменена") return "❌";
                if (NotificationType == "Занятие ПК") return "💻";
                return "🔔";
            }
        }

        public Brush IconColor
        {
            get
            {
                if (NotificationType == "Новая заявка")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
                if (NotificationType == "Готова к выдаче")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                if (NotificationType == "Просрочена")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            }
        }
      
        public void MarkAsRead()
        {
            _isRead = true;
        }
    }
}