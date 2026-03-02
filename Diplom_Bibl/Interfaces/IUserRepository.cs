using System.Collections.Generic;

namespace Diplom_Bibl.Interfaces
{
    public interface IUserRepository
    {
        int InsertUser(string surname, string name, string patronymic, string login, string password);
        void UpdateUser(int id, string surname, string name, string patronymic, string login, string password);
        void DeleteUser(int id);
        void AssignRolesToUser(int userId, List<int> roleIds);
        void RemoveUserRoles(int userId);
        IEnumerable<dynamic> GetAllUsers();
        dynamic GetUserById(int id);
        bool LoginExists(string login);
    }
}