using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class AuthorizationTests
    {
        [Fact]
        public void Login_WithValidCredentials_ShouldOpenMainWindow()
        {
            var service = new AuthService();

            bool result = service.Login("admin", "12345");

            Assert.True(result);
        }
    }
}
