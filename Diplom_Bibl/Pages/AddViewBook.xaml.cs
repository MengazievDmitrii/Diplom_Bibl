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

        private int? _currentViewBookId = null;
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
                MessageBox.Show($"Ошибка загрузки видов книг:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _currentViewBookId = null;
            SaveNewRecordButton.Visibility = Visibility.Visible;
            SaveChangesButton.Visibility = Visibility.Collapsed;
            DeleteButton.Visibility = Visibility.Collapsed;
        }


        private void SaveNewRecordButton_Click(object sender, RoutedEventArgs e)
        {
            string viewName = View_Book_Tb.Text?.Trim();
            if (string.IsNullOrEmpty(viewName))
            {
                MessageBox.Show("Введите название вида книги.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            MessageBox.Show($"Вид «{viewName}» успешно добавлен.",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks(); // Обновляем список
                        }
                        else
                        {
                            MessageBox.Show("Не удалось добавить запись.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 547)
            {
                MessageBox.Show("Такой вид книги уже существует или нарушено ограничение целостности.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentViewBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной записи для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string viewName = View_Book_Tb.Text?.Trim();
            if (string.IsNullOrEmpty(viewName))
            {
                MessageBox.Show("Введите название вида книги.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        command.Parameters.AddWithValue("@Id", _currentViewBookId.Value);

                        int rows = command.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            MessageBox.Show("Запись успешно обновлена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks();
                        }
                        else
                        {
                            MessageBox.Show("Запись не найдена (возможно, удалена).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentViewBookId.HasValue)
            {
                MessageBox.Show("Нет выбранной записи для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Удалить выбранный вид книги?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            const string query = "DELETE FROM ViewBook WHERE ID_View_Book = @Id;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", _currentViewBookId.Value);
                        int rows = command.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            MessageBox.Show("Вид книги удалён.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearFields();
                            LoadViewBooks();
                        }
                        else
                        {
                            MessageBox.Show("Запись уже удалена.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ClearFields();
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show("Невозможно удалить: на этот вид ссылаются другие записи (например, книги).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    
        private void LvStatusBook_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = LvStatusBook.SelectedItem;
            if (item == null) return;

            try
            {
                dynamic view = item;
                _currentViewBookId = view.ID_View_Book;
                View_Book_Tb.Text = view.View_Book;

                SaveNewRecordButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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