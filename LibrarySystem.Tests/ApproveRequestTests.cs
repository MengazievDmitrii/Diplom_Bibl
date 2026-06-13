using LibrarySystem.Tests.Services;

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
