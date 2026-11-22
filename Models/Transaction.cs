namespace Finansim.Models
{
    public enum TransactionType
    {
        Income,
        Expense,
        CreditCardPayment,
        SavingsContribution,
        SavingsWithdrawal
    }

    public enum PaymentMethod
    {
        BankAccount,
        CreditCard
    }

    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Description { get; set; } = "";

        public decimal Amount { get; set; }

        public string Category { get; set; } = "Diğer";

        public TransactionType Type { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        // Gelirin/giderin/ödemenin bağlı olduğu banka hesabı.
        public Guid? BankAccountId { get; set; }

        // Kart harcaması veya kredi kartı ödemesinde hedef kart.
        public Guid? CreditCardId { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;
    }
}
