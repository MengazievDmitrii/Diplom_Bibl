using Diplom_Bibl.Interfaces;
using Diplom_Bibl.Services;
using Moq;
using System;
using Xunit;

namespace Diplom_Bibl.Tests.Services
{
    public class BookServiceTests
    {
        private readonly Mock<IBookRepository> _mockRepo;
        private readonly BookService _service;

        public BookServiceTests()
        {
            _mockRepo = new Mock<IBookRepository>();
            _service = new BookService(_mockRepo.Object);
        }

        [Fact]
        public void InsertBook_ValidData_ReturnsNewId()
        {
            // Arrange (Подготовка)
            string invNumber = "001";
            string title = "Война и мир";
            int authorId = 1;
            int year = 1869;
            int viewId = 1;
            int expectedId = 100;

            // Настраиваем mock: проверка существования номера - false (номер свободен)
            _mockRepo.Setup(repo => repo.BookExists(invNumber)).Returns(false);

            // Настраиваем mock: InsertBook возвращает ожидаемый ID
            _mockRepo.Setup(repo => repo.InsertBook(
                invNumber, title, authorId, null, year, null, null, null, null, viewId, null))
                .Returns(expectedId);

            // Act (Действие)
            int result = _service.AddBook(
                invNumber, title, authorId, null, year,
                null, null, null, null, viewId, null);

            // Assert (Проверка)
            Assert.Equal(expectedId, result);

            // Проверяем, что методы были вызваны ровно 1 раз
            _mockRepo.Verify(repo => repo.BookExists(invNumber), Times.Once);
            _mockRepo.Verify(repo => repo.InsertBook(
                invNumber, title, authorId, null, year, null, null, null, null, viewId, null),
                Times.Once);
        }

        [Fact]
        public void InsertBook_DuplicateInventoryNumber_ThrowsException()
        {
            // Arrange
            string invNumber = "001";

            // Настраиваем mock: номер уже существует
            _mockRepo.Setup(repo => repo.BookExists(invNumber)).Returns(true);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.AddBook(invNumber, "title", 1, null, 2020,
                null, null, null, null, 1, null));

            Assert.Contains("Инвентарный номер уже существует", ex.Message);

            // Проверяем, что InsertBook НЕ вызывался
            _mockRepo.Verify(repo => repo.InsertBook(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void InsertBook_EmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("001", "", 1, null, 2020,
                null, null, null, null, 1, null));

            Assert.Contains("Название", ex.Message);
        }

        [Fact]
        public void InsertBook_EmptyInventoryNumber_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("", "title", 1, null, 2020,
                null, null, null, null, 1, null));

            Assert.Contains("Инвентарный номер", ex.Message);
        }

        [Fact]
        public void InsertBook_InvalidAuthorId_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("001", "title", 0, null, 2020,
                null, null, null, null, 1, null));

            Assert.Contains("автор", ex.Message.ToLower());
        }

        [Fact]
        public void InsertBook_FutureYear_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);
            int futureYear = DateTime.Now.Year + 1;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("001", "title", 1, null, futureYear,
                null, null, null, null, 1, null));

            Assert.Contains("Год", ex.Message);
        }

        [Fact]
        public void InsertBook_YearLessThan1000_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("001", "title", 1, null, 999,
                null, null, null, null, 1, null));

            Assert.Contains("Год", ex.Message);
        }

        [Fact]
        public void InsertBook_InvalidViewId_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.BookExists(It.IsAny<string>())).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddBook("001", "title", 1, null, 2020,
                null, null, null, null, 0, null));

            Assert.Contains("вид книги", ex.Message.ToLower());
        }

        [Fact]
        public void InsertBook_WithAllOptionalFields_CallsRepositoryWithCorrectValues()
        {
            // Arrange
            string invNumber = "001";
            string title = "Война и мир";
            int authorId = 1;
            int? publisherId = 5;
            int year = 1869;
            int? genreId = 3;
            int? statusId = 2;
            string annotation = "Тестовая аннотация";
            int? receiptId = 10;
            int viewId = 1;
            string price = "500.00";
            int expectedId = 100;

            _mockRepo.Setup(repo => repo.BookExists(invNumber)).Returns(false);
            _mockRepo.Setup(repo => repo.InsertBook(
                invNumber, title, authorId, publisherId, year, genreId, statusId,
                annotation, receiptId, viewId, price))
                .Returns(expectedId);

            // Act
            int result = _service.AddBook(
                invNumber, title, authorId, publisherId, year,
                genreId, statusId, annotation, receiptId, viewId, price);

            // Assert
            Assert.Equal(expectedId, result);
            _mockRepo.Verify(repo => repo.InsertBook(
                invNumber, title, authorId, publisherId, year, genreId, statusId,
                annotation, receiptId, viewId, price), Times.Once);
        }
    }
}