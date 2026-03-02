using Diplom_Bibl.Interfaces;
using System;
using System.Collections.Generic;

namespace Diplom_Bibl.Services
{
    public class UserService
    {
        private readonly IUserRepository _repository;

        public UserService(IUserRepository repository)
        {
            _repository = repository;
        }

        public int AddUser(string surname, string name, string patronymic,
                          string login, string password, List<int> roleIds)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(surname))
                throw new ArgumentException("Фамилия не может быть пустой");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя не может быть пустым");

            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Логин не может быть пустым");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым");

            if (roleIds == null || roleIds.Count == 0)
                throw new ArgumentException("Должна быть выбрана хотя бы одна роль");

            // Проверка уникальности логина - ВАЖНО!
            if (_repository.LoginExists(login.Trim()))
                throw new InvalidOperationException("Логин уже занят");

            // Создание пользователя
            int userId = _repository.InsertUser(
                surname.Trim(),
                name.Trim(),
                patronymic?.Trim() ?? "",
                login.Trim(),
                password);

            // Назначение ролей - ВАЖНО!
            if (userId > 0 && roleIds.Count > 0)
            {
                _repository.AssignRolesToUser(userId, roleIds);
            }

            return userId;
        }
    }
}