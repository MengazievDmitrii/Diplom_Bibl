using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class RequestTests
    {
        [Fact]
        public void CreateBookRequest_ShouldCreateApplication()
        {
            var service = new RequestService();

            int requestId = service.CreateRequest(1);

            Assert.True(requestId > 0);
        }
    }
}
