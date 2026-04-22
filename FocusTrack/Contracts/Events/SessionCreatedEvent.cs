namespace Contracts.Events
{
    public record SessionCreatedEvent(
      Guid SessionId,
      string UserId,
      decimal DurationMin,
      DateTime OccurredOn);
}
