namespace Session.Domain.DomainEvents
{
    public sealed record SessionDeletedEvent(
     Guid Id,
     Guid SessionId,
     string UserId,
     DateTime OccurredOn) : IDomainEvent;
}
