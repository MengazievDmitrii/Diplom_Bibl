using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExcelDataReader;
using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class ImportReaders : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;

        public ImportReaders()
        {
            InitializeComponent();

            // Важно для поддержки русского языка в CSV
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Изначально кнопка импорта заблокирована
            ImportButton.IsEnabled = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel файлы|*.xlsx;*.xls|CSV файлы|*.csv|Все файлы|*.*",
                Title = "Выберите файл с данными читателей"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
                ImportButton.IsEnabled = true;

                // Очищаем предыдущие результаты
                ClearResults();
            }
            else
            {
                // Если пользователь отменил выбор файла, но в поле уже есть текст,
                // проверяем, существует ли файл
                if (!string.IsNullOrEmpty(FilePathTextBox.Text) && !File.Exists(FilePathTextBox.Text))
                {
                    FilePathTextBox.Text = "";
                    ImportButton.IsEnabled = false;
                    CustomMessageBox.Show("Выбранный файл не найден. Пожалуйста, выберите файл заново.",
                        "Файл не найден", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                }
            }
        }

        private void FilePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Если пользователь вручную очистил поле - блокируем кнопку импорта
            if (string.IsNullOrWhiteSpace(FilePathTextBox.Text))
            {
                ImportButton.IsEnabled = false;
            }
            else
            {
                // Если в поле есть текст, проверяем существование файла
                ImportButton.IsEnabled = File.Exists(FilePathTextBox.Text);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверка: выбран ли файл
            if (string.IsNullOrEmpty(FilePathTextBox.Text))
            {
                CustomMessageBox.Show("Выберите файл для импорта.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            // Проверка: существует ли файл
            if (!File.Exists(FilePathTextBox.Text))
            {
                CustomMessageBox.Show("Файл не найден. Пожалуйста, выберите файл заново.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                FilePathTextBox.Text = "";
                ImportButton.IsEnabled = false;
                return;
            }

            try
            {
                // Блокируем кнопки
                ImportButton.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                ImportButton.Content = "⏳ Импорт...";

                string filePath = FilePathTextBox.Text;
                string fileExtension = Path.GetExtension(filePath).ToLower();

                Services.ExcelImportService.ImportResult result = null;

                // Выполняем импорт в фоновом потоке
                result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var service = new Services.ExcelImportService(_connectionString);

                    // Для CSV файлов используем специальную обработку
                    if (fileExtension == ".csv")
                    {
                        return service.ImportFromCsv(filePath);
                    }
                    else
                    {
                        return service.ImportFromFile(filePath);
                    }
                });

                // Отображаем результаты
                DisplayResults(result);

                // Если импорт был успешным, очищаем поле выбора файла
                if (result.SuccessRows > 0)
                {
                    FilePathTextBox.Text = "";
                    ImportButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при импорте: {ex.Message}\n\nПопробуйте сохранить файл в формате CSV.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            finally
            {
                // Разблокируем кнопки
                BrowseButton.IsEnabled = true;
                ImportButton.Content = "📥 Импортировать";

                // Повторно проверяем состояние кнопки импорта
                ImportButton.IsEnabled = !string.IsNullOrEmpty(FilePathTextBox.Text) && File.Exists(FilePathTextBox.Text);
            }
        }

        private void DisplayResults(Services.ExcelImportService.ImportResult result)
        {
            TotalRowsText.Text = result.TotalRows.ToString();
            SuccessRowsText.Text = result.SuccessRows.ToString();
            ErrorRowsText.Text = result.ErrorRows.ToString();

            if (result.Errors.Count > 0)
            {
                ErrorsListBox.ItemsSource = result.Errors;
                ErrorsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorsListBox.Visibility = Visibility.Collapsed;
            }

            if (result.Warnings.Count > 0)
            {
                WarningsListBox.ItemsSource = result.Warnings;
                WarningsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                WarningsListBox.Visibility = Visibility.Collapsed;
            }

            // Показываем данные для входа
            if (result.UserCredentials.Count > 0)
            {
                string credentialsMessage = "✅ Созданы учётные записи для следующих читателей:\n\n" +
                                            string.Join("\n", result.UserCredentials) +
                                            "\n\n🔑 Роль: Читатель (READER)";

                CustomMessageBox.Show(credentialsMessage, "Данные для входа",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }

            string message = result.HasErrors
                ? $"⚠️ Импорт завершен с ошибками.\n✅ Успешно: {result.SuccessRows}, ❌ Ошибок: {result.ErrorRows}"
                : $"✅ Импорт успешно завершен!\n📚 Добавлено читателей: {result.SuccessRows}\n🔑 Для всех созданы учётные записи с ролью 'Читатель'";

            CustomMessageBox.Show(message, "Результат импорта",
                CustomMessageBoxButton.OK,
                result.HasErrors ? CustomMessageBoxImage.Warning : CustomMessageBoxImage.Information);
        }

        private void ClearResults()
        {
            TotalRowsText.Text = "0";
            SuccessRowsText.Text = "0";
            ErrorRowsText.Text = "0";
            ErrorsListBox.Visibility = Visibility.Collapsed;
            WarningsListBox.Visibility = Visibility.Collapsed;
        }

        private void DownloadTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV файл|*.csv",
                Title = "Сохранить шаблон",
                FileName = "Шаблон_импорта_читателей.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                CreateCsvTemplate(saveFileDialog.FileName);
            }
        }

        private void CreateCsvTemplate(string filePath)
        {
            try
            {
                var template = new StringBuilder();

                // Русские заголовки
                template.AppendLine("Номер читательского билета,Фамилия,Имя,Отчество,Тип читателя,Группа,Должность,Специальность,Телефон,Домашний адрес,Дата начала членства,Дата оформления");

                // Пример данных (студент)
                template.AppendLine("001,Иванов,Иван,Иванович,Студент,ИС-121п,,09.02.07 Информационные системы и программирование,\"+7 (999) 123-45-67\",\"г Москва ул Ленина д 1\",01.09.2024,01.09.2024");

                // Пример данных (сотрудник)
                template.AppendLine("002,Петров,Петр,Петрович,Сотрудник,,Библиотекарь,Высшее образование,\"+7 (999) 765-43-21\",\"г Москва ул Мира д 10\",15.01.2020,15.01.2020");

                // Пример с минимальными данными
                template.AppendLine("003,Сидоров,Сидор,,Студент,ИС-122п,,,,,,");

                File.WriteAllText(filePath, template.ToString(), Encoding.UTF8);

                CustomMessageBox.Show($"CSV шаблон сохранён:\n{filePath}\n\n" +
                    "Заполните данные и сохраните файл.\n\n" +
                    "💡 Подсказка: Для Excel откройте этот CSV файл и сохраните как XLSX.\n\n" +
                    "🔐 При импорте для каждого читателя автоматически:\n" +
                    "• Создаётся учётная запись\n" +
                    "• Логин = ФамилияИмя (например: ИвановИван)\n" +
                    "• Пароль = номер читательского билета (например: 001)\n" +
                    "• Назначается роль 'Читатель' (READER)",
                    "Шаблон создан", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при создании шаблона: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ClearResults();
        }
    }
}