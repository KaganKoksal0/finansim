namespace Finansim.Models
{
    public class SavingsGoal
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = "Genel Birikim";

        public decimal TargetAmount { get; set; }

        public decimal CurrentAmount { get; set; }

        public DateTime? TargetDate { get; set; }

        public decimal ProgressPercentage =>
            TargetAmount > 0
                ? Math.Min(100, CurrentAmount / TargetAmount * 100)
                : 0;
    }
}