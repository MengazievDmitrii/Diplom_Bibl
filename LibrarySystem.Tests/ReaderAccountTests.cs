using LibrarySystem.Tests.Services;
using Xunit;

namespace LibraryAIS.Tests;

public class ReaderAccountTests
{
    [Fact]
    public void CreateReaderAccount_ShouldAssignReaderRole()
    {
        var service = new ReaderAccountService();

        bool result = service.CreateAccount(
            "Иванов",
            "Иван",
            "ИС-121п");

        Assert.True(result);
    }
}