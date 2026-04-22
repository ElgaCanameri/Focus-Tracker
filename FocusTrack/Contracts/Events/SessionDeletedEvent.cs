namespace Contracts.Events
{
    public record SessionDeletedEvent(
     Guid SessionId,
     string UserId,
     DateTime OccurredOn);
}
