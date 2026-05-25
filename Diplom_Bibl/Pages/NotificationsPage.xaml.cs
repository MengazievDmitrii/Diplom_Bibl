using Diplom_Bibl.Controls;
using Diplom_Bibl.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class NotificationsPage : Page
    {
        private readonly NotificationService _notificationService;
        private ObservableCollection<NotificationDisplayItem> _notifications;

        public NotificationsPage(NotificationService notificationService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _notifications = new ObservableCollection<NotificationDisplayItem>();

            System.Diagnostics.Debug.WriteLine("📱 СТРАНИЦА УВЕДОМЛЕНИЙ ОТКРЫТА");

            LoadNotifications();

            _notificationService.NotificationsUpdated += (s, n) =>
            {
                System.Diagnostics.Debug.WriteLine("🔄 Событие NotificationsUpdated");
                Dispatcher.Invoke(() => LoadNotifications());
            };
            _notificationService.UnreadCountChanged += (s, count) =>
            {
                System.Diagnostics.Debug.WriteLine($"📊 UnreadCountChanged: {count}");
                Dispatcher.Invoke(() => UpdateUnreadBadge(count));
            };
        }

        private void LoadNotifications()
        {
            System.Diagnostics.Debug.WriteLine("📥 ЗАГРУЗКА УВЕДОМЛЕНИЙ");

            _notifications.Clear();

            var items = _notificationService.GetNotifications();
            System.Diagnostics.Debug.WriteLine($"Получено уведомлений из сервиса: {items.Count}");

            foreach (var item in items)
            {
                System.Diagnostics.Debug.WriteLine($"  - ID={item.ID_Notification}: {item.Message}");
                _notifications.Add(new NotificationDisplayItem(item));
            }

            NotificationsList.ItemsSource = _notifications;
            UpdateUnreadBadge(_notificationService.GetUnreadCount());
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
                await _notificationService.MarkAllAsReadAsync();
                LoadNotifications();
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadNotifications();
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
                if (!notification.IsRead)
                {
                    await _notificationService.MarkAsReadAsync(notification.ID_Notification);
                    notification.MarkAsRead();
                    LoadNotifications();
                }

                NavigationService?.Navigate(new BookRequestsManagement(notification.RequestID));
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