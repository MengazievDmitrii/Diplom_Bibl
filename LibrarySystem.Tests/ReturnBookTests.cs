using LibrarySystem.Tests.Services;

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
