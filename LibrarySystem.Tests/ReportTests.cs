using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
