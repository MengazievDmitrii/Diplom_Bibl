using System.Collections.Generic;

namespace Diplom_Bibl.Interfaces
{
    public interface IGenreRepository
    {
        int InsertGenre(string title);
        bool GenreExists(string title);
        IEnumerable<dynamic> GetAllGenres();
        dynamic GetGenreById(int id);
        void UpdateGenre(int id, string title);
        void DeleteGenre(int id);
    }
}