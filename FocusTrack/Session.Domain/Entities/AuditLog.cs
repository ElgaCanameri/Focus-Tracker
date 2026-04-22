namespace Session.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Action { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string PerformedBy { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string action,
        string targetId,
        string targetType,
        string performedBy,
        string details)
    {
        return new AuditLog
        {
            Action = action,
            TargetId = targetId,
            TargetType = targetType,
            PerformedBy = performedBy,
            Details = details,
            OccurredOn = DateTime.UtcNow
        };
    }
}