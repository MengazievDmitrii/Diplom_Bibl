using Diplom_Bibl.Controls;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplom_Bibl.Pages
{
    public partial class AddViewBook : Page
    {
        private readonly string connectionString =
            "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";

        private int? _currentViewIDBook = null;
        private System.Collections.ObjectModel.ObservableCollection<object> _allViewBooks;

        public AddViewBook()
        {
            InitializeComponent();
            LoadViewBooks();
        }


        private void LoadViewBooks()
        {
            try
            {
                const string query = @"
                    SELECT ID_View_Book, View_Book 
                    FROM ViewBook 
                    ORDER BY View_Book";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            items.Add(new
                            {
                                ID_View_Book = reader["ID_View_Book"] is int id ? id : 0,
                                View_Book = reader["View_Book"]?.ToString() ?? ""
                            });
                        }

                        _allViewBooks = items;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки видов книг:\n{ex.Message}",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }


        private void ApplyFilter(string searchTerm)
        {
            if (_allViewBooks == null)
            {
                LvStatusBook.ItemsSource = null;
                return;
            }

            var filtered = _allViewBooks
                .Where(item =>
                {
                    var name = item.GetType().GetProperty("View_Book")?.GetValue(item)?.ToString() ?? "";
                    return string.IsNullOrEmpty(searchTerm) ||
                           name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            LvStatusBook.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(filtered);
        }


        private void ClearFields()
        {
            View_Book_Tb.Clear();
            _currentViewIDBook = null;
            SaveNewRecordButton.Visibility = Visibility.Visible;
            SaveChangesButton.Visibility = Visibility.Collapsed;
            DeleteButton.Visibility = Visibility.Collapsed;
        }


        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            string viewName = View_Book_Tb.Text?.Trim();
            if (string.IsNullOrEmpty(viewName))
            {
                CustomMessageBox.Show("Введите название вида книги.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            const string query = "INSERT INTO ViewBook (View_Book) VALUES (@View_Book);";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@View_Book", viewName);
                        int rows = command.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            CustomMessageBox.Show($"Вид «{viewName}» успешно добавлен.",
                               "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks(); // Обновляем список
                        }
                        else
                        {
                            CustomMessageBox.Show("Не удалось добавить запись.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 547)
            {
                CustomMessageBox.Show("Такой вид книги уже существует или нарушено ограничение целостности.",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }


        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentViewIDBook.HasValue)
            {
                CustomMessageBox.Show("Нет выбранной записи для редактирования.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            string viewName = View_Book_Tb.Text?.Trim();
            if (string.IsNullOrEmpty(viewName))
            {
                CustomMessageBox.Show("Введите название вида книги.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            const string query = @"
                UPDATE ViewBook 
                SET View_Book = @View_Book 
                WHERE ID_View_Book = @Id;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@View_Book", viewName);
                        command.Parameters.AddWithValue("@Id", _currentViewIDBook.Value);

                        int rows = command.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            CustomMessageBox.Show("Запись успешно обновлена.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks();
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись не найдена (возможно, удалена).", "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка обновления:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }


        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentViewIDBook.HasValue)
            {
                CustomMessageBox.Show("Нет выбранной записи для удаления.", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show(
                "Удалить выбранный вид книги?",
                "Подтверждение", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            const string query = "DELETE FROM ViewBook WHERE ID_View_Book = @Id;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", _currentViewIDBook.Value);
                        int rows = command.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            CustomMessageBox.Show("Вид книги удалён.", "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks();
                        }
                        else
                        {
                            CustomMessageBox.Show("Запись уже удалена.", "Внимание", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                CustomMessageBox.Show("Невозможно удалить: на этот вид ссылаются другие записи (например, книги).",
                   "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка удаления:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }


        private void LvStatusBook_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = LvStatusBook.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic view = item;
                _currentViewIDBook = view.ID_View_Book;
                View_Book_Tb.Text = view.View_Book;

                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}", "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = SearchBox.Text.Trim();
            ApplyFilter(term);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Clear();
                SearchBox.Focus();
            }
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}