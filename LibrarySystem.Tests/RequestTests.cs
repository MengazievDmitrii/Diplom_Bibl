using LibrarySystem.Tests.Services;

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
