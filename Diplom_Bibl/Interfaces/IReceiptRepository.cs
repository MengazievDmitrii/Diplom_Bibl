using System;
using System.Collections.Generic;

namespace Diplom_Bibl.Interfaces
{
    public interface IReceiptRepository
    {
        int InsertReceipt(string number, DateTime date, int responsibleId, string comment);
        void UpdateReceipt(int id, int responsibleId, string comment);
        void DeleteReceipt(int id);
        IEnumerable<dynamic> GetAllReceipts();
        dynamic GetReceiptById(int id);
        int GetNextReceiptNumber();
    }
}