using LibrarySystem.Tests.Services;

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
