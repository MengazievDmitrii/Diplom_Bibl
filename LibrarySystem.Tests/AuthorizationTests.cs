using LibrarySystem.Tests.Services;

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
