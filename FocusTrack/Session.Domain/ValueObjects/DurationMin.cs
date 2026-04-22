namespace Session.Domain.ValueObjects;

public sealed class DurationMin
{
    public decimal Value { get; }

    private DurationMin(decimal value) => Value = value;
    private DurationMin() { } 

    public static DurationMin Create(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ArgumentException("EndTime must be after StartTime.");

        var minutes = (decimal)(end - start).TotalMinutes;
        return new DurationMin(Math.Round(minutes, 2));
    }
}