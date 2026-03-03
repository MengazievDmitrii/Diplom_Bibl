using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
    public partial class ImportReaders : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";

        public ImportReaders()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel файлы|*.xls;*.xlsx|CSV файлы|*.csv|Все файлы|*.*",
                Title = "Выберите файл с данными читателей"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
                ImportButton.IsEnabled = true;

                // Очищаем предыдущие результаты
                ClearResults();
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePathTextBox.Text))
            {
                MessageBox.Show("Выберите файл для импорта.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(FilePathTextBox.Text))
            {
                MessageBox.Show("Файл не найден.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Блокируем кнопки
                ImportButton.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                ImportButton.Content = "⏳ Импорт...";

                // Сохраняем путь к файлу в локальную переменную ДО запуска фонового потока
                string filePath = FilePathTextBox.Text;

                // Выполняем импорт в фоновом потоке
                var result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var service = new Services.ExcelImportService(_connectionString);
                    return service.ImportFromFile(filePath); // Используем локальную переменную
                });

                // Отображаем результаты (уже в UI потоке)
                DisplayResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Разблокируем кнопки (в UI потоке)
                ImportButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
                ImportButton.Content = "📥 Импортировать";
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

            string message = result.HasErrors
                ? $"Импорт завершен с ошибками.\nУспешно: {result.SuccessRows}, Ошибок: {result.ErrorRows}"
                : $"Импорт успешно завершен!\nДобавлено читателей: {result.SuccessRows}";

            MessageBox.Show(message, "Результат импорта",
                MessageBoxButton.OK,
                result.HasErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
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

                MessageBox.Show($"CSV шаблон сохранён:\n{filePath}\n\n" +
                               "Заполните данные и сохраните файл.",
                    "Шаблон создан", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании шаблона: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ClearResults();
        }
    }
}