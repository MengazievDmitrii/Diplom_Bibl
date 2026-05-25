using Diplom_Bibl.Interfaces;
using Diplom_Bibl.Services;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Diplom_Bibl.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockRepo;
        private readonly UserService _service;

        public UserServiceTests()
        {
            _mockRepo = new Mock<IUserRepository>();
            _service = new UserService(_mockRepo.Object);
        }

        [Fact]
        public void InsertUser_ValidData_ReturnsNewId()
        {
            // Arrange (Подготовка)
            string surname = "Иванов";
            string name = "Иван";
            string patronymic = "Иванович";
            string login = "ivanov";
            string password = "12345";
            var roles = new List<int> { 1, 2 };
            int expectedUserId = 10;

            // Настраиваем mock: логин свободен
            _mockRepo.Setup(repo => repo.LoginExists(login)).Returns(false);

            // Настраиваем mock: InsertUser возвращает ожидаемый ID
            _mockRepo.Setup(repo => repo.InsertUser(surname, name, patronymic, login, password))
                     .Returns(expectedUserId);

            // Act (Действие)
            int result = _service.AddUser(surname, name, patronymic, login, password, roles);

            // Assert (Проверка)
            Assert.Equal(expectedUserId, result);

            // Проверяем, что методы были вызваны с правильными параметрами
            _mockRepo.Verify(repo => repo.LoginExists(login), Times.Once);
            _mockRepo.Verify(repo => repo.InsertUser(surname, name, patronymic, login, password), Times.Once);
            _mockRepo.Verify(repo => repo.AssignRolesToUser(expectedUserId, roles), Times.Once);
        }

        [Fact]
        public void InsertUser_DuplicateLogin_ThrowsException()
        {
            // Arrange
            string login = "existing";
            var roles = new List<int> { 1 };

            // Настраиваем mock: логин уже занят
            _mockRepo.Setup(repo => repo.LoginExists(login)).Returns(true);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.AddUser("Иванов", "Иван", "Иванович", login, "123", roles));

            Assert.Contains("Логин уже занят", ex.Message);

            // Проверяем, что InsertUser НЕ вызывался
            _mockRepo.Verify(repo => repo.InsertUser(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void InsertUser_NoRoles_ThrowsArgumentException()
        {
            // Arrange
            _mockRepo.Setup(repo => repo.LoginExists("ivanov")).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddUser("Иванов", "Иван", "Иванович", "ivanov", "123", new List<int>()));

            Assert.Contains("хотя бы одна роль", ex.Message);
        }

        [Fact]
        public void InsertUser_EmptyPassword_ThrowsArgumentException()
        {
            // Arrange
            var roles = new List<int> { 1 };
            _mockRepo.Setup(repo => repo.LoginExists("ivanov")).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddUser("Иванов", "Иван", "Иванович", "ivanov", "", roles));

            Assert.Contains("Пароль", ex.Message);
        }

        [Fact]
        public void InsertUser_EmptySurname_ThrowsArgumentException()
        {
            // Arrange
            var roles = new List<int> { 1 };
            _mockRepo.Setup(repo => repo.LoginExists("ivanov")).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddUser("", "Иван", "Иванович", "ivanov", "123", roles));

            Assert.Contains("Фамилия", ex.Message);
        }

        [Fact]
        public void InsertUser_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var roles = new List<int> { 1 };
            _mockRepo.Setup(repo => repo.LoginExists("ivanov")).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddUser("Иванов", "", "Иванович", "ivanov", "123", roles));

            Assert.Contains("Имя", ex.Message);
        }

        [Fact]
        public void InsertUser_EmptyLogin_ThrowsArgumentException()
        {
            // Arrange
            var roles = new List<int> { 1 };
            _mockRepo.Setup(repo => repo.LoginExists("")).Returns(false);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddUser("Иванов", "Иван", "Иванович", "", "123", roles));

            Assert.Contains("Логин", ex.Message);
        }

        [Fact]
        public void InsertUser_WithNullPatronymic_CallsRepositoryWithEmptyString()
        {
            // Arrange
            string surname = "Иванов";
            string name = "Иван";
            string patronymic = null;
            string login = "ivanov";
            string password = "12345";
            var roles = new List<int> { 1, 2 };
            int expectedUserId = 10;

            _mockRepo.Setup(repo => repo.LoginExists(login)).Returns(false);
            _mockRepo.Setup(repo => repo.InsertUser(surname, name, "", login, password))
                     .Returns(expectedUserId);

            // Act
            int result = _service.AddUser(surname, name, patronymic, login, password, roles);

            // Assert
            Assert.Equal(expectedUserId, result);
            _mockRepo.Verify(repo => repo.InsertUser(surname, name, "", login, password), Times.Once);
        }

        [Fact]
        public void InsertUser_WithWhitespaceInFields_TrimsValues()
        {
            // Arrange
            string surname = "  Иванов  ";
            string name = "  Иван  ";
            string patronymic = "  Иванович  ";
            string login = "  ivanov  ";
            string password = "12345";
            var roles = new List<int> { 1, 2 };
            int expectedUserId = 10;

            _mockRepo.Setup(repo => repo.LoginExists("ivanov")).Returns(false);
            _mockRepo.Setup(repo => repo.InsertUser("Иванов", "Иван", "Иванович", "ivanov", password))
                     .Returns(expectedUserId);

            // Act
            int result = _service.AddUser(surname, name, patronymic, login, password, roles);

            // Assert
            Assert.Equal(expectedUserId, result);
            _mockRepo.Verify(repo => repo.InsertUser("Иванов", "Иван", "Иванович", "ivanov", password), Times.Once);
        }

        [Fact]
        public void InsertUser_RepositoryThrowsException_PropagatesException()
        {
            // Arrange
            string login = "ivanov";
            var roles = new List<int> { 1 };

            _mockRepo.Setup(repo => repo.LoginExists(login)).Returns(false);
            _mockRepo.Setup(repo => repo.InsertUser(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new SqlException("Ошибка базы данных")); // Примечание: SqlException нужно подключить

            // Act & Assert
            Assert.Throws<SqlException>(() =>
                _service.AddUser("Иванов", "Иван", "Иванович", login, "123", roles));
        }
    }
}

// Вспомогательный класс для теста (добавьте в конец файла или отдельно)
public class SqlException : Exception
{
    public SqlException(string message) : base(message) { }
}