using Diplom_Bibl.Interfaces;
using Diplom_Bibl.Services;
using Moq;
using System;
using Xunit;

namespace Diplom_Bibl.Tests.Services
{
    public class ReceiptServiceTests
    {
        private readonly Mock<IReceiptRepository> _mockRepo;
        private readonly ReceiptService _service;
        private readonly ReceiptNumberGenerator _generator;

        public ReceiptServiceTests()
        {
            _mockRepo = new Mock<IReceiptRepository>();
            _generator = new ReceiptNumberGenerator();
            _service = new ReceiptService(_mockRepo.Object, _generator);
        }

        [Fact]
        public void InsertReceipt_ValidData_ReturnsNewId()
        {
            // Arrange (Подготовка)
            int responsibleId = 5;
            string comment = "Тестовый приход";
            int expectedId = 42;
            int nextNumber = 42;

            // Настраиваем mock: GetNextReceiptNumber возвращает 42
            _mockRepo.Setup(repo => repo.GetNextReceiptNumber()).Returns(nextNumber);

            // Настраиваем mock: InsertReceipt возвращает ожидаемый ID
            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, comment))
                .Returns(expectedId);

            // Act (Действие)
            int result = _service.AddReceipt(responsibleId, comment);

            // Assert (Проверка)
            Assert.Equal(expectedId, result);

            // Проверяем, что методы были вызваны с правильными параметрами
            _mockRepo.Verify(repo => repo.GetNextReceiptNumber(), Times.Once);
            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, comment),
                Times.Once);
        }

        [Fact]
        public void InsertReceipt_NullComment_UsesEmptyString()
        {
            // Arrange
            int responsibleId = 5;
            string comment = null;
            int expectedId = 42;
            int nextNumber = 42;

            _mockRepo.Setup(repo => repo.GetNextReceiptNumber()).Returns(nextNumber);
            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, ""))
                .Returns(expectedId);

            // Act
            int result = _service.AddReceipt(responsibleId, comment);

            // Assert
            Assert.Equal(expectedId, result);
            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, ""),
                Times.Once);
        }

        [Fact]
        public void InsertReceipt_InvalidResponsibleId_ThrowsException()
        {
            // Arrange
            int responsibleId = -1;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddReceipt(responsibleId, "comment"));

            Assert.Contains("ID ответственного", ex.Message);

            // Проверяем, что методы репозитория НЕ вызывались
            _mockRepo.Verify(repo => repo.GetNextReceiptNumber(), Times.Never);
            _mockRepo.Verify(repo => repo.InsertReceipt(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void InsertReceipt_ResponsibleIdZero_ThrowsException()
        {
            // Arrange
            int responsibleId = 0;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddReceipt(responsibleId, "comment"));

            Assert.Contains("ID ответственного", ex.Message);
        }

        [Fact]
        public void GenerateReceiptNumber_ValidId_ReturnsFormattedString()
        {
            // Arrange
            var generator = new ReceiptNumberGenerator();

            // Act & Assert
            Assert.Equal("ПН-0005", generator.GenerateReceiptNumber(5));
            Assert.Equal("ПН-0010", generator.GenerateReceiptNumber(10));
            Assert.Equal("ПН-0123", generator.GenerateReceiptNumber(123));
            Assert.Equal("ПН-0001", generator.GenerateReceiptNumber(1));
            Assert.Equal("ПН-9999", generator.GenerateReceiptNumber(9999));
        }

        [Fact]
        public void InsertReceipt_WithEmptyComment_UsesEmptyString()
        {
            // Arrange
            int responsibleId = 5;
            string comment = "";
            int expectedId = 42;
            int nextNumber = 42;

            _mockRepo.Setup(repo => repo.GetNextReceiptNumber()).Returns(nextNumber);
            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, ""))
                .Returns(expectedId);

            // Act
            int result = _service.AddReceipt(responsibleId, comment);

            // Assert
            Assert.Equal(expectedId, result);
            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, ""),
                Times.Once);
        }

        [Fact]
        public void InsertReceipt_VerifyCorrectDateFormat()
        {
            // Arrange
            int responsibleId = 5;
            string comment = "Тест";
            int nextNumber = 42;
            DateTime beforeCall = DateTime.Now.AddSeconds(-1);

            _mockRepo.Setup(repo => repo.GetNextReceiptNumber()).Returns(nextNumber);
            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, comment))
                .Returns(42);

            // Act
            _service.AddReceipt(responsibleId, comment);

            // Assert - проверяем, что дата установлена корректно (текущая)
            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0042",
                It.Is<DateTime>(d => d >= beforeCall && d <= DateTime.Now.AddSeconds(1)),
                responsibleId,
                comment),
                Times.Once);
        }

        [Fact]
        public void InsertReceipt_RepositoryThrowsException_PropagatesException()
        {
            // Arrange
            int responsibleId = 5;
            string comment = "Тест";
            int nextNumber = 42;

            _mockRepo.Setup(repo => repo.GetNextReceiptNumber()).Returns(nextNumber);
            _mockRepo.Setup(repo => repo.InsertReceipt(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Ошибка базы данных"));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.AddReceipt(responsibleId, comment));

            Assert.Contains("Ошибка базы данных", ex.Message);
        }

        [Fact]
        public void InsertReceipt_MultipleCalls_GeneratesSequentialNumbers()
        {
            // Arrange
            int responsibleId = 5;
            string comment = "Тест";

            // Первый вызов
            _mockRepo.SetupSequence(repo => repo.GetNextReceiptNumber())
                .Returns(42)  // первый вызов
                .Returns(43); // второй вызов

            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, comment))
                .Returns(100);
            _mockRepo.Setup(repo => repo.InsertReceipt(
                "ПН-0043", It.IsAny<DateTime>(), responsibleId, comment))
                .Returns(101);

            // Act - первый вызов
            int result1 = _service.AddReceipt(responsibleId, comment);

            // Act - второй вызов
            int result2 = _service.AddReceipt(responsibleId, comment);

            // Assert
            Assert.Equal(100, result1);
            Assert.Equal(101, result2);

            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0042", It.IsAny<DateTime>(), responsibleId, comment), Times.Once);
            _mockRepo.Verify(repo => repo.InsertReceipt(
                "ПН-0043", It.IsAny<DateTime>(), responsibleId, comment), Times.Once);
        }
    }
}