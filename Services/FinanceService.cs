using Finansim.Models;

namespace Finansim.Services
{
    public class FinanceService
    {
        private readonly StorageService _storage;

        private const string StorageKey = "finance-transactions";

        // Eski tek hesaplı veriler her sayfada aynı kimliklerle eşleşsin.
        private static readonly Guid LegacyBankAccountId = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid LegacyCreditCardId = new("22222222-2222-2222-2222-222222222222");

        private FinanceSettings _settings = new();
        private readonly List<Transaction> _transactions = new();
        private bool _initialized;

        public FinanceService(StorageService storage)
        {
            _storage = storage;
            NormalizeSettings(_settings);
        }

        public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();

        public IReadOnlyList<BankAccount> BankAccounts => _settings.BankAccounts;

        public IReadOnlyList<CreditCard> CreditCards => _settings.CreditCards;

        public static void NormalizeSettings(FinanceSettings settings)
        {
            settings.BankAccounts ??= new List<BankAccount>();
            settings.CreditCards ??= new List<CreditCard>();

            // Eski tek banka hesabını yeni listeye bir kez aktar.
            if (settings.BankAccounts.Count == 0)
            {
                settings.BankAccounts.Add(new BankAccount
                {
                    Id = LegacyBankAccountId,
                    AccountName = "Ana Banka Hesabım",
                    InitialBalance = settings.InitialBankBalance,
                    CurrentBalance = settings.InitialBankBalance,
                    MinimumBalance = settings.MinimumBankBalance
                });
            }

            // Eski tek kredi kartını yeni listeye bir kez aktar.
            if (settings.CreditCards.Count == 0)
            {
                settings.CreditCards.Add(new CreditCard
                {
                    Id = LegacyCreditCardId,
                    CardName = "Ana Kredi Kartım",
                    TotalLimit = settings.CreditCardLimit,
                    InitialDebt = settings.InitialCreditCardDebt,
                    StatementDay = settings.StatementDate?.Day ?? settings.StatementDay,
                    PaymentDay = settings.PaymentDate?.Day ?? settings.PaymentDay,
                    StatementDate = settings.StatementDate ?? LegacyDayToNextDate(settings.StatementDay),
                    PaymentDate = settings.PaymentDate ?? LegacyDayToNextDate(settings.PaymentDay)
                });
            }

            foreach (var account in settings.BankAccounts)
            {
                if (account.Id == Guid.Empty)
                    account.Id = Guid.NewGuid();

                if (string.IsNullOrWhiteSpace(account.AccountName))
                    account.AccountName = "Banka Hesabı";
            }

            foreach (var card in settings.CreditCards)
            {
                if (card.Id == Guid.Empty)
                    card.Id = Guid.NewGuid();

                if (string.IsNullOrWhiteSpace(card.CardName))
                    card.CardName = "Kredi Kartı";

                if (card.StatementDate.HasValue)
                    card.StatementDay = card.StatementDate.Value.Day;

                if (card.PaymentDate.HasValue)
                    card.PaymentDay = card.PaymentDate.Value.Day;
            }

            SyncLegacySummary(settings);
        }

        private static DateTime? LegacyDayToNextDate(int day)
        {
            if (day < 1 || day > 31)
                return null;

            DateTime today = DateTime.Today;
            int currentDay = Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month));
            DateTime candidate = new(today.Year, today.Month, currentDay);

            if (candidate < today)
            {
                DateTime nextMonth = today.AddMonths(1);
                int nextDay = Math.Min(day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                candidate = new DateTime(nextMonth.Year, nextMonth.Month, nextDay);
            }

            return candidate;
        }

        public static void SyncLegacySummary(FinanceSettings settings)
        {
            settings.InitialBankBalance = settings.BankAccounts.Sum(x => x.InitialBalance);
            settings.MinimumBankBalance = settings.BankAccounts.Sum(x => x.MinimumBalance);
            settings.CreditCardLimit = settings.CreditCards.Sum(x => x.TotalLimit);
            settings.InitialCreditCardDebt = settings.CreditCards.Sum(x => x.InitialDebt);

            var firstCard = settings.CreditCards.FirstOrDefault();
            settings.StatementDate = firstCard?.StatementDate;
            settings.PaymentDate = firstCard?.PaymentDate;
            settings.StatementDay = firstCard?.StatementDate?.Day ?? firstCard?.StatementDay ?? 0;
            settings.PaymentDay = firstCard?.PaymentDate?.Day ?? firstCard?.PaymentDay ?? 0;
        }

