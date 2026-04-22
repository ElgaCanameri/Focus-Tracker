namespace Contracts.Events
{
    public record UserStatusChangedEvent(
      string UserId,
      string OldStatus,
      string NewStatus,
      DateTime OccurredOn);
}
