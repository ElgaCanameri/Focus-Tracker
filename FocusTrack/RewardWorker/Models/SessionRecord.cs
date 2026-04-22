namespace RewardWorker.Models
{
    public class SessionRecord
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DurationMinRecord DurationMin { get; set; } = null!;
        public bool IsDailyGoalAchieved { get; set; }
    }

    public class DurationMinRecord
    {
        public decimal Value { get; set; }
    }
}
