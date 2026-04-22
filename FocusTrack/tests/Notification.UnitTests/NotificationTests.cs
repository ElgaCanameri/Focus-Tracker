using Contracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Notification.Domain.Interfaces;
using Notification.Infrastructure.Services;
using Notification.Presentation.Consumers;
using Xunit;

namespace Notification.UnitTests;

public class NotificationEntityTests
{
    [Fact]
    public void Create_initializes_as_offline()
    {
        var n = Notification.Domain.Entities.Notification.Create("u", "u@example.com");

        n.UserId.Should().Be("u");
        n.Email.Should().Be("u@example.com");
        n.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void SetOnline_and_SetOffline_toggle_state()
    {
        var n = Notification.Domain.Entities.Notification.Create("u", "u@example.com");

        n.SetOnline();
        n.IsOnline.Should().BeTrue();

        n.SetOffline();
        n.IsOnline.Should().BeFalse();
    }
}

public class OnlineTrackerTests
{
    // OnlineTracker uses a static dictionary; every test touches a unique user
    // id so they don't interfere with each other.

    [Fact]
    public async Task Is_offline_by_default()
    {
        var tracker = new OnlineTracker();
        (await tracker.IsOnlineAsync("user-" + Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Becomes_online_after_connect()
    {
        var tracker = new OnlineTracker();
        var userId = "user-" + Guid.NewGuid();

        OnlineTracker.UserConnected(userId);

        (await tracker.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnects_only_when_all_connections_close()
    {
        var tracker = new OnlineTracker();
        var userId = "user-" + Guid.NewGuid();

        OnlineTracker.UserConnected(userId);
        OnlineTracker.UserConnected(userId);    // second tab
        OnlineTracker.UserDisconnected(userId); // close one

        (await tracker.IsOnlineAsync(userId)).Should().BeTrue();

        OnlineTracker.UserDisconnected(userId); // close the last one
        (await tracker.IsOnlineAsync(userId)).Should().BeFalse();
    }
}

public class SessionSharedConsumerTests
{
    [Fact]
    public async Task Sends_realtime_to_online_recipient_and_email_to_offline()
    {
        var notif = new Mock<INotificationService>();
        var repo = new Mock<INotificationRepository>();

        var online = Notification.Domain.Entities.Notification.Create("auth0|online", "on@example.com");
        online.SetOnline();
        var offline = Notification.Domain.Entities.Notification.Create("auth0|offline", "off@example.com");

        repo.Setup(r => r.GetByUserIdAsync("auth0|owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Notification.Domain.Entities.Notification.Create("auth0|owner", "owner@example.com"));
        repo.Setup(r => r.GetByUserIdAsync("auth0|online", It.IsAny<CancellationToken>()))
            .ReturnsAsync(online);
        repo.Setup(r => r.GetByUserIdAsync("auth0|offline", It.IsAny<CancellationToken>()))
            .ReturnsAsync(offline);

        var consumer = new SessionSharedConsumer(notif.Object, repo.Object, NullLogger<SessionSharedConsumer>.Instance);

        var evt = new SessionSharedEvent(
            Guid.NewGuid(),
            "auth0|owner",
            new List<string> { "auth0|online", "auth0|offline" },
            DateTime.UtcNow);

        await consumer.Consume(CreateContext(evt).Object);

        notif.Verify(n => n.SendRealtimeAsync("auth0|online", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notif.Verify(n => n.SendEmailAsync("off@example.com", "owner@example.com",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notif.Verify(n => n.SendRealtimeAsync("auth0|offline", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Skips_recipients_without_preference_record()
    {
        var notif = new Mock<INotificationService>();
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification.Domain.Entities.Notification?)null);

        var consumer = new SessionSharedConsumer(notif.Object, repo.Object, NullLogger<SessionSharedConsumer>.Instance);

        await consumer.Consume(CreateContext(new SessionSharedEvent(
            Guid.NewGuid(), "auth0|owner", new List<string> { "auth0|missing" }, DateTime.UtcNow)).Object);

        notif.VerifyNoOtherCalls();
    }

    private static Mock<ConsumeContext<SessionSharedEvent>> CreateContext(SessionSharedEvent evt)
    {
        var ctx = new Mock<ConsumeContext<SessionSharedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }
}

public class DailyGoalAchievedConsumerTests
{
    [Fact]
    public async Task Sends_realtime_when_user_online()
    {
        var notif = new Mock<INotificationService>();
        var repo = new Mock<INotificationRepository>();

        var pref = Notification.Domain.Entities.Notification.Create("auth0|u", "u@example.com");
        pref.SetOnline();
        repo.Setup(r => r.GetByUserIdAsync("auth0|u", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pref);

        var consumer = new DailyGoalAchievedConsumer(notif.Object, repo.Object, NullLogger<DailyGoalAchievedConsumer>.Instance);

        await consumer.Consume(CreateContext(new DailyGoalAchievedEvent(
            Guid.NewGuid(), "auth0|u", 120m, DateTime.UtcNow)).Object);

        notif.Verify(n => n.SendRealtimeAsync("auth0|u", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sends_email_when_user_offline()
    {
        var notif = new Mock<INotificationService>();
        var repo = new Mock<INotificationRepository>();

        var pref = Notification.Domain.Entities.Notification.Create("auth0|u", "u@example.com");
        // default IsOnline = false
        repo.Setup(r => r.GetByUserIdAsync("auth0|u", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pref);

        var consumer = new DailyGoalAchievedConsumer(notif.Object, repo.Object, NullLogger<DailyGoalAchievedConsumer>.Instance);

        await consumer.Consume(CreateContext(new DailyGoalAchievedEvent(
            Guid.NewGuid(), "auth0|u", 120m, DateTime.UtcNow)).Object);

        notif.Verify(n => n.SendEmailAsync("u@example.com", null,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Does_nothing_when_preference_missing()
    {
        var notif = new Mock<INotificationService>();
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification.Domain.Entities.Notification?)null);

        var consumer = new DailyGoalAchievedConsumer(notif.Object, repo.Object, NullLogger<DailyGoalAchievedConsumer>.Instance);

        await consumer.Consume(CreateContext(new DailyGoalAchievedEvent(
            Guid.NewGuid(), "auth0|u", 120m, DateTime.UtcNow)).Object);

        notif.VerifyNoOtherCalls();
    }

    private static Mock<ConsumeContext<DailyGoalAchievedEvent>> CreateContext(DailyGoalAchievedEvent evt)
    {
        var ctx = new Mock<ConsumeContext<DailyGoalAchievedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }
}
