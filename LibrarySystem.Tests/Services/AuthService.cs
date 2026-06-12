using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystem.Tests.Services
{
    public class AuthService
    {
        public bool Login(string login, string password)
        {
            return login == "admin" && password == "12345";
        }
    }
}
