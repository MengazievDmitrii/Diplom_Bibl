using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class InventoryActReport : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<BookInventoryItem> _books;

        // Удаляем приватное поле _bookcategory — теперь используем публичное свойство

        private int _nextRowNumber = 1;

        // Публичное свойство для привязки данных
        public ObservableCollection<CategoryBooksItems> BookCategories { get; set; }

        public class BookInventoryItem
        {
            public int IDBook { get; set; }
            public int RowNumber { get; set; }
            public string BookFullTitle { get; set; } = "";
            public int YearPublishing { get; set; }
            public decimal Price { get; set; }
            public string InventaryNumber { get; set; } = "";
            public string Category { get; set; } = "";
            public string VerificationStatus { get; set; } = "В наличии";
            public string ReasonMissing { get; set; } = "";
            public bool CanEdit { get; set; } = true;
        }

        public class CategoryBooksItems
        {
            public int ID_Category { get; set; }
            public string CategoryName { get; set; }
            public string Description { get; set; }
        }

        public ObservableCollection<string> StatusOptions { get; set; }

        public InventoryActReport()
        {
            InitializeComponent();
            StatusOptions = new ObservableCollection<string> { "В наличии", "Отсутствует" };

            // Инициализация коллекции категорий
            BookCategories = new ObservableCollection<CategoryBooksItems>();

            DataContext = this;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StartDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
                EndDatePicker.SelectedDate = DateTime.Now;
                await LoadLibrarySettings();
                await LoadCommissionInfo();
                await LoadBooks();
                await LoadBookCatigories();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadLibrarySettings()
        {
            try
            {
                const string sql = "SELECT TOP 1 OrganizationName FROM LibrarySettings ORDER BY ID_Setting";
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null)
                        {
                            OrganizationName.Text = result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadCommissionInfo()
        {
            try
            {
                const string sql = @"
                    SELECT TOP 1 ChairmanName, ChairmanPosition, 
                           Member1Name, Member1Position,
                           Member2Name, Member2Position,
                           OrderNumber, OrderDate
                    FROM InventoryCommissions WHERE IsActive = 1
                    ORDER BY CommissionDate DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            ChairmanInfo.Text = $"Председатель: {reader["ChairmanPosition"]} {reader["ChairmanName"]}";

                            string member1 = reader["Member1Name"]?.ToString();
                            if (!string.IsNullOrEmpty(member1))
                                Member1Info.Text = $"Члены комиссии: {reader["Member1Position"]} {member1}";

                            string member2 = reader["Member2Name"]?.ToString();
                            if (!string.IsNullOrEmpty(member2))
                                Member2Info.Text = $"{reader["Member2Position"]} {member2}";

                            string orderNumber = reader["OrderNumber"]?.ToString();
                            DateTime? orderDate = reader["OrderDate"] != DBNull.Value ? Convert.ToDateTime(reader["OrderDate"]) : (DateTime?)null;

                            if (!string.IsNullOrEmpty(orderNumber) && orderDate.HasValue)
                            {
                                OrderInfo.Text = $"Основание: приказ {orderNumber} от {orderDate.Value:dd.MM.yyyy}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки комиссии: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadBooks()
        {
            try
            {
                string query = @"
                    SELECT 
                        b.IDBook,
                        CONCAT(a.SurnameAuthor, ' ', a.NameAuthor, ' ', ISNULL(a.PatronymicAuthor, ''), ' — ', b.TitleBook) AS BookFullTitle,
                        b.YearPublishing,
                        ISNULL(b.Price, 0) AS Price,
                        b.InventaryNumber,
                        bc.CategoryName AS Category
                    FROM Books b
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN BookCategories bc ON b.CategoryID = bc.ID_Category
                    ORDER BY b.TitleBook";

                _books = new ObservableCollection<BookInventoryItem>();
                int rowNum = 1;

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _books.Add(new BookInventoryItem
                            {
                                IDBook = reader.GetInt32(0),
                                RowNumber = rowNum++,
                                BookFullTitle = reader.GetString(1),
                                YearPublishing = reader.GetInt32(2),
                                Price = reader.GetDecimal(3),
                                InventaryNumber = reader.GetString(4),
                                Category = reader.IsDBNull(5) ? "Не указана" : reader.GetString(5),
                                VerificationStatus = "В наличии",
                                ReasonMissing = ""
                            });
                        }
                    }
                }

                _nextRowNumber = rowNum;
                ApplyFilters();
                UpdateTotals();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private System.Threading.Tasks.Task LoadBookCatigories()
        {
            return LoadBookCatigories(CategoryFilterCombo);
        }

        private async System.Threading.Tasks.Task LoadBookCatigories(ComboBox categoryFilterCombo)
        {
            try
            {
                string query = @"
            SELECT 
                bc.ID_Category,            
                bc.CategoryName AS Category
            FROM BookCategories bc
            ORDER BY bc.CategoryName";

                // Очищаем существующую коллекцию
                BookCategories.Clear();

                // Добавляем «Все книги» как первый элемент
                BookCategories.Add(new CategoryBooksItems
                {
                    ID_Category = -1, // специальный ID для фильтра «Все»
                    CategoryName = "Все книги"
                });

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            BookCategories.Add(new CategoryBooksItems
                            {
                                ID_Category = reader.GetInt32(0),
                                CategoryName = reader.IsDBNull(1) ? "Не указана" : reader.GetString(1),
                            });
                        }
                    }
                }

                ApplyFilters();
                UpdateTotals();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки категории книг: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }




        private void ApplyFilters()
        {
            if (_books == null) return;

            var filtered = _books.AsEnumerable();

            string statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все книги";
            if (statusFilter == "В наличии")
                filtered = filtered.Where(b => b.VerificationStatus == "В наличии");
            else if (statusFilter == "Отсутствуют")
                filtered = filtered.Where(b => b.VerificationStatus == "Отсутствует");

            string categoryFilter = (CategoryFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все книги";
            if (categoryFilter != "Все книги")
                filtered = filtered.Where(b => b.Category == categoryFilter);

            BooksDataGrid.ItemsSource = filtered.ToList();
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            var currentList = BooksDataGrid.ItemsSource as System.Collections.IEnumerable;
            if (currentList == null) return;

            var list = currentList.Cast<BookInventoryItem>().ToList();
            TotalBooksCount.Text = list.Count.ToString();
            BooksPresentCount.Text = list.Count(b => b.VerificationStatus == "В наличии").ToString();
            BooksMissingCount.Text = list.Count(b => b.VerificationStatus == "Отсутствует").ToString();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (_books == null || _books.Count == 0)
            {
                CustomMessageBox.Show("Нет данных для формирования отчета.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            CustomMessageBox.Show("Акт сформирован. Проверьте данные в таблице и при необходимости отредактируйте статус проверки.\n\nЗатем экспортируйте в Excel.",
                "Отчет готов", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            var items = BooksDataGrid.ItemsSource as System.Collections.IEnumerable;
            if (items == null)
            {
                CustomMessageBox.Show("Нет данных для экспорта.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var data = items.Cast<BookInventoryItem>().ToList();
            if (data.Count == 0)
            {
                CustomMessageBox.Show("Нет данных для экспорта.", "Информация",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Акт_проверки_фонда_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToExcel(data, saveFileDialog.FileName);
                    CustomMessageBox.Show($"Отчёт сохранён:\n{saveFileDialog.FileName}", "Успешно",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                        CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void ExportToExcel(System.Collections.Generic.List<BookInventoryItem> data, string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = false;

                workbook = excelApp.Workbooks.Add();
                worksheet = workbook.ActiveSheet;
                worksheet.Name = "Акт проверки фонда";

                int currentRow = 1;

                // Заголовок организации
                worksheet.Cells[currentRow, 1] = OrganizationName.Text;
                var titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 14;
                titleRange.Merge(worksheet.Cells[currentRow, 7]);
                currentRow += 2;

                // Название акта
                worksheet.Cells[currentRow, 1] = $"АКТ № ___ от \"__\" _______ 20__ г.";
                titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 16;
                titleRange.Merge(worksheet.Cells[currentRow, 7]);
                currentRow++;

                worksheet.Cells[currentRow, 1] = "проверки библиотечного фонда";
                titleRange = (Excel.Range)worksheet.Cells[currentRow, 1];
                titleRange.Font.Size = 14;
                titleRange.Merge(worksheet.Cells[currentRow, 7]);
                currentRow += 2;

                // Основание
                worksheet.Cells[currentRow, 1] = OrderInfo.Text;
                currentRow += 2;

                // Заголовки таблицы
                string[] headers = { "№ п/п", "Автор, заглавие", "Год издания", "Цена", "Инвентарный номер", "Отметка о проверке", "Причина отсутствия" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[currentRow, i + 1] = headers[i];
                    var range = (Excel.Range)worksheet.Cells[currentRow, i + 1];
                    range.Font.Bold = true;
                    range.Interior.Color = System.Drawing.Color.LightGray.ToArgb();
                }
                currentRow++;

                // Данные
                foreach (var item in data)
                {
                    worksheet.Cells[currentRow, 1] = item.RowNumber;
                    worksheet.Cells[currentRow, 2] = item.BookFullTitle;
                    worksheet.Cells[currentRow, 3] = item.YearPublishing;
                    worksheet.Cells[currentRow, 4] = item.Price;
                    worksheet.Cells[currentRow, 5] = item.InventaryNumber;
                    worksheet.Cells[currentRow, 6] = item.VerificationStatus;
                    worksheet.Cells[currentRow, 7] = item.ReasonMissing;
                    currentRow++;
                }

                // Итоги
                currentRow++;
                worksheet.Cells[currentRow, 1] = "Итого проверено книг:";
                worksheet.Cells[currentRow, 2] = data.Count;
                worksheet.Cells[currentRow, 3] = "В наличии:";
                worksheet.Cells[currentRow, 4] = data.Count(b => b.VerificationStatus == "В наличии");
                worksheet.Cells[currentRow, 5] = "Отсутствует:";
                worksheet.Cells[currentRow, 6] = data.Count(b => b.VerificationStatus == "Отсутствует");

                worksheet.Columns.AutoFit();
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                if (worksheet != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
            }
        }
    }
}