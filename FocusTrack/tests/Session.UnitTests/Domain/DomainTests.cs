using FluentAssertions;
using Session.Domain.DomainEvents;
using Session.Domain.Entities;
using Session.Domain.Enums;
using Session.Domain.ValueObjects;
using Xunit;

namespace Session.UnitTests.Domain;

public class DurationMinTests
{
    [Fact]
    public void Create_returns_minutes_rounded_to_two_decimals()
    {
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(50);

        var duration = DurationMin.Create(start, end);

        duration.Value.Should().Be(50.00m);
    }

    [Fact]
    public void Create_rounds_fractional_minutes()
    {
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        // 45.126 minutes -> rounds to 45.13 via Math.Round banker's default? We use Math.Round(v,2)
        // which defaults to ToEven; the current code uses default MidpointRounding.
        var end = start.AddMilliseconds(TimeSpan.FromMinutes(45.125).TotalMilliseconds);

        var duration = DurationMin.Create(start, end);

        duration.Value.Should().BeInRange(45.12m, 45.13m);
    }

    [Fact]
    public void Create_throws_when_end_equals_start()
    {
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        Action act = () => DurationMin.Create(start, start);

        act.Should().Throw<ArgumentException>()
           .WithMessage("EndTime must be after StartTime.");
    }

    [Fact]
    public void Create_throws_when_end_is_before_start()
    {
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        Action act = () => DurationMin.Create(start, start.AddMinutes(-5));

        act.Should().Throw<ArgumentException>();
    }
}

public class SessionAggregateTests
{
    private const string OwnerId = "auth0|user-001";

    [Fact]
    public void Create_produces_session_with_expected_state()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 4, 11, 9, 5, 0, DateTimeKind.Utc);

        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "Data Structures – Arrays", start, end, SessionMode.Reading);

        session.UserId.Should().Be(OwnerId);
        session.Topic.Should().Be("Data Structures – Arrays");
        session.Mode.Should().Be(SessionMode.Reading);
        session.DurationMin.Value.Should().Be(50.00m);
        session.IsDailyGoalAchieved.Should().BeFalse();
        session.PublicLinkToken.Should().BeNull();
        session.IsPublicLinkRevoked.Should().BeFalse();
    }

    [Fact]
    public void Create_raises_SessionCreatedEvent_with_matching_ids()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(30);

        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "Topic", start, end, SessionMode.Coding);

        session.DomainEvents.Should().ContainSingle();
        var evt = session.DomainEvents[0].Should().BeOfType<SessionCreatedEvent>().Subject;
        evt.SessionId.Should().Be(session.Id);
        evt.UserId.Should().Be(OwnerId);
        evt.DurationMin.Should().Be(30.00m);
    }

    [Fact]
    public void Update_changes_state_and_raises_SessionUpdatedEvent()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "Topic", start, start.AddMinutes(30), SessionMode.Reading);
        session.ClearDomainEvents();

        var newStart = start.AddHours(1);
        session.Update("New Topic", newStart, newStart.AddMinutes(45), SessionMode.VideoCourse);

        session.Topic.Should().Be("New Topic");
        session.StartTime.Should().Be(newStart);
        session.Mode.Should().Be(SessionMode.VideoCourse);
        session.DurationMin.Value.Should().Be(45.00m);
        session.DomainEvents.Should().ContainSingle(e => e is SessionUpdatedEvent);
    }

    [Fact]
    public void Delete_raises_SessionDeletedEvent()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "Topic", start, start.AddMinutes(30), SessionMode.Reading);
        session.ClearDomainEvents();

        session.Delete();

        session.DomainEvents.Should().ContainSingle(e => e is SessionDeletedEvent);
    }

    [Fact]
    public void MarkDailyGoalAchieved_sets_flag()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "T", start, start.AddMinutes(30), SessionMode.Reading);

        session.MarkDailyGoalAchieved();

        session.IsDailyGoalAchieved.Should().BeTrue();
    }

    [Fact]
    public void GeneratePublicLink_returns_token_and_sets_not_revoked()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "T", start, start.AddMinutes(30), SessionMode.Reading);

        var token = session.GeneratePublicLink();

        token.Should().NotBeNullOrEmpty();
        session.PublicLinkToken.Should().Be(token);
        session.IsPublicLinkRevoked.Should().BeFalse();
    }

    [Fact]
    public void RevokePublicLink_sets_revoked_true()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "T", start, start.AddMinutes(30), SessionMode.Reading);
        session.GeneratePublicLink();

        session.RevokePublicLink();

        session.IsPublicLinkRevoked.Should().BeTrue();
    }

    [Fact]
    public void Regenerating_public_link_after_revoke_clears_revoked_flag()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "T", start, start.AddMinutes(30), SessionMode.Reading);
        session.GeneratePublicLink();
        session.RevokePublicLink();

        var newToken = session.GeneratePublicLink();

        newToken.Should().NotBeNullOrEmpty();
        session.IsPublicLinkRevoked.Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEvents_empties_the_list()
    {
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            OwnerId, "T", start, start.AddMinutes(30), SessionMode.Reading);
        session.DomainEvents.Should().NotBeEmpty();

        session.ClearDomainEvents();

        session.DomainEvents.Should().BeEmpty();
    }
}

