using Dadata;
using Dadata.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Diplom_Bibl.Controls
{
    public partial class AddressSearchTextBox : UserControl
    {
        // Читаем ключи из конфигурации
        private readonly string API_TOKEN = ConfigurationManager.AppSettings["DaDataApiKey"];
        private readonly string SECRET_KEY = ConfigurationManager.AppSettings["DaDataSecretKey"];

        private SuggestClientAsync _suggestClient;
        private CleanClientAsync _cleanClient;
        private DispatcherTimer _searchTimer;
        private bool _isInternetWarningShown = false; // флаг, чтобы не спамить сообщением

        public static readonly DependencyProperty SelectedAddressProperty =
            DependencyProperty.Register("SelectedAddress", typeof(string), typeof(AddressSearchTextBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SelectedAddress
        {
            get { return (string)GetValue(SelectedAddressProperty); }
            set { SetValue(SelectedAddressProperty, value); }
        }

        public string Text
        {
            get { return AddressTextBox.Text; }
            set { AddressTextBox.Text = value; }
        }

        public AddressSearchTextBox()
        {
            InitializeComponent();

            // Проверяем наличие ключей
            if (string.IsNullOrEmpty(API_TOKEN) || API_TOKEN == "fa8268132e66435ee0b5961551ed008c55eb8b9b")
            {
                System.Diagnostics.Debug.WriteLine("ВНИМАНИЕ: Не настроены API ключи DaData!");
                System.Diagnostics.Debug.WriteLine("Получите ключи на https://dadata.ru и добавьте в App.config");
            }

            InitializeDaDataClient();
            InitializeSearchTimer();
        }

        private void InitializeDaDataClient()
        {
            try
            {
                if (!string.IsNullOrEmpty(API_TOKEN))
                {
                    _suggestClient = new SuggestClientAsync(API_TOKEN);

                    if (!string.IsNullOrEmpty(SECRET_KEY))
                    {
                        _cleanClient = new CleanClientAsync(API_TOKEN, SECRET_KEY);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации DaData: {ex.Message}");
                ShowNoInternetMessage();
            }
        }

        private void InitializeSearchTimer()
        {
            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += async (s, e) =>
            {
                _searchTimer.Stop();
                await SearchAddresses(AddressTextBox.Text);
            };
        }

        /// <summary>
        /// Проверяет наличие интернет-соединения
        /// </summary>
        private bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    // Пинг до DNS Google (быстрый и надёжный)
                    var reply = ping.Send("8.8.8.8", 2000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Показывает предупреждение об отсутствии интернета (не чаще одного раза за сеанс)
        /// </summary>
        private void ShowNoInternetMessage()
        {
            if (!_isInternetWarningShown)
            {
                _isInternetWarningShown = true;
                // Используем тот же стиль сообщений, что и во всём проекте
                CustomMessageBox.Show("Отсутствует подключение к интернету. Поиск адресов недоступен.",
                    "Нет соединения", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
            }
        }

        private async Task SearchAddresses(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                SuggestionsPopup.IsOpen = false;
                return;
            }

            // Проверяем интернет-соединение
            if (!IsInternetAvailable())
            {
                ShowNoInternetMessage();
                SuggestionsPopup.IsOpen = false;
                return;
            }

            try
            {
                if (_suggestClient == null)
                {
                    // Если API не настроен, показываем тестовые данные
                    ShowTestData(query);
                    return;
                }

                var request = new SuggestAddressRequest(query)
                {
                    count = 10,
                    language = "ru",
                };

                var response = await _suggestClient.SuggestAddress(request);

                if (response?.suggestions != null && response.suggestions.Count > 0)
                {
                    var suggestions = new List<string>();
                    foreach (var suggestion in response.suggestions)
                    {
                        suggestions.Add(suggestion.value);
                    }

                    SuggestionsListBox.ItemsSource = suggestions;
                    SuggestionsPopup.IsOpen = true;
                }
                else
                {
                    SuggestionsPopup.IsOpen = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска адреса: {ex.Message}");
                // Если ошибка сетевого характера, показываем предупреждение
                if (ex is System.Net.Http.HttpRequestException ||
                    ex.Message.Contains("network") ||
                    ex.Message.Contains("internet"))
                {
                    ShowNoInternetMessage();
                }
                // В любом случае показываем тестовые данные для демонстрации
                ShowTestData(query);
            }
        }

        // Тестовые данные на случай отсутствия API или интернета
        private void ShowTestData(string query)
        {
            var testSuggestions = new List<string>
            {
               {query}

            };

            SuggestionsListBox.ItemsSource = testSuggestions;
            SuggestionsPopup.IsOpen = true;
        }

        private async void SuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsListBox.SelectedItem != null)
            {
                string selectedAddress = SuggestionsListBox.SelectedItem.ToString();

                // Если есть клиент для стандартизации, можно улучшить адрес
                if (_cleanClient != null && IsInternetAvailable())
                {
                    try
                    {
                        var cleanedAddress = await _cleanClient.Clean<Address>(selectedAddress);
                        selectedAddress = cleanedAddress.result ?? selectedAddress;
                    }
                    catch { /* Игнорируем ошибки стандартизации */ }
                }

                AddressTextBox.Text = selectedAddress;
                SelectedAddress = selectedAddress;
                SuggestionsPopup.IsOpen = false;
            }
        }

        public void Clear()
        {
            AddressTextBox.Clear();
            SelectedAddress = null;
            SuggestionsPopup.IsOpen = false;
        }

        private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!SuggestionsListBox.IsKeyboardFocusWithin)
                {
                    SuggestionsPopup.IsOpen = false;
                }
            }), DispatcherPriority.Background);
        }

        private void AddressTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (AddressTextBox.Text.Length >= 3)
            {
                AddressTextBox_TextChanged(sender, null);
            }
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Перезапускаем таймер при каждом изменении текста
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (SuggestionsPopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        if (SuggestionsListBox.SelectedIndex < SuggestionsListBox.Items.Count - 1)
                            SuggestionsListBox.SelectedIndex++;
                        e.Handled = true;
                        break;
                    case Key.Up:
                        if (SuggestionsListBox.SelectedIndex > 0)
                            SuggestionsListBox.SelectedIndex--;
                        e.Handled = true;
                        break;
                    case Key.Enter:
                        if (SuggestionsListBox.SelectedItem != null)
                        {
                            AddressTextBox.Text = SuggestionsListBox.SelectedItem.ToString();
                            SelectedAddress = AddressTextBox.Text;
                            SuggestionsPopup.IsOpen = false;
                        }
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        SuggestionsPopup.IsOpen = false;
                        e.Handled = true;
                        break;
                }
            }
        }
    }
}