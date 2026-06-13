using LibrarySystem.Tests.Models;

namespace LibrarySystem.Tests.Services
{
    public class BookService
    {
        public bool AddBook(Book book)
        {
            return !string.IsNullOrWhiteSpace(book.Title);
        }
    }
}