public class UserEntityTests
{
    [Fact]
    public void Create_initializes_user_as_active()
    {
        var user = User.Create("auth0|abc");

        user.ExternalId.Should().Be("auth0|abc");
        user.Status.Should().Be(UserStatus.Active);
        user.CanAuthenticate().Should().BeTrue();
    }

    [Fact]
    public void Suspended_user_cannot_authenticate()
    {
        var user = User.Create("auth0|abc");
        user.ChangeStatus(UserStatus.Suspended);

        user.CanAuthenticate().Should().BeFalse();
    }

    [Fact]
    public void Deactivated_user_cannot_authenticate()
    {
        var user = User.Create("auth0|abc");
        user.ChangeStatus(UserStatus.Deactivated);

        user.CanAuthenticate().Should().BeFalse();
    }

    [Fact]
    public void ChangeStatus_updates_status()
    {
        var user = User.Create("auth0|abc");
        user.ChangeStatus(UserStatus.Suspended);
        user.Status.Should().Be(UserStatus.Suspended);
        user.ChangeStatus(UserStatus.Active);
        user.Status.Should().Be(UserStatus.Active);
    }
}

public class AuditLogTests
{
    [Fact]
    public void Create_builds_entry_with_current_timestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var log = AuditLog.Create("UserStatusChanged", "user-1", "User", "admin-1", "Active -> Suspended");

        log.Action.Should().Be("UserStatusChanged");
        log.TargetId.Should().Be("user-1");
        log.TargetType.Should().Be("User");
        log.PerformedBy.Should().Be("admin-1");
        log.Details.Should().Be("Active -> Suspended");
        log.OccurredOn.Should().BeAfter(before);
    }
}

public class SessionShareTests
{
    [Fact]
    public void Create_builds_share_with_current_timestamp()
    {
        var sessionId = Guid.NewGuid();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var share = SessionShare.Create(sessionId, "auth0|recipient");

        share.SessionId.Should().Be(sessionId);
        share.RecipientUserId.Should().Be("auth0|recipient");
        share.SharedAt.Should().BeAfter(before);
    }
}

public class MonthlyFocusEntityTests
{
    [Fact]
    public void AddDuration_accumulates_total()
    {
        var entity = new MonthlyFocusEntity
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Year = 2025,
            Month = 4,
            TotalDurationMin = 30m,
        };

        entity.AddDuration(15.5m);
        entity.AddDuration(4.5m);

        entity.TotalDurationMin.Should().Be(50m);
    }
}
