using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplom_Bibl
{
    public class InfoRecord
    {
        public int IdRecord { get; set; }
        public DateTime DateRecord { get; set; }
        public int DeliveryBook { get; set; }
        public int DeliveryCertificate { get; set; }
        public int EventsCount { get; set; }

        // Пустой конструктор для инициализатора
        public InfoRecord() { }

        // Конструктор с параметрами (опционально)
        public InfoRecord(int id, DateTime date, int books, int refs, int events)
        {
            IdRecord = id;
            DateRecord = date;
            DeliveryBook = books;
            DeliveryCertificate = refs;
            EventsCount = events;
        }
    }
}
