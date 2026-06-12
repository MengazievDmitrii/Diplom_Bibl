using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class ApproveRequestTests
    {
        [Fact]
        public void ApproveRequest_ShouldReserveBooks()
        {
            var service = new RequestService();

            bool result = service.ApproveRequest(1);

            Assert.True(result);
        }
    }
}
