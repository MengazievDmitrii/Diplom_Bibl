// Вспомогательный класс для работы с подключением к БД
using System.Configuration;
using System.Data.SqlClient;

namespace Diplom_Bibl.Helpers
{
    public static class DatabaseHelper
    {
        private static string _connectionString;

        /// <summary>
        /// Возвращает строку подключения из App.config
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    // Пытаемся получить строку подключения из connectionStrings
                    var connectionStringSettings = ConfigurationManager.ConnectionStrings["DefaultConnection"];

                    if (connectionStringSettings != null)
                    {
                        _connectionString = connectionStringSettings.ConnectionString;
                    }
                    else
                    {
                        // Fallback на жестко заданную строку (на случай ошибки в конфиге)
                        _connectionString = "Server=DMITRIJ;Database=DiplomBibl;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True;";
                    }
                }
                return _connectionString;
            }
        }

        /// <summary>
        /// Создает новое подключение к базе данных
        /// </summary>
        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}