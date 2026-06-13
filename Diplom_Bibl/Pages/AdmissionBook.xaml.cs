using Diplom_Bibl.Controls;
using Diplom_Bibl.Helpers;
using Diplom_Bibl.Services;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplom_Bibl.Pages
{
    public partial class AdmissionBook : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<ReceiptItem> _allReceipts;
        private ReceiptItem _selectedReceipt;

        // Класс для отображения данных накладной
        public class ReceiptItem
        {
            public int IDReceipt { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public DateTime ReceiptDate { get; set; }
            public string ResponsiblePerson { get; set; } = "";
            public string SupplierName { get; set; } = "";
            public string Comment { get; set; } = "";
            public int BooksCount { get; set; }
        }

        public AdmissionBook()
        {
            InitializeComponent();

        }

        /// <summary>
        /// Загрузка страницы
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReceipts();
        }

        /// <summary>
        /// Загрузка списка накладных
        /// </summary>
        private void LoadReceipts()
        {
            try
            {
                string query = @"
                    SELECT 
                        r.IDReceipt, 
                        r.ReceiptNumber, 
                        r.ReceiptDate, 
                        u.Surname + ' ' + u.Name + ISNULL(' ' + u.Patronymic, '') AS ResponsiblePerson,
                        ISNULL(s.SupplierName, '') AS SupplierName,
                        ISNULL(r.Comment, '') AS Comment,
                        (SELECT COUNT(*) FROM Books WHERE IDReceip = r.IDReceipt) AS BooksCount
                    FROM ReceiptBooks r
                    LEFT JOIN Users u ON r.ID_ResponsibleUser = u.ID_User
                    LEFT JOIN Suppliers s ON r.SupplierID = s.ID_Supplier
                    ORDER BY r.ReceiptDate DESC";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var list = new ObservableCollection<ReceiptItem>();
                        while (reader.Read())
                        {
                            list.Add(new ReceiptItem
                            {
                                IDReceipt = Convert.ToInt32(reader["IDReceipt"]),
                                ReceiptNumber = reader["ReceiptNumber"].ToString(),
                                ReceiptDate = Convert.ToDateTime(reader["ReceiptDate"]),
                                ResponsiblePerson = reader["ResponsiblePerson"].ToString(),
                                SupplierName = reader["SupplierName"].ToString(),
                                Comment = reader["Comment"].ToString(),
                                BooksCount = Convert.ToInt32(reader["BooksCount"])
                            });
                        }
                        _allReceipts = list;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                _allReceipts = new ObservableCollection<ReceiptItem>();
                LvReceipts.ItemsSource = _allReceipts;
            }
        }

        /// <summary>
        /// Фильтрация списка
        /// </summary>
        private void ApplyFilter(string term)
        {
            if (_allReceipts == null) return;

            var filtered = _allReceipts.Where(r =>
                string.IsNullOrEmpty(term) ||
                r.ReceiptNumber.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.ResponsiblePerson.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.SupplierName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.Comment.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            LvReceipts.ItemsSource = filtered;

            // Обновляем информационную панель
            var selectedInfo = FindName("SelectedInfo") as TextBlock;
            if (selectedInfo != null)
            {
                selectedInfo.Text = $"Найдено записей: {filtered.Count} | Всего в системе: {_allReceipts.Count}";
            }
        }

        /// <summary>
        /// Обработчик изменения выделения в списке
        /// </summary>
        private void LvReceipts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedReceipt = LvReceipts.SelectedItem as ReceiptItem;

            var selectedInfo = FindName("SelectedInfo") as TextBlock;
            if (selectedInfo != null)
            {
                if (_selectedReceipt != null)
                {
                    selectedInfo.Text = $"Выбрана накладная: {_selectedReceipt.ReceiptNumber} | " +
                                       $"Дата: {_selectedReceipt.ReceiptDate:dd.MM.yyyy HH:mm} | " +
                                       $"Книг: {_selectedReceipt.BooksCount}";
                }
                else
                {
                    selectedInfo.Text = $"Найдено записей: {(LvReceipts.ItemsSource as System.Collections.IList)?.Count ?? 0} | " +
                                       $"Всего в системе: {_allReceipts?.Count ?? 0}";
                }
            }
        }

        /// <summary>
        /// Поиск
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchBox.Text.Trim());
        }

        /// <summary>
        /// Обновление списка
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReceipts();
            SearchBox.Clear();
        }

        /// <summary>
        /// Печать акта поступления
        /// </summary>
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedReceipt == null)
            {
                CustomMessageBox.Show("Выберите накладную для формирования акта.",
                    "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (_selectedReceipt.BooksCount == 0)
            {
                var result = CustomMessageBox.Show(
                    $"Накладная \"{_selectedReceipt.ReceiptNumber}\" не содержит книг.\n\n" +
                    "Продолжить формирование пустого акта?",
                    "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

                if (result != CustomMessageBoxResult.Yes) return;
            }

            try
            {
                // Генерируем PDF
                byte[] pdfBytes = ReceiptActPdfService.GenerateReceiptActPdf(_selectedReceipt.IDReceipt);

                // Сохраняем на рабочий стол
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"Акт_поступления_{_selectedReceipt.ReceiptNumber}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                string fullPath = System.IO.Path.Combine(desktop, fileName);
                System.IO.File.WriteAllBytes(fullPath, pdfBytes);

                CustomMessageBox.Show(
                    $"Акт поступления успешно сформирован!\n\n" +
                    $"Накладная: {_selectedReceipt.ReceiptNumber}\n" +
                    $"Книг: {_selectedReceipt.BooksCount}\n" +
                    $"Файл сохранён:\n{fullPath}",
                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка формирования акта: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }


    }
}