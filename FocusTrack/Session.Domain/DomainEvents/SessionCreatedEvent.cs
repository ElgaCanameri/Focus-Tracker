namespace Session.Domain.DomainEvents
{
    public sealed record SessionCreatedEvent(
    Guid Id,
    Guid SessionId,
    string UserId,
    decimal DurationMin,
    DateTime OccurredOn) : IDomainEvent;
}
