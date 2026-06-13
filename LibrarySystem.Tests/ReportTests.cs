using LibrarySystem.Tests.Services;

namespace LibrarySystem.Tests
{
    public class ReportTests
    {
        [Fact]
        public void GenerateFundReport_ShouldCreateReport()
        {
            var service = new ReportService();

            string reportFile = service.GenerateFundReport();

            Assert.False(string.IsNullOrEmpty(reportFile));
        }
    }
}
