using Diplom_Bibl.Interfaces;
using System;

namespace Diplom_Bibl.Services
{
    public class ReceiptService
    {
        private readonly IReceiptRepository _repository;
        private readonly ReceiptNumberGenerator _generator;

        public ReceiptService(IReceiptRepository repository)
        {
            _repository = repository;
            _generator = new ReceiptNumberGenerator();
        }

        // Для тестирования с возможностью подмены генератора
        public ReceiptService(IReceiptRepository repository, ReceiptNumberGenerator generator)
        {
            _repository = repository;
            _generator = generator;
        }

        public int AddReceipt(int responsibleId, string comment)
        {
            // Валидация
            if (responsibleId <= 0)
                throw new ArgumentException("Неверный ID ответственного лица");

            // Получаем следующий номер
            int nextId = _repository.GetNextReceiptNumber();
            string number = _generator.GenerateReceiptNumber(nextId);

            // Сохраняем накладную
            return _repository.InsertReceipt(
                number,
                DateTime.Now,
                responsibleId,
                comment ?? ""); // null comment -> пустая строка
        }
    }
}