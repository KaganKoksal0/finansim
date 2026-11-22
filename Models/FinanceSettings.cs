namespace Finansim.Models
{
    public class FinanceSettings
    {
        // Yeni çoklu hesap yapısı.
        public List<BankAccount> BankAccounts { get; set; } = new();

        public List<CreditCard> CreditCards { get; set; } = new();

        // Birikim bilgileri hesaplardan bağımsız toplam olarak tutulur.
        public decimal SavingsTarget { get; set; }

        public decimal CurrentSavings { get; set; }

        // Eski tek hesaplı sürümden veri kaybetmeden geçiş için saklanan alanlar.
        // Yeni hesaplama sistemi bu alanları değil, BankAccounts/CreditCards listelerini kullanır.
        public decimal InitialBankBalance { get; set; }

        public decimal CreditCardLimit { get; set; }

        public decimal InitialCreditCardDebt { get; set; }

        public decimal MinimumBankBalance { get; set; }

        public int StatementDay { get; set; }

        public int PaymentDay { get; set; }

        public DateTime? StatementDate { get; set; }

        public DateTime? PaymentDate { get; set; }
    }
}
