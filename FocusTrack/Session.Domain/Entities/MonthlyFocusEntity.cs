namespace Session.Domain.Entities
{
    public class MonthlyFocusEntity
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = default!;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalDurationMin { get; set; }

        public void AddDuration(decimal duration)
        {
            TotalDurationMin += duration;
        }
    }
}
