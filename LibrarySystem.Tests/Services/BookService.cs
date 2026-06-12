using LibrarySystem.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
