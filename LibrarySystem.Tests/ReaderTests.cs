using LibrarySystem.Tests.Models;
using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class ReaderTests
    {
        [Fact]
        public void AddReader_ShouldSaveReader()
        {
            var service = new ReaderService();

            var reader = new Reader
            {
                LastName = "Иванов",
                FirstName = "Иван"
            };

            bool result = service.AddReader(reader);

            Assert.True(result);
        }
    }
}
