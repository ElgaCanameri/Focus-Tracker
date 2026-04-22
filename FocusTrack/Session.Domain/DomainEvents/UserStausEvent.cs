using Session.Domain.Enums;

namespace Session.Domain.DomainEvents
{
    public sealed record UserStausEvent(
    Guid Id,
    Guid UserId,
    UserStatus OldStatus,
    UserStatus NewStatus,
    DateTime OccurredOn) : IDomainEvent;
}
