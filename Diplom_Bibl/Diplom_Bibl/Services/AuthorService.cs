using Diplom_Bibl.Interfaces;
using System;

namespace Diplom_Bibl.Services
{
    public class AuthorService
    {
        private readonly IAuthorRepository _repository;

        public AuthorService(IAuthorRepository repository)
        {
            _repository = repository;
        }

        public int AddAuthor(string surname, string name, string patronymic)
        {
            if (string.IsNullOrWhiteSpace(surname))
                throw new ArgumentException("Фамилия не может быть пустой");

            name = name ?? "";
            patronymic = patronymic ?? "";

            if (_repository.AuthorExists(surname.Trim(), name.Trim(), patronymic.Trim()))
                throw new InvalidOperationException("Автор уже существует");

            return _repository.InsertAuthor(surname.Trim(), name.Trim(), patronymic.Trim());
        }
    }
}