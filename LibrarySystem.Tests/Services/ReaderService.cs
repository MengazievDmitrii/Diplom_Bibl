using LibrarySystem.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
