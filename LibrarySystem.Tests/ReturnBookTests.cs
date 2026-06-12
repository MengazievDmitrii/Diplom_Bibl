using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class ReturnBookTests
    {
        [Fact]
        public void ReturnBook_ShouldChangeStatusToAvailable()
        {
            var service = new IssueService();

            bool result = service.ReturnBook(1);

            Assert.True(result);
        }
    }
}
