using Diplom_Bibl.Interfaces;
using Diplom_Bibl.Services;
using Moq;
using System;
using Xunit;

namespace Diplom_Bibl.Tests.Services
{
    public class AuthorServiceTests
    {
        [Fact]
        public void AddAuthor_ValidData_ReturnsNewId()
        {
            // Arrange (подготовка)
            var mockRepo = new Mock<IAuthorRepository>();
            mockRepo.Setup(repo => repo.AuthorExists("Толстой", "Лев", "Николаевич"))
                    .Returns(false);
            mockRepo.Setup(repo => repo.InsertAuthor("Толстой", "Лев", "Николаевич"))
                    .Returns(42);

            var service = new AuthorService(mockRepo.Object);

            // Act (действие)
            int result = service.AddAuthor("Толстой", "Лев", "Николаевич");

            // Assert (проверка)
            Assert.Equal(42, result);
        }

        [Fact]
        public void AddAuthor_EmptySurname_ThrowsException()
        {
            // Arrange
            var mockRepo = new Mock<IAuthorRepository>();
            var service = new AuthorService(mockRepo.Object);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                service.AddAuthor("", "Лев", "Николаевич"));
        }

        [Fact]
        public void AddAuthor_DuplicateAuthor_ThrowsException()
        {
            // Arrange
            var mockRepo = new Mock<IAuthorRepository>();
            mockRepo.Setup(repo => repo.AuthorExists("Толстой", "Лев", "Николаевич"))
                    .Returns(true);

            var service = new AuthorService(mockRepo.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                service.AddAuthor("Толстой", "Лев", "Николаевич"));
        }
    }
}