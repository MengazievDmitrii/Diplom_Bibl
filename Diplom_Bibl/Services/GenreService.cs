using Diplom_Bibl.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diplom_Bibl.Services
{
    public class GenreService
    {
        private readonly IGenreRepository _repository;

        public GenreService(IGenreRepository repository)
        {
            _repository = repository;
        }

        public int AddGenre(string title)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название жанра не может быть пустым");

            // Очищаем от пробелов
            string trimmedTitle = title.Trim();

            // Проверка на дубликат
            if (_repository.GenreExists(trimmedTitle))
                throw new InvalidOperationException($"Жанр '{trimmedTitle}' уже существует");

            // Сохраняем
            return _repository.InsertGenre(trimmedTitle);
        }

        public IEnumerable<dynamic> SearchGenres(string searchTerm)
        {
            var allGenres = _repository.GetAllGenres();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return allGenres;

            // Поиск без учета регистра
            return allGenres.Where(g =>
                g.TitleGener.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void UpdateGenre(int id, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название жанра не может быть пустым");

            string trimmedTitle = title.Trim();

            var existing = _repository.GetGenreById(id);
            if (existing == null)
                throw new InvalidOperationException("Жанр не найден");

            // Если название изменилось, проверяем уникальность
            if (!existing.TitleGener.Equals(trimmedTitle, StringComparison.OrdinalIgnoreCase))
            {
                if (_repository.GenreExists(trimmedTitle))
                    throw new InvalidOperationException($"Жанр '{trimmedTitle}' уже существует");
            }

            _repository.UpdateGenre(id, trimmedTitle);
        }

        public void DeleteGenre(int id)
        {
            var existing = _repository.GetGenreById(id);
            if (existing == null)
                throw new InvalidOperationException("Жанр не найден");

            _repository.DeleteGenre(id);
        }
    }
}