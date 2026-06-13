using LibrarySystem.Tests.Models;
using LibrarySystem.Tests.Services;

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
