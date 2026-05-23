using Diplom_Bibl.Interfaces;
using System;

namespace Diplom_Bibl.Services
{
    public class BookService
    {
        private readonly IBookRepository _repository;

        public BookService(IBookRepository repository)
        {
            _repository = repository;
        }

        public int AddBook(string invNumber, string title, int authorId, int? publisherId,
                          int year, int? genreId, int? statusId, string annotation,
                          int? receiptId, int viewId, string price)
        {
            // Валидация обязательных полей
            if (string.IsNullOrWhiteSpace(invNumber))
                throw new ArgumentException("Инвентарный номер не может быть пустым");

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название не может быть пустым");

            if (authorId <= 0)
                throw new ArgumentException("Не выбран автор");

            if (viewId <= 0)
                throw new ArgumentException("Не выбран вид книги");

            // Проверка года
            if (year < 1000 || year > DateTime.Now.Year)
                throw new ArgumentException($"Год должен быть от 1000 до {DateTime.Now.Year}");

            // Проверка уникальности инвентарного номера
            if (_repository.BookExists(invNumber.Trim()))
                throw new InvalidOperationException("Инвентарный номер уже существует");

            // Сохранение книги
            return _repository.InsertBook(
                invNumber.Trim(),
                title.Trim(),
                authorId,
                publisherId,
                year,
                genreId,
                statusId,
                annotation?.Trim(),
                receiptId,
                viewId,
                price?.Trim());
        }
    }
}