        public void Configure(FinanceSettings settings)
        {
            NormalizeSettings(settings);
            _settings = settings;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            var savedTransactions =
                await _storage.LoadAsync<List<Transaction>>(StorageKey);

            _transactions.Clear();

            if (savedTransactions != null)
                _transactions.AddRange(savedTransactions);

            bool migrated = AssignLegacyTransactionAccounts();
            _initialized = true;

            if (migrated)
                await SaveAsync();
        }

        // Yedek geri yüklendikten sonra bellekteki işlemleri IndexedDB'den yeniden yükler.
        public async Task ReloadAsync(FinanceSettings settings)
        {
            Configure(settings);
            _initialized = false;
            await InitializeAsync();
        }

        private bool AssignLegacyTransactionAccounts()
        {
            bool changed = false;
            Guid? defaultBankId = _settings.BankAccounts.FirstOrDefault()?.Id;
            Guid? defaultCardId = _settings.CreditCards.FirstOrDefault()?.Id;

            foreach (var transaction in _transactions)
            {
                bool needsBank =
                    transaction.Type == TransactionType.Income ||
                    transaction.Type == TransactionType.CreditCardPayment ||
                    transaction.Type == TransactionType.SavingsContribution ||
                    transaction.Type == TransactionType.SavingsWithdrawal ||
                    (transaction.Type == TransactionType.Expense &&
                     transaction.PaymentMethod == PaymentMethod.BankAccount);

                bool needsCard = transaction.Type == TransactionType.CreditCardPayment
                    || (transaction.Type == TransactionType.Expense &&
                        transaction.PaymentMethod == PaymentMethod.CreditCard);

                if (needsBank && transaction.BankAccountId == null && defaultBankId.HasValue)
                {
                    transaction.BankAccountId = defaultBankId;
                    changed = true;
                }

                if (needsCard && transaction.CreditCardId == null && defaultCardId.HasValue)
                {
                    transaction.CreditCardId = defaultCardId;
                    changed = true;
                }
            }

            return changed;
        }

        private async Task SaveAsync()
        {
            await _storage.SaveAsync(StorageKey, _transactions);
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            await InitializeAsync();
            ValidateTransaction(transaction);
            _transactions.Add(transaction);

            try
            {
                await SaveAsync();
            }
            catch
            {
                _transactions.Remove(transaction);
                throw;
            }
        }

        public async Task UpdateTransactionAsync(Transaction updated)
        {
            await InitializeAsync();
            ValidateTransaction(updated);

            int index = _transactions.FindIndex(x => x.Id == updated.Id);
            if (index < 0)
                throw new InvalidOperationException("İşlem bulunamadı.");

            var previous = _transactions[index];
            _transactions[index] = updated;

            try
            {
                await SaveAsync();
            }
            catch
            {
                _transactions[index] = previous;
                throw;
            }
        }

        public async Task DeleteTransactionAsync(Guid id)
        {
            await InitializeAsync();
            var transaction = _transactions.FirstOrDefault(x => x.Id == id);
            if (transaction == null)
                return;

            _transactions.Remove(transaction);

            try
            {
                await SaveAsync();
            }
            catch
            {
                _transactions.Add(transaction);
                throw;
            }
        }

        private static void ValidateTransaction(Transaction transaction)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("İşlem tutarı sıfırdan büyük olmalıdır.");

