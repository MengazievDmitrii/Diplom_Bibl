using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExcelDataReader;
using System.Data;

namespace Diplom_Bibl.Pages
{
    public partial class ImportReaders : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;

        public ImportReaders()
        {
            InitializeComponent();

            // Для EPPlus 4.5.3.3 установка лицензии НЕ ТРЕБУЕТСЯ
            // Просто удалите строку с ExcelPackage.LicenseContext

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
                ClearResults();
            }
            else
            {
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
            if (string.IsNullOrWhiteSpace(FilePathTextBox.Text))
            {
                ImportButton.IsEnabled = false;
            }
            else
            {
                ImportButton.IsEnabled = File.Exists(FilePathTextBox.Text);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePathTextBox.Text))
            {
                CustomMessageBox.Show("Выберите файл для импорта.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

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
                ImportButton.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                ImportButton.Content = "⏳ Импорт...";

                string filePath = FilePathTextBox.Text;
                string fileExtension = Path.GetExtension(filePath).ToLower();

                Services.ExcelImportService.ImportResult result = null;

                result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var service = new Services.ExcelImportService(_connectionString);

                    if (fileExtension == ".csv")
                    {
                        return service.ImportFromCsv(filePath);
                    }
                    else
                    {
                        return service.ImportFromFile(filePath);
                    }
                });

