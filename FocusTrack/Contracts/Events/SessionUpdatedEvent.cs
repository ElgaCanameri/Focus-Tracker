namespace Contracts.Events
{
    public record SessionUpdatedEvent(
    Guid SessionId,
    string UserId,
    decimal DurationMin,
    DateTime OccurredOn);
}
