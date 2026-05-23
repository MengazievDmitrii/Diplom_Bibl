namespace Diplom_Bibl.Services
{
    public class ReceiptNumberGenerator
    {
        public string GenerateReceiptNumber(int nextId)
        {
            return $"ПН-{nextId:D4}";
        }
    }
}