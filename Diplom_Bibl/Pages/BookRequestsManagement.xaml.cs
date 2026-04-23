using Diplom_Bibl.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom_Bibl.Pages
{
	public partial class BookRequestsManagement : Page
	{
		private readonly string _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;";
		private ObservableCollection<RequestItem> _allRequests;
		private int _librarianId;
		private System.Timers.Timer _notificationTimer;

		// Класс для отображения заявки
		public class RequestItem
		{
			public int ID_Request { get; set; }
			public string RequestNumber { get; set; } = "";
			public DateTime RequestDate { get; set; }
			public int UserID { get; set; }
			public string UserName { get; set; } = "";
			public int? GroupID { get; set; }
			public string GroupCode { get; set; } = "";
			public string LessonName { get; set; } = "";
			public DateTime LessonDate { get; set; }
			public int StatusID { get; set; }
			public string StatusName { get; set; } = "";
			public int TotalBooks { get; set; }
			public DateTime? ExpirationDate { get; set; }
			public int UserTypeID { get; set; }

			public SolidColorBrush StatusColor
			{
				get
				{
					switch (StatusName)
					{
						case "Новая": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
						case "В обработке": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
						case "Готова": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
						case "Выдана": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C"));
						case "Отменена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
						case "Просрочена": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
						default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
					}
				}
			}

			public Visibility ConfirmVisible => (StatusName == "Новая" || StatusName == "В обработке") ? Visibility.Visible : Visibility.Collapsed;
			public Visibility IssueVisible => (StatusName == "Готова") ? Visibility.Visible : Visibility.Collapsed;
			public Visibility CancelVisible => (StatusName != "Выдана" && StatusName != "Отменена" && StatusName != "Просрочена") ? Visibility.Visible : Visibility.Collapsed;
		}

		// Класс для элемента заявки (книги)
		public class RequestBookItem
		{
			public int ID_RequestItem { get; set; }
			public int BookID { get; set; }
			public string BookTitle { get; set; } = "";
			public string AuthorName { get; set; } = "";
			public int Quantity { get; set; }
			public int StatusID { get; set; }
			public string StatusName { get; set; } = "";
		}

		public BookRequestsManagement(int librarianId)
		{
			InitializeComponent();
			_librarianId = librarianId;

			// Запускаем таймер для проверки новых заявок каждые 10 секунд
			_notificationTimer = new System.Timers.Timer(10000);
			_notificationTimer.Elapsed += async (sender, e) => await CheckNewRequests();
			_notificationTimer.Start();
		}

		private async void Page_Loaded(object sender, RoutedEventArgs e)
		{
			await LoadDataAsync();
		}

		private async System.Threading.Tasks.Task LoadDataAsync()
		{
			try
			{
				await LoadRequests();
			}
			catch (Exception ex)
			{
				CustomMessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
					CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
			}
		}

		private async System.Threading.Tasks.Task CheckNewRequests()
		{
			try
			{
				int newRequestsCount = 0;

				using (var conn = new SqlConnection(_connectionString))
				{
					await conn.OpenAsync();
					string query = "SELECT COUNT(*) FROM BookRequests WHERE StatusID = 1 AND RequestDate > DATEADD(minute, -10, GETDATE())";
					using (var cmd = new SqlCommand(query, conn))
					{
						newRequestsCount = (int)await cmd.ExecuteScalarAsync();
					}
				}

				if (newRequestsCount > 0)
				{
					Dispatcher.Invoke(() =>
					{
						// Воспроизводим звук уведомления
						SystemSounds.Beep.Play();

						// Показываем диалоговое окно
						CustomMessageBox.Show($"Поступило {newRequestsCount} новых заявок!", "Новые заявки",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

						// Обновляем список заявок
						_ = LoadRequests();
					});
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Ошибка проверки новых заявок: {ex.Message}");
			}
		}

		private async System.Threading.Tasks.Task LoadRequests()
		{
			await System.Threading.Tasks.Task.Run(() =>
			{
				string query = @"
                    SELECT 
                        r.ID_Request,
                        r.RequestNumber,
                        r.RequestDate,
                        r.UserID,
                        CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                        r.GroupID,
                        g.GroupCode,
                        r.LessonName,
                        r.LessonDate,
                        r.StatusID,
                        rs.StatusName,
                        r.ExpirationDate,
                        r.UserTypeID,
                        (SELECT SUM(Quantity) FROM RequestItems WHERE RequestID = r.ID_Request) AS TotalBooks
                    FROM BookRequests r
                    LEFT JOIN ReaderForm rd ON r.UserID = rd.ReaderID
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
                    ORDER BY r.RequestDate DESC";

				using (var conn = new SqlConnection(_connectionString))
				{
					conn.Open();
					using (var cmd = new SqlCommand(query, conn))
					using (var reader = cmd.ExecuteReader())
					{
						var items = new ObservableCollection<RequestItem>();
						while (reader.Read())
						{
							items.Add(new RequestItem
							{
								ID_Request = Convert.ToInt32(reader["ID_Request"]),
								RequestNumber = reader["RequestNumber"].ToString(),
								RequestDate = Convert.ToDateTime(reader["RequestDate"]),
								UserID = Convert.ToInt32(reader["UserID"]),
								UserName = reader["UserName"].ToString(),
								GroupID = reader["GroupID"] != DBNull.Value ? Convert.ToInt32(reader["GroupID"]) : (int?)null,
								GroupCode = reader["GroupCode"]?.ToString() ?? "",
								LessonName = reader["LessonName"]?.ToString() ?? "",
								LessonDate = Convert.ToDateTime(reader["LessonDate"]),
								StatusID = Convert.ToInt32(reader["StatusID"]),
								StatusName = reader["StatusName"].ToString(),
								ExpirationDate = reader["ExpirationDate"] != DBNull.Value ? Convert.ToDateTime(reader["ExpirationDate"]) : (DateTime?)null,
								TotalBooks = Convert.ToInt32(reader["TotalBooks"]),
								UserTypeID = Convert.ToInt32(reader["UserTypeID"])
							});
						}
						Dispatcher.Invoke(() =>
						{
							_allRequests = items;
							ApplyFilters();
						});
					}
				}
			});
		}

		private void ApplyFilters()
		{
			if (_allRequests == null) return;

			string searchTerm = TbSearch.Text?.Trim().ToLower() ?? "";
			string statusFilter = (CbStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все заявки";

			var filtered = _allRequests.AsEnumerable();

			if (!string.IsNullOrEmpty(searchTerm))
			{
				filtered = filtered.Where(r =>
					r.RequestNumber.ToLower().Contains(searchTerm) ||
					r.UserName.ToLower().Contains(searchTerm) ||
					r.GroupCode.ToLower().Contains(searchTerm) ||
					r.LessonName.ToLower().Contains(searchTerm));
			}

			if (statusFilter != "Все заявки")
			{
				filtered = filtered.Where(r => r.StatusName == statusFilter);
			}

			DgRequests.ItemsSource = filtered.ToList();
		}

		private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
		{
			ApplyFilters();
		}

		private void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ApplyFilters();
		}

		private async void ViewRequest_Click(object sender, RoutedEventArgs e)
		{
			Button btn = sender as Button;
			if (btn != null && btn.Tag != null)
			{
				int requestId = (int)btn.Tag;
				await ShowRequestDetails(requestId);
			}
		}

		private void CloseDetailsPanel_Click(object sender, RoutedEventArgs e)
		{
			DetailsPanel.Visibility = Visibility.Collapsed;
			DgRequestItems.ItemsSource = null;
		}

		private async System.Threading.Tasks.Task ShowRequestDetails(int requestId)
		{
			try
			{
				string query = @"
                    SELECT 
                        r.RequestNumber,
                        CONCAT(rd.LastName, ' ', rd.FirstName, ' ', ISNULL(rd.MiddleName, '')) AS UserName,
                        rd.TypeID AS UserTypeID,
                        r.LessonName,
                        r.LessonDate,
                        r.RequestDate,
                        g.GroupCode,
                        rs.StatusName,
                        r.ExpirationDate,
                        ri.ID_RequestItem,
                        ri.BookID,
                        b.TitleBook,
                        ISNULL(a.SurnameAuthor + ' ' + a.NameAuthor + ' ' + a.PatronymicAuthor, '') AS AuthorName,
                        ri.Quantity,
                        ris.StatusName AS ItemStatusName
                    FROM BookRequests r
                    LEFT JOIN ReaderForm rd ON r.UserID = rd.ReaderID
                    LEFT JOIN Groups g ON r.GroupID = g.GroupID
                    LEFT JOIN RequestStatuses rs ON r.StatusID = rs.ID_Status
                    LEFT JOIN RequestItems ri ON r.ID_Request = ri.RequestID
                    LEFT JOIN Books b ON ri.BookID = b.IDBook
                    LEFT JOIN Authors a ON b.Author = a.ID_Autor
                    LEFT JOIN RequestItemStatuses ris ON ri.StatusID = ris.ID_Status
                    WHERE r.ID_Request = @RequestId";

				using (var conn = new SqlConnection(_connectionString))
				{
					await conn.OpenAsync();
					using (var cmd = new SqlCommand(query, conn))
					{
						cmd.Parameters.AddWithValue("@RequestId", requestId);
						using (var reader = await cmd.ExecuteReaderAsync())
						{
							bool hasData = false;
							int userTypeID = 0;
							var items = new ObservableCollection<RequestBookItem>();

							while (await reader.ReadAsync())
							{
								if (!hasData)
								{
									userTypeID = reader["UserTypeID"] != DBNull.Value ? Convert.ToInt32(reader["UserTypeID"]) : 0;
									string userTypeName = userTypeID == 1 ? "Студент" : (userTypeID == 3 ? "Сотрудник" : "Неизвестно");

									Dispatcher.Invoke(() =>
									{
										TbRequestNumber.Text = $"Заявка №: {reader["RequestNumber"]}";
										TbUserName.Text = $"Заявитель: {reader["UserName"]}";
										TbUserType.Text = $"Тип: {userTypeName}";
										TbLessonInfo.Text = $"Занятие: {reader["LessonName"]}";
										TbRequestDate.Text = $"Дата создания: {Convert.ToDateTime(reader["RequestDate"]):dd.MM.yyyy HH:mm}";
										TbGroupInfo.Text = $"Группа: {reader["GroupCode"]?.ToString() ?? "Не указана"}";
										TbStatus.Text = $"Статус: {reader["StatusName"]}";
										if (reader["ExpirationDate"] != DBNull.Value)
											TbExpirationDate.Text = $"Срок брони: {Convert.ToDateTime(reader["ExpirationDate"]):dd.MM.yyyy}";
										else
											TbExpirationDate.Text = "";
									});
									hasData = true;
								}

								if (reader["ID_RequestItem"] != DBNull.Value)
								{
									items.Add(new RequestBookItem
									{
										ID_RequestItem = Convert.ToInt32(reader["ID_RequestItem"]),
										BookID = Convert.ToInt32(reader["BookID"]),
										BookTitle = reader["TitleBook"]?.ToString() ?? "Не указано",
										AuthorName = reader["AuthorName"]?.ToString() ?? "Не указан",
										Quantity = Convert.ToInt32(reader["Quantity"]),
										StatusName = reader["ItemStatusName"]?.ToString() ?? "Неизвестен"
									});
								}
							}

							Dispatcher.Invoke(() =>
							{
								DgRequestItems.ItemsSource = items;
								DetailsPanel.Visibility = Visibility.Visible;

								// Если есть книги, показываем сообщение
								if (items.Count == 0)
								{
									var emptyList = new ObservableCollection<RequestBookItem>();
									emptyList.Add(new RequestBookItem
									{
										BookTitle = "Нет книг в заявке",
										AuthorName = "",
										Quantity = 0,
										StatusName = ""
									});
									DgRequestItems.ItemsSource = emptyList;
								}
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				CustomMessageBox.Show($"Ошибка загрузки деталей: {ex.Message}", "Ошибка",
					CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
			}
		}

		private async void ConfirmRequest_Click(object sender, RoutedEventArgs e)
		{
			Button btn = sender as Button;
			if (btn != null && btn.Tag != null)
			{
				int requestId = (int)btn.Tag;

				var result = CustomMessageBox.Show(
					"Подтвердить заявку? Книги будут зарезервированы.",
					"Подтверждение",
					CustomMessageBoxButton.YesNo,
					CustomMessageBoxImage.Question);

				if (result == CustomMessageBoxResult.Yes)
				{
					try
					{
						using (var conn = new SqlConnection(_connectionString))
						{
							await conn.OpenAsync();
							using (var cmd = new SqlCommand("sp_ReserveBooks", conn))
							{
								cmd.CommandType = System.Data.CommandType.StoredProcedure;
								cmd.Parameters.AddWithValue("@RequestID", requestId);
								cmd.Parameters.AddWithValue("@ReserveDays", 3);
								await cmd.ExecuteNonQueryAsync();
							}
						}

						CustomMessageBox.Show("Заявка подтверждена! Книги зарезервированы.", "Успешно",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

						await LoadDataAsync();
						DetailsPanel.Visibility = Visibility.Collapsed;
					}
					catch (Exception ex)
					{
						CustomMessageBox.Show($"Ошибка подтверждения: {ex.Message}", "Ошибка",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
					}
				}
			}
		}

		private void IssueBooks_Click(object sender, RoutedEventArgs e)
		{
			Button btn = sender as Button;
			if (btn != null && btn.Tag != null)
			{
				int requestId = (int)btn.Tag;

				var request = _allRequests.FirstOrDefault(r => r.ID_Request == requestId);
				if (request != null)
				{
					if (request.StatusName == "Отменена")
					{
						CustomMessageBox.Show("Нельзя выдать книги по отмененной заявке.", "Ошибка",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
						return;
					}

					if (request.StatusName != "Готова")
					{
						CustomMessageBox.Show($"Книги по заявке еще не готовы к выдаче. Текущий статус: {request.StatusName}", "Ошибка",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Warning);
						return;
					}

					var infoServicePage = new InformationServicePage();

					if (request.GroupID.HasValue && request.GroupID > 0)
					{
						infoServicePage.SetDataFromRequest(
							requestId,
							request.UserID,
							request.GroupID,
							request.UserName,
							request.GroupCode,
							request.LessonName,
							request.LessonDate
						);
					}
					else
					{
						infoServicePage.SetDataFromRequestIndividual(
							requestId,
							request.UserID,
							request.UserName,
							request.LessonName,
							request.LessonDate,
							request.UserTypeID
						);
					}

					NavigationService?.Navigate(infoServicePage);
				}
			}
		}

		private async void CancelRequest_Click(object sender, RoutedEventArgs e)
		{
			Button btn = sender as Button;
			if (btn != null && btn.Tag != null)
			{
				int requestId = (int)btn.Tag;

				var result = CustomMessageBox.Show(
					"Отменить заявку?",
					"Подтверждение отмены",
					CustomMessageBoxButton.YesNo,
					CustomMessageBoxImage.Question);

				if (result == CustomMessageBoxResult.Yes)
				{
					try
					{
						int cancelledStatusId = 5;

						using (var conn = new SqlConnection(_connectionString))
						{
							await conn.OpenAsync();
							string query = "UPDATE BookRequests SET StatusID = @StatusId WHERE ID_Request = @RequestId";
							using (var cmd = new SqlCommand(query, conn))
							{
								cmd.Parameters.AddWithValue("@StatusId", cancelledStatusId);
								cmd.Parameters.AddWithValue("@RequestId", requestId);
								await cmd.ExecuteNonQueryAsync();
							}
						}

						CustomMessageBox.Show("Заявка отменена.", "Успешно",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Information);

						await LoadDataAsync();
						DetailsPanel.Visibility = Visibility.Collapsed;
					}
					catch (Exception ex)
					{
						CustomMessageBox.Show($"Ошибка отмены: {ex.Message}", "Ошибка",
							CustomMessageBoxButton.OK, CustomMessageBoxImage.Error);
					}
				}
			}
		}

		private void DgRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (DgRequests.SelectedItem is RequestItem selected)
			{
				_ = ShowRequestDetails(selected.ID_Request);
			}
		}

		private void NavigateToRequest_Click(object sender, RoutedEventArgs e)
		{
			Button btn = sender as Button;
			if (btn != null && btn.Tag != null)
			{
				int requestId = (int)btn.Tag;
				_ = ShowRequestDetails(requestId);
			}
		}
	}
}