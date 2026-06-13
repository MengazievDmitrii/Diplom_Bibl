using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace Diplom_Bibl.Pages
{
    // Класс-обертка для отображения сессии с дополнительными свойствами
    public class SessionDisplayItem
    {
        public int ID_Session { get; set; }
        public string ComputerNumber { get; set; } = "";
        public int UserID { get; set; }
        public string FullName { get; set; } = "";
        public string GroupCode { get; set; } = "";
        public string Purpose { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; }
        public string Status { get; set; } = "";

        // Вычисляемые свойства для отображения
        public string StartTimeDisplay => StartTime.ToString("dd.MM.yyyy HH:mm:ss");
        public string EndTimeDisplay => EndTime?.ToString("dd.MM.yyyy HH:mm:ss") ?? "—";
        public string DurationDisplay => Duration.HasValue ? $"{Duration.Value} мин" : "—";

        // Текущая длительность для активных сессий
        public string CurrentDuration
        {
            get
            {
                if (Status != "Active") return DurationDisplay;
                TimeSpan duration = DateTime.Now - StartTime;
                return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }

        // Текст статуса на русском
        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case "Active": return "🟢 Активна";
                    case "Completed": return "✅ Завершена";
                    case "Terminated": return "⚠️ Прервана";
                    default: return "❓ Неизвестно";
                }
            }
        }

        // Цвет фона для статуса
        public SolidColorBrush StatusColor
        {
            get
            {
                switch (Status)
                {
                    case "Active":
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")); // Зеленый
                    case "Completed":
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A2B8")); // Голубой
                    case "Terminated":
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")); // Красный
                    default:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")); // Серый
                }
            }
        }

        // Статус активности
        public bool IsActive => Status == "Active";
    }

    public partial class ComputerSessionsManagement : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private readonly ComputerSessionService _sessionService;
        private readonly DispatcherTimer _refreshTimer;
        private readonly ObservableCollection<SessionDisplayItem> _activeSessions;
        private readonly ObservableCollection<SessionDisplayItem> _historySessions;

        public ComputerSessionsManagement()
        {
            InitializeComponent();
            _sessionService = new ComputerSessionService(_connectionString);
            _activeSessions = new ObservableCollection<SessionDisplayItem>();
            _historySessions = new ObservableCollection<SessionDisplayItem>();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await RefreshActiveSessionsAsync();

            DateFrom.SelectedDate = DateTime.Today;
            DateTo.SelectedDate = DateTime.Today;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync();
            _refreshTimer.Start();
        }

        private async Task RefreshAllAsync()
        {
            await RefreshActiveSessionsAsync();
            await RefreshHistoryAsync();
        }

        private async Task RefreshActiveSessionsAsync()
        {
            try
            {
                var sessions = await _sessionService.GetActiveSessionsAsync();

                await Dispatcher.InvokeAsync(() =>
                {
                    _activeSessions.Clear();
                    foreach (var session in sessions)
                    {
                        _activeSessions.Add(new SessionDisplayItem
                        {
                            ID_Session = session.ID_Session,
                            ComputerNumber = session.ComputerNumber,
                            UserID = session.UserID,
                            FullName = session.FullName,
                            GroupCode = session.GroupCode,
                            Purpose = session.Purpose,
                            StartTime = session.StartTime,
                            EndTime = session.EndTime,
                            Duration = session.Duration,
                            Status = session.Status
                        });
                    }

                    if (ActiveSessionsList != null)
                    {
                        ActiveSessionsList.ItemsSource = _activeSessions;
                        NoActiveSessionsLabel.Visibility = _activeSessions.Count == 0 ?
                            Visibility.Visible : Visibility.Collapsed;
                    }

                    UpdateStatistics();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления активных сессий: {ex.Message}");
            }
        }

        private async Task RefreshHistoryAsync()
        {
            try
            {
                DateTime? from = DateFrom.SelectedDate;
                DateTime? to = DateTo.SelectedDate?.AddDays(1);

                var sessions = await _sessionService.GetCompletedSessionsAsync(from, to);

                await Dispatcher.InvokeAsync(() =>
                {
                    _historySessions.Clear();
                    foreach (var session in sessions)
                    {
                        _historySessions.Add(new SessionDisplayItem
                        {
                            ID_Session = session.ID_Session,
                            ComputerNumber = session.ComputerNumber,
                            UserID = session.UserID,
                            FullName = session.FullName,
                            GroupCode = session.GroupCode,
                            Purpose = session.Purpose,
                            StartTime = session.StartTime,
                            EndTime = session.EndTime,
                            Duration = session.Duration,
                            Status = session.Status
                        });
                    }
                    HistoryGrid.ItemsSource = _historySessions;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления истории: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                ActiveComputersCount.Text = _activeSessions.Count.ToString();

                var todaySessions = _historySessions.Where(s => s.StartTime.Date == DateTime.Today).ToList();
                TotalSessionsToday.Text = todaySessions.Count.ToString();

                if (todaySessions.Any(s => s.Duration.HasValue))
                {
                    var avgMinutes = todaySessions.Where(s => s.Duration.HasValue).Average(s => s.Duration.Value);
                    AvgDuration.Text = $"{avgMinutes:F0} мин";
                }
                else
                {
                    AvgDuration.Text = "0 мин";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статистики: {ex.Message}");
            }
        }

        private async void ForceEndSession_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || btn.Tag == null) return;

            int sessionId = (int)btn.Tag;

            var result = CustomMessageBox.Show("Принудительно завершить сессию пользователя?",
                "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                bool success = await _sessionService.ForceEndSessionAsync(sessionId, "Завершено библиотекарем");

                if (success)
                {
                    CustomMessageBox.Show("Сессия успешно завершена.", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                    await RefreshAllAsync();
                }
                else
                {
                    CustomMessageBox.Show("Сессия уже завершена или не найдена.", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка завершения сессии: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            await RefreshHistoryAsync();
        }

        private async void RefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            await RefreshHistoryAsync();
        }

        private void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_historySessions.Count == 0)
            {
                CustomMessageBox.Show("Нет данных для экспорта.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = $"История_ПК_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    StringBuilder csv = new StringBuilder();
                    csv.AppendLine("ПК;ФИО;Группа;Цель;Начало;Конец;Длительность(мин);Статус");

                    foreach (var session in _historySessions)
                    {
                        csv.AppendLine($"{session.ComputerNumber};{session.FullName};{session.GroupCode};" +
                            $"{session.Purpose};{session.StartTimeDisplay};{session.EndTimeDisplay};" +
                            $"{session.Duration?.ToString() ?? "0"};{session.StatusDisplay}");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);

                    CustomMessageBox.Show($"Экспорт выполнен:\n{saveDialog.FileName}", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }
    }
}