using Session.Domain.Common;
using Session.Domain.DomainEvents;
using Session.Domain.Enums;
using Session.Domain.ValueObjects;

namespace Session.Domain.Entities;

public class Session : AggregateRoot
{
    public string Topic { get; private set; } = string.Empty;
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public SessionMode Mode { get; private set; }
    public DurationMin DurationMin { get; private set; } = null!;
    public string UserId { get; private set; }
    public bool IsDailyGoalAchieved { get; private set; }
    public string? PublicLinkToken { get; private set; }
    public bool IsPublicLinkRevoked { get; private set; }

    private Session() { } 
    public static Session Create(
        string userId,
        string topic,
        DateTime startTime,
        DateTime endTime,
        SessionMode mode)
    {
        var session = new Session
        {
            UserId = userId,
            Topic = topic,
            StartTime = startTime,
            EndTime = endTime,
            Mode = mode,
            DurationMin = DurationMin.Create(startTime, endTime)
        };

        //event raised here to create a session
        session.RaiseDomainEvent(new SessionCreatedEvent(
            Guid.NewGuid(),
            session.Id,
            userId,
            session.DurationMin.Value,
            DateTime.UtcNow));

        return session;
    }

    public void Update(
        string topic,
        DateTime startTime,
        DateTime endTime,
        SessionMode mode)
    {
        Topic = topic;
        StartTime = startTime;
        EndTime = endTime;
        Mode = mode;
        DurationMin = DurationMin.Create(startTime, endTime);

        //event raised here to update a session
        RaiseDomainEvent(new SessionUpdatedEvent(
            Guid.NewGuid(), Id, UserId, DurationMin.Value, DateTime.UtcNow));
    }

    public void MarkDailyGoalAchieved()
    {
        IsDailyGoalAchieved = true;
    }

    public string GeneratePublicLink()
    {
        PublicLinkToken = Guid.NewGuid().ToString("N");
        IsPublicLinkRevoked = false;
        return PublicLinkToken;
    }

    public void RevokePublicLink()
    {
        IsPublicLinkRevoked = true;
    }

    public void Delete()
    {
        //event raised here to delete a session
        RaiseDomainEvent(new SessionDeletedEvent(
            Guid.NewGuid(), Id, UserId, DateTime.UtcNow));
    }
}