                DisplayResults(result);

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
                BrowseButton.IsEnabled = true;
                ImportButton.Content = "📥 Импортировать";
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
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                Title = "Сохранить шаблон",
                FileName = "Шаблон_импорта_читателей.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                CreateExcelTemplate(saveFileDialog.FileName);
            }
        }

        private void CreateExcelTemplate(string filePath)
        {
            try
            {
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Шаблон импорта");

                    // ========== ЗАГОЛОВКИ ==========
                    string[] headers = {
                        "Номер читательского билета",
                        "Фамилия",
                        "Имя",
                        "Отчество",
                        "Тип читателя",
                        "Группа",
                        "Должность",
                        "Специальность",
                        "Телефон",
                        "Домашний адрес",
                        "Дата начала членства",
                        "Дата оформления"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = headers[i];
                    }

                    // Стиль заголовков
                    using (var headerRange = worksheet.Cells[1, 1, 1, headers.Length])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }

                    // ========== ПРИМЕР ДАННЫХ ==========

                    // Студент
                    worksheet.Cells[2, 1].Value = "001";
                    worksheet.Cells[2, 2].Value = "Иванов";
                    worksheet.Cells[2, 3].Value = "Иван";
                    worksheet.Cells[2, 4].Value = "Иванович";
                    worksheet.Cells[2, 5].Value = "Студент";
                    worksheet.Cells[2, 6].Value = "ИС-121п";
                    worksheet.Cells[2, 7].Value = "";
                    worksheet.Cells[2, 8].Value = "09.02.07 Информационные системы и программирование";
                    worksheet.Cells[2, 9].Value = "+7 (999) 123-45-67";
                    worksheet.Cells[2, 10].Value = "г Москва ул Ленина д 1";
                    worksheet.Cells[2, 11].Value = "01.09.2024";
                    worksheet.Cells[2, 12].Value = "01.09.2024";

                    // Сотрудник
                    worksheet.Cells[3, 1].Value = "002";
                    worksheet.Cells[3, 2].Value = "Петров";
                    worksheet.Cells[3, 3].Value = "Петр";
                    worksheet.Cells[3, 4].Value = "Петрович";
                    worksheet.Cells[3, 5].Value = "Сотрудник";
                    worksheet.Cells[3, 6].Value = "";
                    worksheet.Cells[3, 7].Value = "Библиотекарь";
                    worksheet.Cells[3, 8].Value = "Высшее образование";
                    worksheet.Cells[3, 9].Value = "+7 (999) 765-43-21";
                    worksheet.Cells[3, 10].Value = "г Москва ул Мира д 10";
                    worksheet.Cells[3, 11].Value = "15.01.2020";
                    worksheet.Cells[3, 12].Value = "15.01.2020";

                    // Минимальные данные
                    worksheet.Cells[4, 1].Value = "003";
                    worksheet.Cells[4, 2].Value = "Сидоров";
                    worksheet.Cells[4, 3].Value = "Сидор";
                    worksheet.Cells[4, 4].Value = "";
                    worksheet.Cells[4, 5].Value = "Студент";
                    worksheet.Cells[4, 6].Value = "ИС-122п";
                    worksheet.Cells[4, 7].Value = "";
                    worksheet.Cells[4, 8].Value = "";
                    worksheet.Cells[4, 9].Value = "";
                    worksheet.Cells[4, 10].Value = "";
                    worksheet.Cells[4, 11].Value = "";
                    worksheet.Cells[4, 12].Value = "";

                    // ========== ОФОРМЛЕНИЕ ==========

                    worksheet.Cells.AutoFitColumns();
                    worksheet.Cells[1, 1, 1, headers.Length].AutoFilter = true;
                    worksheet.View.FreezePanes(2, 1);

                    // ========== ЛИСТ С ИНСТРУКЦИЕЙ ==========
                    var instructionSheet = package.Workbook.Worksheets.Add("Инструкция");

                    instructionSheet.Cells[1, 1].Value = "ИНСТРУКЦИЯ ПО ЗАПОЛНЕНИЮ ШАБЛОНА";
                    instructionSheet.Cells[1, 1, 1, 2].Merge = true;
                    instructionSheet.Cells[1, 1].Style.Font.Bold = true;
                    instructionSheet.Cells[1, 1].Style.Font.Size = 14;
                    instructionSheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    instructionSheet.Cells[3, 1].Value = "1. Обязательные колонки:";
                    instructionSheet.Cells[4, 1].Value = "   - Номер читательского билета (уникальный)";
                    instructionSheet.Cells[5, 1].Value = "   - Фамилия";
                    instructionSheet.Cells[6, 1].Value = "   - Имя";
                    instructionSheet.Cells[7, 1].Value = "   - Тип читателя (Студент или Сотрудник)";
                    instructionSheet.Cells[7, 1].Style.Font.Bold = true;

                    instructionSheet.Cells[9, 1].Value = "2. Тип читателя определяет заполнение полей:";
                    instructionSheet.Cells[10, 1].Value = "   - Для студента: заполните колонку 'Группа' и 'Специальность'";
                    instructionSheet.Cells[11, 1].Value = "   - Для сотрудника: заполните колонку 'Должность'";
                    instructionSheet.Cells[12, 1].Value = "   - Остальные колонки необязательны для обоих типов";

                    instructionSheet.Cells[14, 1].Value = "3. Даты указывайте в формате ДД.ММ.ГГГГ";
                    instructionSheet.Cells[15, 1].Value = "4. Телефон можно указывать в любом формате";
                    instructionSheet.Cells[16, 1].Value = "5. Не изменяйте названия колонок!";
                    instructionSheet.Cells[17, 1].Value = "6. Если данные не нужны для конкретного читателя, оставляйте ячейку пустой";

                    instructionSheet.Cells[19, 1].Value = "=======================================================";
                    instructionSheet.Cells[20, 1].Value = "🔐 ПРИ ИМПОРТЕ АВТОМАТИЧЕСКИ СОЗДАЮТСЯ УЧЁТНЫЕ ЗАПИСИ:";
                    instructionSheet.Cells[21, 1].Value = "   • Логин = номер читательского билета";
                    instructionSheet.Cells[22, 1].Value = "   • Пароль = номер читательского билета";
                    instructionSheet.Cells[23, 1].Value = "   • Роль = Читатель (READER)";

                    instructionSheet.Column(1).AutoFit();
                    instructionSheet.View.TabSelected = true;

                    File.WriteAllBytes(filePath, package.GetAsByteArray());
                }

                CustomMessageBox.Show(
                    $"📋 Excel шаблон сохранён:\n{filePath}\n\n" +
                    "📌 Инструкция:\n" +
                    "• Заполните данные на листе 'Шаблон импорта'\n" +
                    "• Не изменяйте названия колонок!\n" +
                    "• Обязательные поля: Номер билета, Фамилия, Имя, Тип читателя\n" +
                    "• Даты указывайте в формате ДД.ММ.ГГГГ\n\n" +
                    "🔐 При импорте для каждого читателя автоматически:\n" +
                    "• Создаётся учётная запись\n" +
                    "• Логин и пароль = номер читательского билета\n" +
                    "• Назначается роль 'Читатель' (READER)\n\n" +
                    "💡 Подробная инструкция на листе 'Инструкция'",
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