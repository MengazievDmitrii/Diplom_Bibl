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
