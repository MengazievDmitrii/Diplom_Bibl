using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diplom_Bibl.Helpers;

namespace Diplom_Bibl.Pages
{
    public partial class AddFundingSource : Page
    {
        private readonly string _connectionString = DatabaseHelper.ConnectionString;
        private ObservableCollection<FundingSourceDisplayItem> _allFundingSources;
        private int? _currentFundingSourceId = null;
        private bool _isPlaceholderActive = true;

        public class FundingSourceDisplayItem
        {
            public int ID_FundingSource { get; set; }
            public string FundingSourceCode { get; set; } = "";
            public string FundingSourceName { get; set; } = "";
            public string AccountingAccount { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsActive { get; set; }
        }

        public AddFundingSource()
        {
            InitializeComponent();
            LoadFundingSources();
            SetSearchPlaceholder();
        }

        private void SetSearchPlaceholder()
        {
            SearchBox.Text = "Поиск по коду или названию...";
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

        private void LoadFundingSources()
        {
            try
            {
                const string query = @"
                    SELECT ID_FundingSource, FundingSourceCode, FundingSourceName, 
                           ISNULL(AccountingAccount, '') AS AccountingAccount,
                           ISNULL(Description, '') AS Description, IsActive
                    FROM FundingSources
                    ORDER BY FundingSourceCode";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new ObservableCollection<FundingSourceDisplayItem>();

                        while (reader.Read())
                        {
                            items.Add(new FundingSourceDisplayItem
                            {
                                ID_FundingSource = Convert.ToInt32(reader["ID_FundingSource"]),
                                FundingSourceCode = reader["FundingSourceCode"].ToString(),
                                FundingSourceName = reader["FundingSourceName"].ToString(),
                                AccountingAccount = reader["AccountingAccount"].ToString(),
                                Description = reader["Description"].ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }

                        _allFundingSources = items;
                        ApplyFilter("");
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void ApplyFilter(string searchTerm)
        {
            if (_allFundingSources == null) return;

            var filtered = _allFundingSources.Where(s =>
                string.IsNullOrEmpty(searchTerm) ||
                s.FundingSourceCode.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.FundingSourceName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            LvFundingSources.ItemsSource = filtered;
        }

        private void ClearForm()
        {
            TbFundingSourceCode.Clear();
            TbFundingSourceName.Clear();
            TbAccountingAccount.Clear();
            TbDescription.Clear();
            ChkIsActive.IsChecked = true;
            _currentFundingSourceId = null;

            BtnSaveNew.Visibility = Visibility.Visible;
            BtnSaveChanges.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(TbFundingSourceCode.Text))
            {
                CustomMessageBox.Show("Введите код статьи финансирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbFundingSourceCode.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TbFundingSourceName.Text))
            {
                CustomMessageBox.Show("Введите название статьи финансирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                TbFundingSourceName.Focus();
                return false;
            }

            return true;
        }

        private bool IsCodeUnique(string code, int? excludeId = null)
        {
            string query = "SELECT COUNT(*) FROM FundingSources WHERE FundingSourceCode = @Code";
            if (excludeId.HasValue) query += " AND ID_FundingSource != @ExcludeId";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code.Trim());
                    if (excludeId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            string code = TbFundingSourceCode.Text.Trim().ToUpper();

            if (!IsCodeUnique(code))
            {
                CustomMessageBox.Show("Статья финансирования с таким кодом уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    INSERT INTO FundingSources (FundingSourceCode, FundingSourceName, AccountingAccount, Description, IsActive)
                    VALUES (@Code, @Name, @Account, @Description, @IsActive)";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Code", code);
                        cmd.Parameters.AddWithValue("@Name", TbFundingSourceName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Account", string.IsNullOrWhiteSpace(TbAccountingAccount.Text) ? (object)DBNull.Value : TbAccountingAccount.Text.Trim());
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@IsActive", ChkIsActive.IsChecked == true);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Статья финансирования добавлена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFundingSources();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFundingSourceId.HasValue)
            {
                CustomMessageBox.Show("Выберите запись для редактирования.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm()) return;

            string code = TbFundingSourceCode.Text.Trim().ToUpper();

            if (!IsCodeUnique(code, _currentFundingSourceId))
            {
                CustomMessageBox.Show("Статья финансирования с таким кодом уже существует.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            try
            {
                const string query = @"
                    UPDATE FundingSources 
                    SET FundingSourceCode = @Code, FundingSourceName = @Name, 
                        AccountingAccount = @Account, Description = @Description, IsActive = @IsActive
                    WHERE ID_FundingSource = @Id";

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Code", code);
                        cmd.Parameters.AddWithValue("@Name", TbFundingSourceName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Account", string.IsNullOrWhiteSpace(TbAccountingAccount.Text) ? (object)DBNull.Value : TbAccountingAccount.Text.Trim());
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(TbDescription.Text) ? (object)DBNull.Value : TbDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@IsActive", ChkIsActive.IsChecked == true);
                        cmd.Parameters.AddWithValue("@Id", _currentFundingSourceId.Value);

                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Данные обновлены.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFundingSources();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFundingSourceId.HasValue)
            {
                CustomMessageBox.Show("Выберите запись для удаления.", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            bool isUsed = false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ReceiptBooks WHERE FundingSourceID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", _currentFundingSourceId.Value);
                    int count = (int)cmd.ExecuteScalar();
                    isUsed = count > 0;
                }
            }

            if (isUsed)
            {
                CustomMessageBox.Show("Невозможно удалить: статья финансирования используется в приходных накладных.\nРекомендуется деактивировать запись.",
                    "Удаление запрещено", CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
                return;
            }

            var result = CustomMessageBox.Show("Вы уверены, что хотите удалить эту статью финансирования?",
                "Подтверждение удаления", CustomMessageBoxButton.YesNo, CustomMessageBoxImage.Question);

            if (result != CustomMessageBoxResult.Yes) return;

            try
            {
                const string query = "DELETE FROM FundingSources WHERE ID_FundingSource = @Id";
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentFundingSourceId.Value);
                        cmd.ExecuteNonQuery();
                        CustomMessageBox.Show("Запись удалена.", "Успешно",
                            CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);
                        ClearForm();
                        LoadFundingSources();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                    CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
            }
        }

        private void LvFundingSources_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvFundingSources.SelectedItem is FundingSourceDisplayItem selected)
            {
                _currentFundingSourceId = selected.ID_FundingSource;
                TbFundingSourceCode.Text = selected.FundingSourceCode;
                TbFundingSourceName.Text = selected.FundingSourceName;
                TbAccountingAccount.Text = selected.AccountingAccount;
                TbDescription.Text = selected.Description;
                ChkIsActive.IsChecked = selected.IsActive;

                BtnSaveNew.Visibility = Visibility.Collapsed;
                BtnSaveChanges.Visibility = Visibility.Visible;
                BtnDelete.Visibility = Visibility.Visible;
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
            ApplyFilter(SearchBox.Text.Trim());
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFundingSources();
        }

        private void ClosePage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}