using Diplom_Bibl.Controls;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Diplom_Bibl
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Установка лицензии НЕ нужна для Microsoft.Office.Interop.Excel
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            ShowFatalError(exception?.Message ?? "Неизвестная ошибка");
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowFatalError(e.Exception.Message);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowFatalError(e.Exception.Message);
            e.SetObserved();
        }

        private void ShowFatalError(string message)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var result = CustomMessageBox.Show(
                        $"Программа не отвечает.\n\nОшибка: {message}\n\nРекомендуется перезапустить приложение.\n\nНажмите ОК для выхода.",
                        "Критическая ошибка",
                        CustomMessageBoxButton.OK,
                        CustomMessageBoxImage.Error);

                    if (result == CustomMessageBoxResult.OK)
                    {
                        Shutdown();
                    }
                });
            }
            catch
            {
                // Если не удалось показать сообщение, просто выходим
                Environment.Exit(1);
            }
        }

    }
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    public class InvertBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Collapsed;
        }
    }

    public class NotificationTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string type = value as string;
            if (type == "Новая заявка") return "📋";
            if (type == "Готова к выдаче") return "✅";
            if (type == "Просрочена") return "⚠️";
            if (type == "Отменена") return "❌";
            if (type == "Занятие ПК") return "💻";
            if (type == "Выдача оформлена") return "📚";
            if (type == "Скоро возврат") return "⏰";
            return "🔔";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (status == "Новая заявка") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            if (status == "Готова к выдаче") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            if (status == "Просрочена") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            if (status == "Отменена") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
            if (status == "Занятие ПК") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B59B6"));
            if (status == "Выдача оформлена") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"));
            if (status == "Скоро возврат") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1C40F"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRead = value is true;
            if (isRead)
                return new SolidColorBrush(Colors.White);
            else
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRead = value is true;
            return isRead ? FontWeights.Normal : FontWeights.Bold;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }
}
