using LibrarySystem.Tests.Models;
using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class BookTests
    {
        [Fact]
        public void AddBook_ShouldSaveBook()
        {
            var service = new BookService();

            var book = new Book
            {
                Title = "Война и мир"
            };

            bool result = service.AddBook(book);

            Assert.True(result);
        }
    }
}
