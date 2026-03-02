using System.Collections.Generic;

namespace Diplom_Bibl.Interfaces
{
    public interface IBookRepository
    {
        int InsertBook(string invNumber, string title, int authorId, int? publisherId,
                      int year, int? genreId, int? statusId, string annotation,
                      int? receiptId, int viewId, string price);
        bool BookExists(string invNumber);
        IEnumerable<dynamic> GetAllBooks();
        dynamic GetBookById(int id);
        void UpdateBook(int id, string invNumber, string title, int? authorId,
                       int? publisherId, int year, int? genreId, int? statusId,
                       string annotation, int? receiptId, int? viewId, string price);
        void DeleteBook(int id);
    }
}