            if (string.IsNullOrWhiteSpace(transaction.Description))
                throw new ArgumentException("İşlem açıklaması boş bırakılamaz.");
        }

        public decimal GetBankBalance()
        {
            return _settings.BankAccounts.Sum(account => GetBankBalance(account.Id));
        }

        public decimal GetBankBalance(Guid accountId)
        {
            var account = _settings.BankAccounts.FirstOrDefault(x => x.Id == accountId);
            if (account == null)
                return 0;

            decimal balance = account.InitialBalance;

            foreach (var transaction in _transactions.Where(x => x.BankAccountId == accountId))
            {
                switch (transaction.Type)
                {
                    case TransactionType.Income:
                    case TransactionType.SavingsWithdrawal:
                        balance += transaction.Amount;
                        break;

                    case TransactionType.Expense when transaction.PaymentMethod == PaymentMethod.BankAccount:
                    case TransactionType.CreditCardPayment:
                    case TransactionType.SavingsContribution:
                        balance -= transaction.Amount;
                        break;
                }
            }

            account.CurrentBalance = balance;
            return balance;
        }

        public decimal GetCreditCardDebt()
        {
            return _settings.CreditCards.Sum(card => GetCreditCardDebt(card.Id));
        }

        public decimal GetCreditCardDebt(Guid cardId)
        {
            var card = _settings.CreditCards.FirstOrDefault(x => x.Id == cardId);
            if (card == null)
                return 0;

            decimal debt = card.InitialDebt;

            foreach (var transaction in _transactions.Where(x => x.CreditCardId == cardId))
            {
                if (transaction.Type == TransactionType.Expense &&
                    transaction.PaymentMethod == PaymentMethod.CreditCard)
                {
                    debt += transaction.Amount;
                }
                else if (transaction.Type == TransactionType.CreditCardPayment)
                {
                    debt -= transaction.Amount;
                }
            }

            card.CurrentDebt = debt;
            return debt;
        }

        public decimal GetAvailableLimit()
        {
            return _settings.CreditCards.Sum(x => x.TotalLimit) - GetCreditCardDebt();
        }

        public decimal GetAvailableLimit(Guid cardId)
        {
            var card = _settings.CreditCards.FirstOrDefault(x => x.Id == cardId);
            return card == null ? 0 : card.TotalLimit - GetCreditCardDebt(cardId);
        }

        public decimal GetTotalCreditCardLimit()
        {
            return _settings.CreditCards.Sum(x => x.TotalLimit);
        }

        public decimal GetSavings()
        {
            decimal savings = _settings.CurrentSavings;

            foreach (var transaction in _transactions)
            {
                if (transaction.Type == TransactionType.SavingsContribution)
                    savings += transaction.Amount;

                if (transaction.Type == TransactionType.SavingsWithdrawal)
                    savings -= transaction.Amount;
            }

            return savings;
        }

        public decimal GetDailyExpenses(DateTime date)
        {
            return _transactions
                .Where(x => x.Type == TransactionType.Expense && x.Date.Date == date.Date)
                .Sum(x => x.Amount);
        }

        public decimal GetMonthlyExpenses(DateTime date)
        {
            return _transactions
                .Where(x => x.Type == TransactionType.Expense &&
                            x.Date.Month == date.Month && x.Date.Year == date.Year)
                .Sum(x => x.Amount);
        }

        public decimal GetMonthlyIncome(DateTime date)
        {
            return _transactions
                .Where(x => x.Type == TransactionType.Income &&
                            x.Date.Month == date.Month && x.Date.Year == date.Year)
                .Sum(x => x.Amount);
        }

        public decimal GetSavingsTarget() => _settings.SavingsTarget;

        public decimal GetMinimumBankBalance()
        {
            return _settings.BankAccounts.Sum(x => x.MinimumBalance);
        }

        public string GetBankAccountName(Guid? accountId)
        {
            return _settings.BankAccounts.FirstOrDefault(x => x.Id == accountId)?.AccountName
                ?? "Banka hesabı";
        }

        public string GetCreditCardName(Guid? cardId)
        {
            return _settings.CreditCards.FirstOrDefault(x => x.Id == cardId)?.CardName
                ?? "Kredi kartı";
        }

        public DateTime? GetNextPaymentDate()
        {
            DateTime today = DateTime.Today;
            var nextDates = new List<DateTime>();

            foreach (var card in _settings.CreditCards)
            {
                int day = card.PaymentDate?.Day ?? card.PaymentDay;
                if (day < 1 || day > 31)
                    continue;

                int thisMonthDay = Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month));
                DateTime candidate = new(today.Year, today.Month, thisMonthDay);

                if (candidate < today)
                {
                    DateTime nextMonth = today.AddMonths(1);
                    int nextDay = Math.Min(day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    candidate = new DateTime(nextMonth.Year, nextMonth.Month, nextDay);
                }

                nextDates.Add(candidate);
            }

            return nextDates.Count == 0 ? null : nextDates.Min();
        }
    }
}
