using LibrarySystem.Tests.Models;

namespace LibrarySystem.Tests.Services
{
    public class ReaderService
    {
        public bool AddReader(Reader reader)
        {
            return !string.IsNullOrWhiteSpace(reader.LastName)
                && !string.IsNullOrWhiteSpace(reader.FirstName);
        }
    }
}
