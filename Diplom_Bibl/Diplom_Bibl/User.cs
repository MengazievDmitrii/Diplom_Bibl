using System.Collections.Generic;

namespace Diplom_Bibl.Models
{
    public class User
    {
        public int ID_User { get; set; }
        public string Surname { get; set; } = "";
        public string Name { get; set; } = "";
        public string Patronymic { get; set; } = "";
        public string Login { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        // Связь с читательским билетом
        public int? ReaderID { get; set; }

        // Информация из читательского билета
        public string CardNumber { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public int TypeID { get; set; }
        public string TypeName { get; set; } = "";
        public int? GroupID { get; set; }
        public string GroupCode { get; set; } = "";

        // Роли пользователя
        public List<string> Roles { get; set; } = new List<string>();

        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();

        public bool IsAdmin => Roles.Contains("ADMIN");
        public bool IsLibrarian => Roles.Contains("LIBRARIAN");
        public bool IsReader => Roles.Contains("READER");
    }
}