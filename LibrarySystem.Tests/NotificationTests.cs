using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibrarySystem.Tests.Services;

namespace LibrarySystem.Tests
{
    public class NotificationTests
    {
        [Fact]
        public void CreateOverdueNotification_ShouldNotifyReader()
        {
            var service = new NotificationService();

            bool result = service.CreateOverdueNotification(1);

            Assert.True(result);
        }
    }
}
