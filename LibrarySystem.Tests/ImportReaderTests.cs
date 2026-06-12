using LibrarySystem.Tests.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests
{
    public class ImportReaderTests
    {
        [Fact]
        public void ImportReadersFromExcel_ShouldLoadData()
        {
            var service = new ReaderImportService();

            int importedReaders = service.Import("Readers.xlsx");

            Assert.True(importedReaders > 0);
        }
    }
}
