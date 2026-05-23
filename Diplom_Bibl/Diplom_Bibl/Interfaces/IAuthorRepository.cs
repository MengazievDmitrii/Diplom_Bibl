using System.Collections.Generic;

namespace Diplom_Bibl.Interfaces
{
    public interface IAuthorRepository
    {
        int InsertAuthor(string surname, string name, string patronymic);
        IEnumerable<dynamic> GetAllAuthors();
        bool AuthorExists(string surname, string name, string patronymic);
    }
}