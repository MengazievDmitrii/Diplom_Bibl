namespace LibrarySystem.Tests.Services
{
    public class ReaderAccountService
    {
        public bool CreateAccount(
            string lastName,
            string firstName,
            string group)
        {
            return !string.IsNullOrWhiteSpace(lastName)
                && !string.IsNullOrWhiteSpace(firstName)
                && !string.IsNullOrWhiteSpace(group);
        }
    }
}
