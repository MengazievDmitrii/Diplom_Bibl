using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace Diplom_Bibl.Windows
{
    public partial class ComputerSessionWindow : Window
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private readonly int _userId;
        private readonly int _readerId;
        private readonly string _fullName;
        private readonly string _groupCode;
        private readonly ComputerSessionService _sessionService;
        private int _currentSessionId;
        private bool _isRegistered = false;

        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int GWL_STYLE = -16;

        public ComputerSessionWindow(int userId, int readerId, string fullName, string groupCode)
        {
            InitializeComponent();
            _userId = userId;
            _readerId = readerId;
            _fullName = fullName;
            _groupCode = groupCode;
            _sessionService = new ComputerSessionService();
            Loaded += ComputerSessionWindow_Loaded;
        }

        //загрузка окна записи на использование компьютером

        private void ComputerSessionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TbDateTime.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            TbFullName.Text = _fullName;

            if (!string.IsNullOrEmpty(_groupCode))
                TbGroup.Text = _groupCode;
            else
                GroupPanel.Visibility = Visibility.Collapsed;

            string computerNumber = ParseComputerNumber(Environment.MachineName);
            TbComputerNumber.Text = computerNumber;
            TbPurpose.Focus();
        }
        /// <summary>
        /// парсинг наименования компьютера из настроек системы
        /// </summary>
        /// <param name="machineName"></param>
        /// <returns></returns>
        private string ParseComputerNumber(string machineName)
        {
            if (string.IsNullOrEmpty(machineName)) return "Неизвестно";
            Match match = Regex.Match(machineName, @"[\d]+$");
            if (match.Success) return match.Value;
            return machineName.Length > 15 ? machineName.Substring(0, 15) : machineName;
        }

        private void LockMinimizeButton()
        {
            try
            {
                WindowInteropHelper helper = new WindowInteropHelper(this);
                IntPtr handle = helper.Handle;
                int style = NativeMethods.GetWindowLong(handle, GWL_STYLE);
                style &= ~WS_MINIMIZEBOX;
                NativeMethods.SetWindowLong(handle, GWL_STYLE, style);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка блокировки сворачивания: {ex.Message}"); }
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbPurpose.Text))
            {
                CustomMessageBox.Show("Укажите цель использования компьютера.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbPurpose.Focus();
                return;
            }

            try
            {
                _currentSessionId = await _sessionService.CreateSessionAsync(TbComputerNumber.Text, _userId, TbFullName.Text, TbGroup.Text, TbPurpose.Text.Trim());
                _isRegistered = true;

                BtnRegister.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                TbPurpose.IsEnabled = false;
                LockMinimizeButton();

                CustomMessageBox.Show("Вы успешно зарегистрированы!\n",
                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                //if (_currentSessionId > 0)
                //{
                //    var mainWindow = Application.Current.MainWindow as MainWindow;
                //    if (mainWindow != null)
                //        await mainWindow.SendComputerSessionNotificationToLibrarian(_fullName, _groupCode);
                //}
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка регистрации: {ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        }
    }
}