using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для AddReader.xaml
    /// </summary>
    public partial class AddReader : Page
    {
        DiplomBiblEntities db = new DiplomBiblEntities();
        private string connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
        private System.Collections.ObjectModel.ObservableCollection<object> _allSpecialities;

        public AddReader()
        {
            InitializeComponent();
            LoadReaderType();
            LoadGroup();
            LoadSpecialities();
        }

        private void LoadReaderType()
        {
            try
            {
                const string query = @"
                    SELECT TypeID, TypeName, Description 
                    FROM ReaderType 
                    ORDER BY TypeID";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<dynamic>();

                        while (reader.Read())
                        {
                            // 🔹 Безопасное чтение ID — даже если SpecialityID — строка с числом
                            int id = reader["TypeID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["TypeID"]);

                            string name = reader["TypeName"]?.ToString() ?? "";
                            string description = reader["Description"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = id,
                                DisplayTr = $"{name}"
                            });
                        }

                        TRCb.ItemsSource = items;
                        TRCb.DisplayMemberPath = "DisplayTr";
                        TRCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типа читателя:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                TRCb.ItemsSource = null;
            }
        }

        private void LoadGroup()
        {
            try
            {
                const string query = @"
                    SELECT GroupID, GroupCode, SpecialityID 
                    FROM Groups 
                    ORDER BY GroupID";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<dynamic>();

                        while (reader.Read())
                        {
                            // 🔹 Безопасное чтение ID — даже если SpecialityID — строка с числом
                            int id = reader["GroupID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["GroupID"]);

                            string name = reader["GroupCode"]?.ToString() ?? "";

                            items.Add(new
                            {
                                ID = id,
                                DisplayGr = $"{name}"
                            });
                        }

                        GrCb.ItemsSource = items;
                        GrCb.DisplayMemberPath = "DisplayGr";
                        GrCb.SelectedValuePath = "ID";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                GrCb.ItemsSource = null;
            }
        }
        private void LoadSpecialities()
        {
            try
            {
                const string query = @"
            SELECT SpecialityID, SpecialityCode, NameSpeciality 
            FROM Speciality 
            ORDER BY SpecialityCode";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        var items = new System.Collections.ObjectModel.ObservableCollection<object>();

                        while (reader.Read())
                        {
                            int SpecialityID = reader["SpecialityID"]
                            == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["SpecialityID"]);
                            string SpecialityCode = reader["SpecialityCode"]?.ToString() ?? "";
                            string NameSpeciality = reader["NameSpeciality"]?.ToString() ?? "";
                            items.Add(new
                            {
                                ID = SpecialityID,
                                DisplaySp = $"{SpecialityCode} {NameSpeciality}"
                            });

                        }

                        // 🔹 СОХРАНЯЕМ ПОЛНЫЙ СПИСОК — это важно!
                        SpCb.ItemsSource = items;
                        SpCb.DisplayMemberPath = "DisplaySp";
                        SpCb.SelectedValuePath = "ID";

                        // 🔹 Применяем фильтр (сначала — пустой, т.е. показываем все)

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //private void AddReaderBtn_Click(object sender, RoutedEventArgs e)
        //{
            // Проверка ввода (аналогично вашему SaveButton_Click)
            //        string cardnumber = CardNumberTb.Text?.Trim(); // Trim() — на случай пробелов
            //        string lastname = LastNameTb.Text?.Trim();
            //        string firstname = FirstNameTb.Text?.Trim();
            //        string middlename = MiddleNameTb.Text?.Trim();

            //        if (string.IsNullOrEmpty(cardnumber) || string.IsNullOrEmpty(lastname)||string.IsNullOrEmpty(firstname))
            //        {
            //            MessageBox.Show("Пожалуйста, введите корректные значения.",
            //                            "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
            //            return;
            //        }



            //        string query = @"
            //            INSERT INTO ReaderForm (CardNumber, LastName, FirstName, MiddleName, TypeID, GroupID, Specialty, PhoneNumber, HomeAddress, MembershipStartDate, FormFillDate )
            //            VALUES (@CardNumberV, @LastNameV, @FirstNameV, @MiddleNameV,@TypeIDV, @GroupIDV, @SpecialityV, @PhoneNumberV, @HomeAddressV,@MembershipStartDateV, @FormFillDate)";

            //        try
            //        {
            //            using (SqlConnection connection = new SqlConnection(connectionString))
            //            {
            //                connection.Open();

            //                using (SqlCommand command = new SqlCommand(query, connection))
            //                {
            //                    // Добавляем параметры, чтобы избежать SQL-инъекций
            //                    command.Parameters.AddWithValue("@SpecialityCodeV", codespeciality); // date уже в формате "ГГГГ-ММ-ДД"
            //                    command.Parameters.AddWithValue("@NameSpecialityV", namespeciality);


            //                    int rowsAffected = command.ExecuteNonQuery();

            //                    if (rowsAffected > 0)
            //                    {
            //                        MessageBox.Show($"Специальность {codespeciality + " " + namespeciality} успешно сохранены в базу данных:\n",
            //                                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            //                        // После сохранения новой записи, можно перезагрузить её, чтобы ID появился
            //                        // или просто сбросить currentRecordId, если не планируете сразу редактировать
            //                        _currentSpecialityId = null; // Сбрасываем, так как это новая запись, ID которой мы не знаем
            //                        // Или вызвать LoadFirstRecordForDate(DateTime.Parse(date)) для получения ID, если это важно
            //                        LoadSpecialities();
            //                    }
            //                    else
            //                    {
            //                        MessageBox.Show("Данные не были сохранены. Проверьте введённые значения.",
            //                                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            //                    }
            //                }
            //            }
            //        }
            //        catch (SqlException sqlEx)
            //        {
            //            // Обработка ошибок SQL Server (например, нарушение ограничений, проблемы с подключением)
            //            MessageBox.Show($"Ошибка базы данных: {sqlEx.Message}",
            //                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            //        }
            //        catch (Exception ex)
            //        {
            //            // Обработка любых других ошибок
            //            MessageBox.Show($"Произошла ошибка: {ex.Message}",
            //                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            //        }
            //    }
        }

        //private void GrCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{

        //    {
        //        try
        //        {
        //            const string query = @"
        //            SELECT GroupID, GroupCode, SpecialityID 
        //            FROM Groups 
        //            ORDER BY GroupID
        //            WHERE SpecialityID=Speciality.SpecialityID";

        //            using (var connection = new SqlConnection(connectionString))
        //            {
        //                connection.Open();
        //                using (var command = new SqlCommand(query, connection))
        //                using (var reader = command.ExecuteReader())
        //                {
        //                    var items = new System.Collections.ObjectModel.ObservableCollection<dynamic>();

        //                    while (reader.Read())
        //                    {
        //                        // 🔹 Безопасное чтение ID — даже если SpecialityID — строка с числом
        //                        int id = reader["GroupID"] == DBNull.Value
        //                            ? 0
        //                            : Convert.ToInt32(reader["GroupID"]);

        //                        string name = reader["GroupCode"]?.ToString() ?? "";

        //                        items.Add(new
        //                        {
        //                            ID = id,
        //                            DisplayGr = $"{name}"
        //                        });
        //                    }

        //                    GrCb.ItemsSource = items;
        //                    GrCb.DisplayMemberPath = "DisplayGr";
        //                    GrCb.SelectedValuePath = "ID";
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}",
        //                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //            GrCb.ItemsSource = null;
        //        }
        //    }

        //}


    }
