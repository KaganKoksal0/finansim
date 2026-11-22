namespace Finansim.Models
{
    public class BankAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string AccountName { get; set; } = "Ana Banka Hesabım";

        // Hesap eklenirken girilen başlangıç bakiyesi.
        public decimal InitialBalance { get; set; }

        // Eski sürümlerle uyumluluk için korunur. Güncel bakiye işlemlerden hesaplanır.
        public decimal CurrentBalance { get; set; }

        public decimal MinimumBalance { get; set; }
    }
}
