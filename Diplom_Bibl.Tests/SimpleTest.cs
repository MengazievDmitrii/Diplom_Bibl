using Xunit;

namespace Diplom_Bibl.Tests
{
    public class SimpleTest
    {
        [Fact]
        public void TestMath()
        {
            // Самый простой тест в мире
            int a = 2;
            int b = 2;
            int result = a + b;

            // Проверяем, что 2+2=4
            Assert.Equal(4, result);
        }
    }
}