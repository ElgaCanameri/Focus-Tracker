namespace Session.Domain.DomainEvents
{
    public sealed record SessionUpdatedEvent(
        Guid Id,
        Guid SessionId,
        string UserId,
        decimal DurationMin,
        DateTime OccurredOn) : IDomainEvent;
}
