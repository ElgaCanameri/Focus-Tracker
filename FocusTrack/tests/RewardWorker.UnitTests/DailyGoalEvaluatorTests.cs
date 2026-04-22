using Contracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RewardWorker;
using RewardWorker.Consumers;
using RewardWorker.Models;
using RewardWorker.Services;
using Xunit;

namespace RewardWorker.UnitTests;

/// <summary>
/// The brief mandates unit tests for total-minutes-in-a-day values of 119.99, 120.00, 120.01.
///
/// IMPORTANT: Your current implementation uses <c>runningTotal >= 120.00m</c>, which means a
/// total of exactly 120.00 DOES award a badge. The brief says "first exceeds 120 minutes",
/// which would imply strictly greater than. These tests document what the code ACTUALLY does —
/// if you change the rule to strict greater-than, flip the 120.00 assertion accordingly.
/// </summary>
public class DailyGoalEvaluatorBoundaryTests
{
    private static RewardDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RewardDbContext>()
            .UseInMemoryDatabase("reward-" + Guid.NewGuid())
            .Options);

    private static SessionRecord Session(string userId, DateTime start, decimal minutes) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        DurationMin = new DurationMinRecord { Value = minutes },
        IsDailyGoalAchieved = false,
    };

    [Fact]
    public async Task Total_11999_does_not_award_badge()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(Session(user, day.AddHours(8),  60.00m));
        db.Sessions.Add(Session(user, day.AddHours(14), 59.99m));
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeFalse();
        triggering.Should().BeNull();
    }

    [Fact]
    public async Task Total_12000_awards_badge_per_current_implementation()
    {
        // Current code: runningTotal >= 120.00m ⇒ 120.00 triggers the badge.
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(Session(user, day.AddHours(8),  60.00m));
        var second = Session(user, day.AddHours(14), 60.00m);
        db.Sessions.Add(second);
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeTrue();
        triggering.Should().Be(second.Id);
    }

    [Fact]
    public async Task Total_12001_awards_badge()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(Session(user, day.AddHours(8),  60.00m));
        var trigger = Session(user, day.AddHours(14), 60.01m);
        db.Sessions.Add(trigger);
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeTrue();
        triggering.Should().Be(trigger.Id);
    }

    [Fact]
    public async Task Single_large_session_triggers_on_its_own()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        var big = Session(user, day.AddHours(8), 180m);
        db.Sessions.Add(big);
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeTrue();
        triggering.Should().Be(big.Id);
    }

    [Fact]
    public async Task Triggering_session_is_the_one_that_crosses_the_threshold()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(Session(user, day.AddHours(8),  30m));
        db.Sessions.Add(Session(user, day.AddHours(9),  30m));
        var trigger = Session(user, day.AddHours(10), 65m); // running total: 30, 60, 125
        db.Sessions.Add(trigger);
        db.Sessions.Add(Session(user, day.AddHours(11), 20m)); // past threshold but not triggering
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeTrue();
        triggering.Should().Be(trigger.Id);
    }

    [Fact]
    public async Task Already_achieved_day_returns_false()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        var already = Session(user, day.AddHours(8), 200m);
        already.IsDailyGoalAchieved = true;
        db.Sessions.Add(already);
        await db.SaveChangesAsync();

        var (reached, triggering) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeFalse();
        triggering.Should().BeNull();
    }

    [Fact]
    public async Task Sessions_from_other_days_are_ignored()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);
        var yesterday = day.AddDays(-1);

        db.Sessions.Add(Session(user, yesterday.AddHours(8), 300m));         // yesterday — huge, but different day
        db.Sessions.Add(Session(user, day.AddHours(8), 60m));                // today — not enough on its own
        await db.SaveChangesAsync();

        var (reached, _) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeFalse();
    }

    [Fact]
    public async Task Sessions_belonging_to_other_users_are_ignored()
    {
        await using var db = NewContext();
        var user = "auth0|u1";
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(Session("auth0|other", day.AddHours(8), 300m));
        db.Sessions.Add(Session(user, day.AddHours(9), 50m));
        await db.SaveChangesAsync();

        var (reached, _) = await new DailyGoalEvaluator(db).EvaluateAsync(user, day);

        reached.Should().BeFalse();
    }

    [Fact]
    public async Task No_sessions_for_day_returns_false()
    {
        await using var db = NewContext();
        var (reached, triggering) = await new DailyGoalEvaluator(db)
            .EvaluateAsync("u", new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc));

        reached.Should().BeFalse();
        triggering.Should().BeNull();
    }
}

public class SessionCreatedConsumerTests
{
    private static RewardDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RewardDbContext>()
            .UseInMemoryDatabase("reward-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task Does_nothing_when_goal_not_reached()
    {
        await using var db = NewContext();
        db.Sessions.Add(new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            StartTime = new DateTime(2025, 4, 11, 8, 0, 0, DateTimeKind.Utc),
            DurationMin = new DurationMinRecord { Value = 30m },
        });
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var consumer = new SessionCreatedConsumer(
            new DailyGoalEvaluator(db), db, publish.Object, NullLogger<SessionCreatedConsumer>.Instance);

        var ctx = CreateContext(new SessionCreatedEvent(
            Guid.NewGuid(), "u", 30m, new DateTime(2025, 4, 11, 8, 0, 0, DateTimeKind.Utc)));

        await consumer.Consume(ctx.Object);

        publish.Verify(p => p.Publish(It.IsAny<DailyGoalAchievedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Marks_triggering_session_and_publishes_when_goal_reached()
    {
        await using var db = NewContext();
        var day = new DateTime(2025, 4, 11, 0, 0, 0, DateTimeKind.Utc);

        db.Sessions.Add(new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            StartTime = day.AddHours(8),
            DurationMin = new DurationMinRecord { Value = 60m },
        });
        var trigger = new SessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            StartTime = day.AddHours(10),
            DurationMin = new DurationMinRecord { Value = 61m },
        };
        db.Sessions.Add(trigger);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var consumer = new SessionCreatedConsumer(
            new DailyGoalEvaluator(db), db, publish.Object, NullLogger<SessionCreatedConsumer>.Instance);

        var ctx = CreateContext(new SessionCreatedEvent(trigger.Id, "u", 61m, day));
        await consumer.Consume(ctx.Object);

        var refreshed = await db.Sessions.FirstAsync(s => s.Id == trigger.Id);
        refreshed.IsDailyGoalAchieved.Should().BeTrue();
        publish.Verify(p => p.Publish(
            It.Is<DailyGoalAchievedEvent>(e => e.UserId == "u"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<ConsumeContext<SessionCreatedEvent>> CreateContext(SessionCreatedEvent evt)
    {
        var ctx = new Mock<ConsumeContext<SessionCreatedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }
}
