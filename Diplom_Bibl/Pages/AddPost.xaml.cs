using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Controls;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddPost.xaml
    /// </summary>
    public partial class AddPost : Page
    {
        private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
        private ObservableCollection<PostItem> _allPosts;
        private int? _currentPostId = null;
        private bool _isPlaceholderActive = true;

        // Класс для представления должности
        public class PostItem
        {
            public int ID { get; set; }
            public string DisplayPost { get; set; }
        }

        public AddPost()
        {
            InitializeComponent();
            SetSearchPlaceholder();
        }

        private void SetSearchPlaceholder()
        {
            SearchBox.Text = "Поиск по названию...";
            SearchBox.Foreground = Brushes.Gray;
            _isPlaceholderActive = true;
        }

        private void ClearPlaceholder()
        {
            if (_isPlaceholderActive)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
                _isPlaceholderActive = false;
            }
        }

        private void LoadPosts()
        {
            try
            {
                const string query = @"
                    SELECT ID_Post, Post_Name 
                    FROM Post 
                    ORDER BY Post_Name";

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new ObservableCollection<PostItem>();

                        while (reader.Read())
                        {
                            int id = 0;
                            if (reader["ID_Post"] != DBNull.Value)
                            {
                                id = Convert.ToInt32(reader["ID_Post"]);
                            }

                            string name = reader["Post_Name"]?.ToString() ?? "";

                            if (id > 0 && !string.IsNullOrEmpty(name))
                            {
                                items.Add(new PostItem
                                {
                                    ID = id,
                                    DisplayPost = name
                                });
                            }
                        }

                        _allPosts = items;
                        PostsListView.ItemsSource = items;
                        CountLabel.Text = $"Всего записей: {items.Count}";
                    }
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка загрузки должностей: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            PostNameTb.Clear();
            _currentPostId = null;
            IdLabel.Text = "";

            SaveNewButton.Visibility = Visibility.Visible;
            SaveChangesButton.Visibility = Visibility.Collapsed;
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(PostNameTb.Text))
            {
                 CustomMessageBox.Show("Введите название должности.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                PostNameTb.Focus();
                return false;
            }

            return true;
        }

        private bool IsPostNameUnique(string postName, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM Post WHERE Post_Name = @PostName";

            if (excludeId.HasValue)
            {
                query += " AND ID_Post != @ExcludeId";
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@PostName", postName.Trim());

                    if (excludeId.HasValue)
                    {
                        command.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                    }

                    int count = (int)command.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        private void SaveNewButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            string postName = PostNameTb.Text.Trim();

            // Проверка на уникальность
            if (!IsPostNameUnique(postName))
            {
                 CustomMessageBox.Show("Должность с таким названием уже существует.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(
                        "INSERT INTO Post (Post_Name) VALUES (@PostName)",
                        connection))
                    {
                        command.Parameters.AddWithValue("@PostName", postName);

                        int result = command.ExecuteNonQuery();

                        if (result > 0)
                        {
                             CustomMessageBox.Show($"Должность '{postName}' успешно добавлена.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearForm();
                            LoadPosts();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) // Нарушение уникальности
                {
                     CustomMessageBox.Show("Должность с таким названием уже существует.",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                else
                {
                     CustomMessageBox.Show($"Ошибка базы данных: {ex.Message}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при добавлении: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPostId.HasValue)
            {
                 CustomMessageBox.Show("Выберите должность для редактирования.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm())
                return;

            string postName = PostNameTb.Text.Trim();

            // Проверка на уникальность (исключая текущую запись)
            if (!IsPostNameUnique(postName, _currentPostId))
            {
                 CustomMessageBox.Show("Должность с таким названием уже существует.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(
                        "UPDATE Post SET Post_Name = @PostName WHERE ID_Post = @Id",
                        connection))
                    {
                        command.Parameters.AddWithValue("@PostName", postName);
                        command.Parameters.AddWithValue("@Id", _currentPostId.Value);

                        int result = command.ExecuteNonQuery();

                        if (result > 0)
                        {
                             CustomMessageBox.Show("Должность успешно обновлена.",
                                "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                            ClearForm();
                            LoadPosts();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627)
                {
                     CustomMessageBox.Show("Должность с таким названием уже существует.",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
                else
                {
                     CustomMessageBox.Show($"Ошибка базы данных: {ex.Message}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                 CustomMessageBox.Show($"Ошибка при обновлении: {ex.Message}",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentPostId.HasValue)
            {
                 CustomMessageBox.Show("Выберите должность для удаления.",
                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result =  CustomMessageBox.Show(
                $"Вы уверены, что хотите удалить должность '{PostNameTb.Text}'?",
                "Подтверждение удаления",
                CustomMessageBoxButton.YesNo,
                CustomMessageBoxImage.Question);

            if (result == CustomMessageBoxResult.Yes)
            {
                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();

                        // Проверяем, используется ли должность в таблице ReaderForm
                        using (var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM ReaderForm WHERE Post = @Id",
                            connection))
                        {
                            checkCmd.Parameters.AddWithValue("@Id", _currentPostId.Value);
                            int usedCount = (int)checkCmd.ExecuteScalar();

                            if (usedCount > 0)
                            {
                                 CustomMessageBox.Show(
                                    $"Невозможно удалить должность, так как она используется в {usedCount} читательских формах.",
                                    "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                                return;
                            }
                        }

                        // Удаляем должность
                        using (var command = new SqlCommand(
                            "DELETE FROM Post WHERE ID_Post = @Id",
                            connection))
                        {
                            command.Parameters.AddWithValue("@Id", _currentPostId.Value);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                 CustomMessageBox.Show("Должность успешно удалена.",
                                    "Успешно", CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

                                ClearForm();
                                LoadPosts();
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 547) // Нарушение внешнего ключа
                    {
                         CustomMessageBox.Show("Невозможно удалить должность, так как она используется в других записях.",
                            "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                    else
                    {
                         CustomMessageBox.Show($"Ошибка базы данных: {ex.Message}",
                            "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                     CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}",
                        "Ошибка", CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
                }
            }
        }

        private void PostsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PostsListView.SelectedItem is PostItem selectedPost)
            {
                _currentPostId = selectedPost.ID;
                PostNameTb.Text = selectedPost.DisplayPost;
                IdLabel.Text = $"ID: {selectedPost.ID}";

                SaveNewButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.Visibility = Visibility.Visible;
                DeleteButton.Visibility = Visibility.Visible;
            }
        }

        private void PostsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PostsListView.SelectedItem != null)
            {
                // Можно добавить дополнительную логику при двойном клике
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearPlaceholder();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SetSearchPlaceholder();
                ApplyFilter("");
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPlaceholderActive) return;

            string searchText = SearchBox.Text.Trim().ToLower();
            ApplyFilter(searchText);
        }

        private void ApplyFilter(string searchTerm)
        {
            if (_allPosts == null) return;

            if (string.IsNullOrEmpty(searchTerm))
            {
                PostsListView.ItemsSource = _allPosts;
            }
            else
            {
                var filtered = _allPosts
                    .Where(p => p.DisplayPost.ToLower().Contains(searchTerm))
                    .ToList();

                PostsListView.ItemsSource = filtered;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPosts();
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}