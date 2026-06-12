using Diplom_Bibl.Controls;
using Diplom_Bibl.Services;
using System;
using System.Collections.Generic;
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
        private readonly List<string> _userRoles;
        private bool _isLoading = false;
        private bool _isInitialLoad = true;

        // Константы для пагинации
        private const int PAGE_SIZE = 20;
        private int _currentPage = 0;
        private int _totalItems = 0;
        private ScrollViewer _scrollViewer;

        public NotificationsPage(NotificationService notificationService, List<string> userRoles)
        {
            InitializeComponent();
            _userRoles = userRoles ?? new List<string>();
            _notificationService = notificationService;
            _notifications = new ObservableCollection<NotificationDisplayItem>();

            System.Diagnostics.Debug.WriteLine("📱 СТРАНИЦА УВЕДОМЛЕНИЙ ОТКРЫТА");

            this.Loaded += async (s, e) =>
            {
                await LoadNotificationsAsync(true); // Загружаем только первую страницу
                SetupScrollListener();
            };

            _notificationService.NotificationsUpdated += async (s, n) =>
            {
                System.Diagnostics.Debug.WriteLine("🔄 Событие NotificationsUpdated");
                await Dispatcher.InvokeAsync(async () =>
                {
                    _currentPage = 0;
                    await LoadNotificationsAsync(true);
                });
            };

            _notificationService.UnreadCountChanged += (s, count) =>
            {
                System.Diagnostics.Debug.WriteLine($"📊 UnreadCountChanged: {count}");
                Dispatcher.Invoke(() => UpdateUnreadBadge(count));
            };
            
        }
        /// <summary>
        /// Проверка, является ли пользователь библиотекарем
        /// </summary>
        private bool IsLibrarian()
        {
            return _userRoles.Contains("LIBRARIAN");
        }
        /// <summary>
        /// Настройка слушателя прокрутки для бесконечной загрузки
        /// </summary>
        private void SetupScrollListener()
        {
            // Находим ScrollViewer внутри страницы
            _scrollViewer = FindVisualChild<ScrollViewer>(this);
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }
        }

        /// <summary>
        /// Поиск визуального элемента по типу
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        /// <summary>
        /// Обработчик прокрутки для подгрузки следующих страниц
        /// </summary>
        private async void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isLoading) return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            // Если доскроллили до конца
            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100)
            {
                if (_notifications.Count < _totalItems)
                {
                    await LoadMoreNotificationsAsync();
                }
            }
        }

        /// <summary>
        /// Асинхронная загрузка уведомлений с пагинацией
        /// </summary>
        /// <param name="reset">Очистить существующий список</param>
        private async Task LoadNotificationsAsync(bool reset = true)
        {
            if (_isLoading) return;
            _isLoading = true;

            System.Diagnostics.Debug.WriteLine("📥 ЗАГРУЗКА УВЕДОМЛЕНИЙ");

            try
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(true, "Загрузка уведомлений..."));
                bool isLibrarian = IsLibrarian();


                // Загружаем уведомления с пагинацией в фоновом потоке
                var (items, totalCount) = await Task.Run(() => GetNotificationsWithPagination(_currentPage, PAGE_SIZE));

                await Dispatcher.InvokeAsync(() =>
                {
                    if (reset)
                    {
                        _notifications.Clear();
                    }

                    _totalItems = totalCount;

                    foreach (var item in items)
                    {
                        _notifications.Add(new NotificationDisplayItem(item, isLibrarian));
                    }

                    NotificationsList.ItemsSource = _notifications;
                    UpdateUnreadBadge(_notificationService.GetUnreadCount());

                    System.Diagnostics.Debug.WriteLine($"✅ Загружено {_notifications.Count} из {_totalItems} уведомлений");

                    // Обновляем счётчик на странице
                    UpdatePageInfo();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    CustomMessageBox.Show($"Ошибка загрузки уведомлений: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => ShowLoading(false));
                _isLoading = false;
            }
        }

        /// <summary>
        /// Загрузка следующей страницы уведомлений
        /// </summary>
        private async Task LoadMoreNotificationsAsync()
        {
            if (_isLoading) return;

            _currentPage++;
            await LoadNotificationsAsync(false);
        }

        /// <summary>
        /// Получение уведомлений с пагинацией из сервиса
        /// </summary>
        private (ObservableCollection<NotificationService.NotificationItem>, int) GetNotificationsWithPagination(int page, int pageSize)
        {
            var allNotifications = _notificationService.GetNotifications();
            int totalCount = allNotifications.Count;

            var pagedNotifications = allNotifications
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new ObservableCollection<NotificationService.NotificationItem>();
            foreach (var item in pagedNotifications)
            {
                result.Add(item);
            }

            return (result, totalCount);
        }

        /// <summary>
        /// Обновление информации о странице
        /// </summary>
        private void UpdatePageInfo()
        {
            // Если есть элемент для отображения информации о пагинации
            var pageInfoText = FindName("PageInfoText") as TextBlock;
            if (pageInfoText != null)
            {
                int totalPages = (int)Math.Ceiling((double)_totalItems / PAGE_SIZE);
                pageInfoText.Text = $"Страница {_currentPage + 1} из {totalPages} (всего: {_totalItems})";
            }
        }

        private void UpdateUnreadBadge(int count)
        {
            UnreadCountText.Text = count.ToString();
            UnreadBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void MarkAllReadBtn_Click(object sender, RoutedEventArgs e)
        {
            MarkAllReadBtn.IsEnabled = false;

            var result = CustomMessageBox.Show("Отметить все уведомления как прочитанные?\n\n" +
                "Это действие может занять некоторое время при большом количестве уведомлений.",
                "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                try
                {
                    ShowLoading(true, "Отметка уведомлений...");

                    await Task.Run(async () =>
                    {
                        await _notificationService.MarkAllAsReadAsync();
                    });

                    _currentPage = 0;
                    await LoadNotificationsAsync(true);

                    CustomMessageBox.Show("Все уведомления отмечены как прочитанные.", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка при отметке уведомлений: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                finally
                {
                    ShowLoading(false);
                }
            }

            MarkAllReadBtn.IsEnabled = true;
        }

        /// <summary>
        /// Принудительное обновление (сброс кэша и перезагрузка)
        /// </summary>
        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 0;
            await RefreshNotifications();
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
                    _ = Task.Run(async () =>
                    {
                        await _notificationService.MarkAsReadAsync(notification.ID_Notification);
                        await Dispatcher.InvokeAsync(() => notification.MarkAsRead());
                    });
                }

                ShowLoading(true, "Загрузка страницы...");

                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        NavigationService?.Navigate(new BookRequestsManagement(notification.RequestID));
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка навигации: {ex.Message}");
                    CustomMessageBox.Show($"Ошибка открытия заявки: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                finally
                {
                    ShowLoading(false);
                }
            }
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
            if (_isInitialLoad)
            {
                _isInitialLoad = false;
                await RefreshNotifications();
            }
        }

        private async Task RefreshNotifications()
        {
            if (_notificationService != null)
            {
                await _notificationService.CheckNewNotifications();
                System.Diagnostics.Debug.WriteLine("📱 Страница уведомлений принудительно обновлена");
                await LoadNotificationsAsync(true);
            }
        }

        /// <summary>
        /// Показывает/скрывает индикатор загрузки
        /// </summary>
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
    }

    /// <summary>
    /// Класс для отображения уведомления с дополнительными свойствами
    /// </summary>
    public class NotificationDisplayItem
    {
        private NotificationService.NotificationItem _source;
        private bool _isRead;
        private bool _isLibrarian;

        public NotificationDisplayItem(NotificationService.NotificationItem source, bool isLibrarian)
        {
            _source = source;
            _isRead = source.IsRead;
            _isLibrarian = isLibrarian;
        }
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
                if (NotificationType == "Просрочена") return "⏰";
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
        public Visibility GoToButtonVisibility => _isLibrarian ? Visibility.Visible : Visibility.Collapsed;

        public void MarkAsRead()
        {
            _isRead = true;
        }
    }
}