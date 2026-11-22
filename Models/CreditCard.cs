namespace Finansim.Models
{
    public class CreditCard
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string CardName { get; set; } = "Ana Kredi Kartım";

        public decimal TotalLimit { get; set; }

        // Kart eklenirken girilen başlangıç borcu.
        public decimal InitialDebt { get; set; }

        // Eski sürümlerle uyumluluk için korunur. Güncel borç işlemlerden hesaplanır.
        public decimal CurrentDebt { get; set; }

        public decimal AvailableLimit => TotalLimit - CurrentDebt;

        public int StatementDay { get; set; }

        public int PaymentDay { get; set; }

        public DateTime? StatementDate { get; set; }

        public DateTime? PaymentDate { get; set; }
    }
}
