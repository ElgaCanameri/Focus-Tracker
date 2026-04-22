namespace Contracts.Events
{
    public record SessionSharedEvent(
     Guid SessionId,
     string OwnerUserId,
     List<string> RecipientUserIds,
     DateTime OccurredOn);
}
