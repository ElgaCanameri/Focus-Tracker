namespace Contracts.Events
{
    public record DailyGoalAchievedEvent(
      Guid SessionId,
      string UserId,
      decimal TotalDurationMin,
      DateTime OccurredOn);
}
