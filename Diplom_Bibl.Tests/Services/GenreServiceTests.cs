using Diplom_Bibl.Interfaces;
using Diplom_Bibl.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Diplom_Bibl.Tests.Services
{
    public class GenreServiceTests
    {
        private readonly Mock<IGenreRepository> _mockRepo;
        private readonly GenreService _service;

        public GenreServiceTests()
        {
            _mockRepo = new Mock<IGenreRepository>();
            _service = new GenreService(_mockRepo.Object);
        }

        [Fact]
        public void AddGenre_ValidTitle_ReturnsNewId()
        {
            // Arrange (Подготовка)
            string title = "Фантастика";
            int expectedId = 10;

            // Настраиваем mock: жанр не существует
            _mockRepo.Setup(repo => repo.GenreExists(title)).Returns(false);

            // Настраиваем mock: InsertGenre возвращает ожидаемый ID
            _mockRepo.Setup(repo => repo.InsertGenre(title)).Returns(expectedId);

            // Act (Действие)
            int result = _service.AddGenre(title);

            // Assert (Проверка)
            Assert.Equal(expectedId, result);

            // Проверяем, что методы были вызваны с правильными параметрами
            _mockRepo.Verify(repo => repo.GenreExists(title), Times.Once);
            _mockRepo.Verify(repo => repo.InsertGenre(title), Times.Once);
        }

        [Fact]
        public void AddGenre_EmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            string title = "";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddGenre(title));

            Assert.Contains("Название жанра", ex.Message);

            // Проверяем, что методы репозитория НЕ вызывались
            _mockRepo.Verify(repo => repo.GenreExists(It.IsAny<string>()), Times.Never);
            _mockRepo.Verify(repo => repo.InsertGenre(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void AddGenre_WhitespaceTitle_ThrowsArgumentException()
        {
            // Arrange
            string title = "   ";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.AddGenre(title));

            Assert.Contains("Название жанра", ex.Message);
        }

        [Fact]
        public void AddGenre_DuplicateTitle_ThrowsInvalidOperationException()
        {
            // Arrange
            string title = "Фантастика";

            // Настраиваем mock: жанр уже существует
            _mockRepo.Setup(repo => repo.GenreExists(title)).Returns(true);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.AddGenre(title));

            Assert.Contains("уже существует", ex.Message);

            // Проверяем, что InsertGenre НЕ вызывался
            _mockRepo.Verify(repo => repo.InsertGenre(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void AddGenre_TrimsWhitespace()
        {
            // Arrange
            string titleWithSpaces = "  Фантастика  ";
            string trimmedTitle = "Фантастика";
            int expectedId = 10;

            _mockRepo.Setup(repo => repo.GenreExists(trimmedTitle)).Returns(false);
            _mockRepo.Setup(repo => repo.InsertGenre(trimmedTitle)).Returns(expectedId);

            // Act
            int result = _service.AddGenre(titleWithSpaces);

            // Assert
            Assert.Equal(expectedId, result);
            _mockRepo.Verify(repo => repo.GenreExists(trimmedTitle), Times.Once);
            _mockRepo.Verify(repo => repo.InsertGenre(trimmedTitle), Times.Once);
        }

        [Fact]
        public void SearchGenres_WithSearchTerm_ReturnsFilteredList()
        {
            // Arrange
            var genres = new List<dynamic>
            {
                new { ID_Gener = 1, TitleGener = "Фантастика" },
                new { ID_Gener = 2, TitleGener = "Детектив" },
                new { ID_Gener = 3, TitleGener = "Роман" },
                new { ID_Gener = 4, TitleGener = "Фэнтези" }
            };

            _mockRepo.Setup(repo => repo.GetAllGenres()).Returns(genres);

            // Act
            var result = _service.SearchGenres("фан");

            // Assert
            Assert.Equal(2, result.Count()); // Должны найти "Фантастика" и "Фэнтези"
            Assert.Contains(result, g => g.TitleGener == "Фантастика");
            Assert.Contains(result, g => g.TitleGener == "Фэнтези");
        }

        [Fact]
        public void SearchGenres_WithEmptySearchTerm_ReturnsAllGenres()
        {
            // Arrange
            var genres = new List<dynamic>
            {
                new { ID_Gener = 1, TitleGener = "Фантастика" },
                new { ID_Gener = 2, TitleGener = "Детектив" },
                new { ID_Gener = 3, TitleGener = "Роман" }
            };

            _mockRepo.Setup(repo => repo.GetAllGenres()).Returns(genres);

            // Act
            var result = _service.SearchGenres("");

            // Assert
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public void SearchGenres_WithNullSearchTerm_ReturnsAllGenres()
        {
            // Arrange
            var genres = new List<dynamic>
            {
                new { ID_Gener = 1, TitleGener = "Фантастика" },
                new { ID_Gener = 2, TitleGener = "Детектив" }
            };

            _mockRepo.Setup(repo => repo.GetAllGenres()).Returns(genres);

            // Act
            var result = _service.SearchGenres(null);

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void SearchGenres_CaseInsensitiveSearch_WorksCorrectly()
        {
            // Arrange
            var genres = new List<dynamic>
            {
                new { ID_Gener = 1, TitleGener = "ФАНТАСТИКА" },
                new { ID_Gener = 2, TitleGener = "Детектив" }
            };

            _mockRepo.Setup(repo => repo.GetAllGenres()).Returns(genres);

            // Act - ищем в нижнем регистре
            var result = _service.SearchGenres("фантастика");

            // Assert
            Assert.Single(result);
            Assert.Equal("ФАНТАСТИКА", result.First().TitleGener);
        }

        [Fact]
        public void SearchGenres_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var genres = new List<dynamic>
            {
                new { ID_Gener = 1, TitleGener = "Фантастика" },
                new { ID_Gener = 2, TitleGener = "Детектив" }
            };

            _mockRepo.Setup(repo => repo.GetAllGenres()).Returns(genres);

            // Act
            var result = _service.SearchGenres("несуществующий");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void UpdateGenre_ValidData_UpdatesSuccessfully()
        {
            // Arrange
            int id = 1;
            string newTitle = "Научная фантастика";
            var existingGenre = new { ID_Gener = 1, TitleGener = "Фантастика" };

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns(existingGenre);
            _mockRepo.Setup(repo => repo.GenreExists(newTitle)).Returns(false);

            // Act
            _service.UpdateGenre(id, newTitle);

            // Assert
            _mockRepo.Verify(repo => repo.UpdateGenre(id, newTitle), Times.Once);
        }

        [Fact]
        public void UpdateGenre_EmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            int id = 1;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _service.UpdateGenre(id, ""));
        }

        [Fact]
        public void UpdateGenre_NonExistentGenre_ThrowsInvalidOperationException()
        {
            // Arrange
            int id = 999;
            string newTitle = "Новый жанр";

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns((dynamic)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _service.UpdateGenre(id, newTitle));
        }

        [Fact]
        public void UpdateGenre_DuplicateTitle_ThrowsInvalidOperationException()
        {
            // Arrange
            int id = 1;
            string duplicateTitle = "Детектив";
            var existingGenre = new { ID_Gener = 1, TitleGener = "Фантастика" };

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns(existingGenre);
            _mockRepo.Setup(repo => repo.GenreExists(duplicateTitle)).Returns(true);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _service.UpdateGenre(id, duplicateTitle));
        }

        [Fact]
        public void UpdateGenre_SameTitle_DoesNotCheckDuplicate()
        {
            // Arrange
            int id = 1;
            string sameTitle = "Фантастика";
            var existingGenre = new { ID_Gener = 1, TitleGener = "Фантастика" };

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns(existingGenre);

            // Act
            _service.UpdateGenre(id, sameTitle);

            // Assert - проверяем, что GenreExists НЕ вызывался
            _mockRepo.Verify(repo => repo.GenreExists(It.IsAny<string>()), Times.Never);
            _mockRepo.Verify(repo => repo.UpdateGenre(id, sameTitle), Times.Once);
        }

        [Fact]
        public void DeleteGenre_ExistingGenre_DeletesSuccessfully()
        {
            // Arrange
            int id = 1;
            var existingGenre = new { ID_Gener = 1, TitleGener = "Фантастика" };

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns(existingGenre);

            // Act
            _service.DeleteGenre(id);

            // Assert
            _mockRepo.Verify(repo => repo.DeleteGenre(id), Times.Once);
        }

        [Fact]
        public void DeleteGenre_NonExistentGenre_ThrowsInvalidOperationException()
        {
            // Arrange
            int id = 999;

            _mockRepo.Setup(repo => repo.GetGenreById(id)).Returns((dynamic)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _service.DeleteGenre(id));
        }
    }
}