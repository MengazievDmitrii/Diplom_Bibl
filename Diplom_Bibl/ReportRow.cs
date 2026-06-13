using System;

namespace Diplom_Bibl
{
    // Добавьте этот класс в файл ReportViewWindow.xaml.cs или в отдельный файл
    public class ReportRow
    {
        public int IdRecord { get; set; }
        // Используем Nullable DateTime
        public DateTime? DateRecord { get; set; } // Теперь может быть null
        public int DeliveryBook { get; set; }
        public int DeliveryCertificate { get; set; }
        public int EventsCount { get; set; }
        public bool IsSummary { get; set; } = false;

        // Конструктор для обычной записи
        public ReportRow(int id, DateTime date, int books, int refs, int events)
        {
            IdRecord = id;
            DateRecord = date; // Присваиваем как есть
            DeliveryBook = books;
            DeliveryCertificate = refs;
            EventsCount = events;
            IsSummary = false;
        }

        // Конструктор для строки итога
        public ReportRow(int totalBooks, int totalRefs, int totalEvents)
        {
            IdRecord = -1;
            DateRecord = null; // Явно присваиваем null
            DeliveryBook = totalBooks;
            DeliveryCertificate = totalRefs;
            EventsCount = totalEvents;
            IsSummary = true;
        }
    }
}
