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
    /// <summary>
    /// Страница отображения уведомлений пользователя
    /// Показывает все уведомления из системы (новые заявки, просрочки, готовность к выдаче и т.д.)
    /// </summary>
    public partial class NotificationsPage : Page
    {
        #region Поля класса

        private readonly NotificationService _notificationService;      // Сервис для работы с уведомлениями
        private ObservableCollection<NotificationDisplayItem> _notifications;  // Коллекция уведомлений для отображения
        private bool _isLoading = false;                                   // Флаг предотвращения повторной загрузки

        #endregion

        #region Конструктор

        public NotificationsPage(NotificationService notificationService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _notifications = new ObservableCollection<NotificationDisplayItem>();

            System.Diagnostics.Debug.WriteLine("📱 СТРАНИЦА УВЕДОМЛЕНИЙ ОТКРЫТА");

            // Подписываемся на событие загрузки страницы
            this.Loaded += async (s, e) => await LoadNotificationsAsync();

            // Подписываемся на обновление уведомлений от сервиса
            _notificationService.NotificationsUpdated += (s, n) =>
            {
                System.Diagnostics.Debug.WriteLine("🔄 Событие NotificationsUpdated");
                // Обновляем список в UI-потоке
                Dispatcher.Invoke(async () => await LoadNotificationsAsync());
            };

            // Подписываемся на изменение количества непрочитанных уведомлений
            _notificationService.UnreadCountChanged += (s, count) =>
            {
                System.Diagnostics.Debug.WriteLine($"📊 UnreadCountChanged: {count}");
                Dispatcher.Invoke(() => UpdateUnreadBadge(count));
            };
        }

        #endregion

        #region Загрузка уведомлений

        /// <summary>
        /// Асинхронная загрузка списка уведомлений из сервиса
        /// </summary>
        private async Task LoadNotificationsAsync()
        {
            // Предотвращаем повторную загрузку, если уже идёт загрузка
            if (_isLoading) return;
            _isLoading = true;

            System.Diagnostics.Debug.WriteLine("📥 ЗАГРУЗКА УВЕДОМЛЕНИЙ");

            try
            {
                // Показываем индикатор загрузки
                await Dispatcher.InvokeAsync(() => ShowLoading(true));

                // Выполняем загрузку в фоновом потоке, чтобы не блокировать UI
                await Task.Run(() =>
                {
                    // Получаем уведомления из сервиса
                    var items = _notificationService.GetNotifications();

                    // Обновляем UI в потоке диспетчера
                    Dispatcher.Invoke(() =>
                    {
                        _notifications.Clear();
                        System.Diagnostics.Debug.WriteLine($"Получено уведомлений из сервиса: {items.Count}");

                        // Добавляем каждое уведомление в коллекцию для отображения
                        foreach (var item in items)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - ID={item.ID_Notification}: {item.Message}");
                            _notifications.Add(new NotificationDisplayItem(item));
                        }

                        // Привязываем коллекцию к ListBox
                        NotificationsList.ItemsSource = _notifications;

                        // Обновляем бейдж с количеством непрочитанных
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
                // Скрываем индикатор загрузки
                await Dispatcher.InvokeAsync(() => ShowLoading(false));
                _isLoading = false;
            }
        }

        #endregion

        #region Обновление UI

        /// <summary>
        /// Обновляет бейдж с количеством непрочитанных уведомлений
        /// </summary>
        /// <param name="count">Количество непрочитанных уведомлений</param>
        private void UpdateUnreadBadge(int count)
        {
            UnreadCountText.Text = count.ToString();
            UnreadBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Показывает/скрывает индикатор загрузки
        /// </summary>
        /// <param name="show">true - показать, false - скрыть</param>
        /// <param name="message">Текст сообщения при загрузке</param>
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

        #endregion

        #region Обработчики событий

        /// <summary>
        /// Обработчик кнопки "Отметить всё как прочитанное"
        /// Отмечает все уведомления прочитанными и обновляет список
        /// </summary>
        private async void MarkAllReadBtn_Click(object sender, RoutedEventArgs e)
        {
            // Блокируем кнопку, чтобы избежать повторных нажатий
            MarkAllReadBtn.IsEnabled = false;

            // Запрашиваем подтверждение у пользователя
            var result = CustomMessageBox.Show("Отметить все уведомления как прочитанные?\n\n" +
                "Это действие может занять некоторое время при большом количестве уведомлений.",
                "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                try
                {
                    ShowLoading(true, "Отметка уведомлений...");

                    // Выполняем операцию в фоновом потоке, чтобы не блокировать UI
                    await Task.Run(async () =>
                    {
                        await _notificationService.MarkAllAsReadAsync();
                    });

                    // Перезагружаем список уведомлений
                    await LoadNotificationsAsync();

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
        /// Обработчик кнопки обновления (Refresh)
        /// Принудительно перезагружает список уведомлений
        /// </summary>
        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadNotificationsAsync();
        }

        /// <summary>
        /// Обработчик кнопки "Назад"
        /// Возвращает на предыдущую страницу
        /// </summary>
        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        /// <summary>
        /// Обработчик клика по уведомлению
        /// Отмечает уведомление как прочитанное и открывает связанную заявку
        /// </summary>
        private async void Notification_Click(object sender, MouseButtonEventArgs e)
        {
            // Получаем уведомление из DataContext элемента
            if (sender is FrameworkElement element && element.DataContext is NotificationDisplayItem notification)
            {
                // Если уведомление не прочитано - отмечаем его как прочитанное
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
                    // Переходим на страницу управления заявками с низким приоритетом
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

        /// <summary>
        /// Обработчик кнопки "Просмотр" для перехода к заявке
        /// </summary>
        private async void ViewRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int requestId)
            {
                ShowLoading(true, "Загрузка заявки...");
                try
                {
                    // Небольшая задержка для корректного отображения индикатора
                    await Task.Delay(10);
                    NavigationService?.Navigate(new BookRequestsManagement(requestId));
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }

        /// <summary>
        /// Обработчик загрузки страницы
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshNotifications();
        }

        /// <summary>
        /// Принудительное обновление уведомлений с проверкой новых
        /// </summary>
        private async Task RefreshNotifications()
        {
            if (_notificationService != null)
            {
                // Проверяем наличие новых уведомлений в БД
                await _notificationService.CheckNewNotifications();
                System.Diagnostics.Debug.WriteLine("📱 Страница уведомлений принудительно обновлена");
                // Перезагружаем список
                await LoadNotificationsAsync();
            }
        }

        #endregion
    }

    /// <summary>
    /// Класс-обёртка для отображения уведомления с дополнительными UI-свойствами
    /// Используется для привязки данных в XAML
    /// </summary>
    public class NotificationDisplayItem
    {
        #region Поля

        private NotificationService.NotificationItem _source;  // Исходное уведомление из сервиса
        private bool _isRead;                                  // Флаг прочитанности (локальная копия)

        #endregion

        #region Конструктор

        public NotificationDisplayItem(NotificationService.NotificationItem source)
        {
            _source = source;
            _isRead = source.IsRead;
        }

        #endregion

        #region Свойства для отображения

        /// <summary>Уникальный ID уведомления</summary>
        public int ID_Notification => _source.ID_Notification;

        /// <summary>ID связанной заявки</summary>
        public int RequestID => _source.RequestID;

        /// <summary>Текст сообщения</summary>
        public string Message => _source.Message;

        /// <summary>Тип уведомления (Новая заявка, Просрочена, Готова к выдаче и т.д.)</summary>
        public string NotificationType => _source.NotificationType;

        /// <summary>Дата создания уведомления</summary>
        public DateTime CreatedDate => _source.CreatedDate;

        /// <summary>Статус прочитанности (локальная копия)</summary>
        public bool IsRead => _isRead;

        /// <summary>Номер заявки</summary>
        public string RequestNumber => _source.RequestNumber;

        /// <summary>Форматированная дата для отображения</summary>
        public string FormattedDate => CreatedDate.ToString("dd.MM.yyyy HH:mm");

        /// <summary>Цвет фона в зависимости от прочитанности (непрочитанные подсвечиваются)</summary>
        public string BackgroundColor => IsRead ? "White" : "#E3F2FD";

        /// <summary>Жирность шрифта (непрочитанные - жирные)</summary>
        public string FontWeight => IsRead ? "Normal" : "Bold";

        /// <summary>Текст иконки (эмодзи) в зависимости от типа уведомления</summary>
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

        /// <summary>Цвет иконки в зависимости от типа уведомления</summary>
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

        #endregion

        #region Методы

        /// <summary>
        /// Отмечает уведомление как прочитанное в локальной копии
        /// Используется для мгновенного обновления UI без перезагрузки всего списка
        /// </summary>
        public void MarkAsRead()
        {
            _isRead = true;
        }

        #endregion
    }
}