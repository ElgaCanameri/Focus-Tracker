using FluentAssertions;
using MassTransit;
using Moq;
using Session.Application.Admin.Commands;
using Session.Application.Admin.Queries;
using Session.Application.Common;
using Session.Application.Sessions.Commands;
using Session.Application.Sessions.Queries;
using Session.Domain.Entities;
using Session.Domain.Enums;
using Session.Infrastructure.Repositories;
using Xunit;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace Session.UnitTests.Application.Handlers;

public class CreateSessionHandlerTests
{
    [Fact]
    public async Task Creates_session_persists_it_and_publishes_event()
    {
        await using var db = InMemoryDbFactory.Create();
        var sessionRepo = new SessionRepository(db);
        var userRepo = new UserRepository(db);
        var publish = new Mock<IPublishEndpoint>();
        var handler = new CreateSessionHandler(sessionRepo, userRepo, db, publish.Object);

        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var id = await handler.Handle(
            new CreateSessionCommand("auth0|u1", "Topic", start, start.AddMinutes(30), SessionMode.Reading),
            CancellationToken.None);

        id.Should().NotBeEmpty();
        (await db.Sessions.FindAsync(id)).Should().NotBeNull();
        (await db.Users.CountAsync()).Should().Be(1); // auto-creates user on first call
        publish.Verify(p => p.Publish(
            It.Is<SessionCreatedEvent>(e => e.SessionId == id && e.UserId == "auth0|u1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reuses_existing_user_on_subsequent_create()
    {
        await using var db = InMemoryDbFactory.Create();
        db.Users.Add(User.Create("auth0|u1"));
        await db.SaveChangesAsync();

        var handler = new CreateSessionHandler(
            new SessionRepository(db), new UserRepository(db), db, new Mock<IPublishEndpoint>().Object);

        var start = DateTime.UtcNow;
        await handler.Handle(
            new CreateSessionCommand("auth0|u1", "Topic", start, start.AddMinutes(30), SessionMode.Coding),
            CancellationToken.None);

        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Throws_when_user_suspended()
    {
        await using var db = InMemoryDbFactory.Create();
        var user = User.Create("auth0|u1");
        user.ChangeStatus(UserStatus.Suspended);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new CreateSessionHandler(
            new SessionRepository(db), new UserRepository(db), db, new Mock<IPublishEndpoint>().Object);

        var start = DateTime.UtcNow;
        Func<Task> act = () => handler.Handle(
            new CreateSessionCommand("auth0|u1", "Topic", start, start.AddMinutes(30), SessionMode.Reading),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

public class UpdateSessionHandlerTests
{
    [Fact]
    public async Task Updates_when_caller_is_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = new DateTime(2025, 4, 11, 8, 15, 0, DateTimeKind.Utc);
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "Old", start, start.AddMinutes(30), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var handler = new UpdateSessionHandler(new SessionRepository(db), db, publish.Object);

        await handler.Handle(
            new UpdateSessionCommand(session.Id, "auth0|u1", "New", start, start.AddMinutes(60), SessionMode.Coding),
            CancellationToken.None);

        var updated = await db.Sessions.FindAsync(session.Id);
        updated!.Topic.Should().Be("New");
        updated.DurationMin.Value.Should().Be(60.00m);
        publish.Verify(p => p.Publish(
            It.IsAny<SessionUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_Forbidden_when_caller_is_not_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(10), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new UpdateSessionHandler(new SessionRepository(db), db, new Mock<IPublishEndpoint>().Object);

        Func<Task> act = () => handler.Handle(
            new UpdateSessionCommand(session.Id, "auth0|stranger", "X", start, start.AddMinutes(5), SessionMode.Coding),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Throws_NotFound_when_session_missing()
    {
        await using var db = InMemoryDbFactory.Create();
        var handler = new UpdateSessionHandler(new SessionRepository(db), db, new Mock<IPublishEndpoint>().Object);

        var start = DateTime.UtcNow;
        Func<Task> act = () => handler.Handle(
            new UpdateSessionCommand(Guid.NewGuid(), "u", "X", start, start.AddMinutes(5), SessionMode.Coding),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class DeleteSessionHandlerTests
{
    [Fact]
    public async Task Deletes_session_and_publishes_event()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(10), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var handler = new DeleteSessionHandler(new SessionRepository(db), db, publish.Object);

        await handler.Handle(new DeleteSessionCommand(session.Id, "auth0|u1"), CancellationToken.None);

        (await db.Sessions.CountAsync()).Should().Be(0);
        publish.Verify(p => p.Publish(
            It.Is<SessionDeletedEvent>(e => e.SessionId == session.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_Forbidden_for_non_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(10), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new DeleteSessionHandler(new SessionRepository(db), db, new Mock<IPublishEndpoint>().Object);

        Func<Task> act = () => handler.Handle(
            new DeleteSessionCommand(session.Id, "auth0|stranger"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class SessionQueryHandlerTests
{
    [Fact]
    public async Task GetById_returns_session_for_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(10), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionByIdHandler(new SessionRepository(db));
        var result = await handler.Handle(new GetSessionByIdQuery(session.Id, "auth0|u1"), CancellationToken.None);

        result.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetById_forbids_other_user()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(10), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionByIdHandler(new SessionRepository(db));
        Func<Task> act = () => handler.Handle(
            new GetSessionByIdQuery(session.Id, "auth0|stranger"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetById_throws_NotFound_when_missing()
    {
        await using var db = InMemoryDbFactory.Create();
        var handler = new GetSessionByIdHandler(new SessionRepository(db));

        Func<Task> act = () => handler.Handle(
            new GetSessionByIdQuery(Guid.NewGuid(), "u"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPaginated_returns_own_and_shared_sessions()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;

        var mine = Session.Domain.Entities.Session.Create(
            "auth0|me", "Mine", start, start.AddMinutes(15), SessionMode.Reading);
        var theirs = Session.Domain.Entities.Session.Create(
            "auth0|other", "Theirs", start, start.AddMinutes(20), SessionMode.Coding);

        db.Sessions.AddRange(mine, theirs);
        db.SessionShares.Add(SessionShare.Create(theirs.Id, "auth0|me"));
        await db.SaveChangesAsync();

        var handler = new GetSessionsPaginatedHandler(new SessionRepository(db));
        var (items, total) = await handler.Handle(
            new GetSessionsPaginatedQuery("auth0|me", 1, 10), CancellationToken.None);

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByPublicLink_throws_NotFound_for_missing_token()
    {
        await using var db = InMemoryDbFactory.Create();
        var handler = new GetSessionByPublicLinkHandler(new SessionRepository(db));

        Func<Task> act = () => handler.Handle(
            new GetSessionByPublicLinkQuery("does-not-exist"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByPublicLink_throws_RevokedException_when_revoked()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(10), SessionMode.Reading);
        var token = session.GeneratePublicLink();
        session.RevokePublicLink();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionByPublicLinkHandler(new SessionRepository(db));
        Func<Task> act = () => handler.Handle(new GetSessionByPublicLinkQuery(token), CancellationToken.None);

        await act.Should().ThrowAsync<RevokedException>();
    }

    [Fact]
    public async Task GetByPublicLink_returns_session_when_active()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(10), SessionMode.Reading);
        var token = session.GeneratePublicLink();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionByPublicLinkHandler(new SessionRepository(db));
        var result = await handler.Handle(new GetSessionByPublicLinkQuery(token), CancellationToken.None);

        result.Id.Should().Be(session.Id);
    }
}

public class ShareAndPublicLinkHandlerTests
{
    [Fact]
    public async Task ShareSession_adds_entries_and_publishes_event()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(30), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var handler = new ShareSessionHandler(
            new SessionRepository(db),
            new SessionShareRepository(db),
            db,
            publish.Object);

        await handler.Handle(
            new ShareSessionCommand(session.Id, "auth0|owner", new List<string> { "auth0|r1", "auth0|r2" }),
            CancellationToken.None);

        (await db.SessionShares.CountAsync()).Should().Be(2);
        publish.Verify(p => p.Publish(
            It.Is<SessionSharedEvent>(e => e.SessionId == session.Id && e.RecipientUserIds.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShareSession_skips_existing_recipients()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(30), SessionMode.Reading);
        db.Sessions.Add(session);
        db.SessionShares.Add(SessionShare.Create(session.Id, "auth0|r1"));
        await db.SaveChangesAsync();

        var handler = new ShareSessionHandler(
            new SessionRepository(db),
            new SessionShareRepository(db),
            db,
            new Mock<IPublishEndpoint>().Object);

        await handler.Handle(
            new ShareSessionCommand(session.Id, "auth0|owner", new List<string> { "auth0|r1", "auth0|r2" }),
            CancellationToken.None);

        (await db.SessionShares.CountAsync()).Should().Be(2); // r1 not duplicated
    }

    [Fact]
    public async Task ShareSession_forbids_non_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(30), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new ShareSessionHandler(
            new SessionRepository(db),
            new SessionShareRepository(db),
            db,
            new Mock<IPublishEndpoint>().Object);

        Func<Task> act = () => handler.Handle(
            new ShareSessionCommand(session.Id, "auth0|stranger", new List<string> { "auth0|r1" }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GeneratePublicLink_returns_token_and_persists()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(30), SessionMode.Reading);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GeneratePublicLinkHandler(new SessionRepository(db), db);
        var token = await handler.Handle(
            new GeneratePublicLinkCommand(session.Id, "auth0|u1"), CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
        var stored = await db.Sessions.FindAsync(session.Id);
        stored!.PublicLinkToken.Should().Be(token);
    }

    [Fact]
    public async Task RevokePublicLink_sets_flag()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|u1", "T", start, start.AddMinutes(30), SessionMode.Reading);
        session.GeneratePublicLink();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new RevokePublicLinkHandler(new SessionRepository(db), db);
        await handler.Handle(new RevokePublicLinkCommand(session.Id, "auth0|u1"), CancellationToken.None);

        var stored = await db.Sessions.FindAsync(session.Id);
        stored!.IsPublicLinkRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokePublicLink_forbids_non_owner()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = DateTime.UtcNow;
        var session = Session.Domain.Entities.Session.Create(
            "auth0|owner", "T", start, start.AddMinutes(30), SessionMode.Reading);
        session.GeneratePublicLink();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new RevokePublicLinkHandler(new SessionRepository(db), db);
        Func<Task> act = () => handler.Handle(
            new RevokePublicLinkCommand(session.Id, "auth0|stranger"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class AdminHandlerTests
{
    [Fact]
    public async Task ChangeUserStatus_updates_user_writes_audit_and_publishes_event()
    {
        await using var db = InMemoryDbFactory.Create();
        var user = User.Create("auth0|target");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var handler = new ChangeUserStatusHandler(
            new UserRepository(db),
            new AuditLogRepository(db),
            db,
            publish.Object);

        await handler.Handle(
            new ChangeUserStatusCommand("auth0|target", UserStatus.Suspended, "auth0|admin"),
            CancellationToken.None);

        var updated = await db.Users.FirstAsync();
        updated.Status.Should().Be(UserStatus.Suspended);
        (await db.AuditLogs.CountAsync()).Should().Be(1);
        publish.Verify(p => p.Publish(
            It.Is<UserStatusChangedEvent>(e =>
                e.UserId == user.Id.ToString() &&
                e.OldStatus == "Active" &&
                e.NewStatus == "Suspended"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeUserStatus_throws_NotFound_for_unknown_user()
    {
        await using var db = InMemoryDbFactory.Create();
        var handler = new ChangeUserStatusHandler(
            new UserRepository(db),
            new AuditLogRepository(db),
            db,
            new Mock<IPublishEndpoint>().Object);

        Func<Task> act = () => handler.Handle(
            new ChangeUserStatusCommand("unknown", UserStatus.Suspended, "admin"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetSessionsFiltered_applies_filters_and_paging()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = new DateTime(2025, 4, 11, 8, 0, 0, DateTimeKind.Utc);

        db.Sessions.AddRange(
            Session.Domain.Entities.Session.Create("auth0|a", "Read1", start, start.AddMinutes(30), SessionMode.Reading),
            Session.Domain.Entities.Session.Create("auth0|a", "Code1", start.AddHours(1), start.AddHours(1).AddMinutes(90), SessionMode.Coding),
            Session.Domain.Entities.Session.Create("auth0|b", "Read2", start, start.AddMinutes(10), SessionMode.Reading));
        await db.SaveChangesAsync();

        var handler = new GetSessionsFilteredHandler(new SessionRepository(db));
        var (items, total) = await handler.Handle(
            new GetSessionsFilteredQuery(
                UserId: "auth0|a",
                Mode: null,
                StartDateFrom: null,
                StartDateTo: null,
                MinDuration: null,
                MaxDuration: null,
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        total.Should().Be(2);
        items.Should().OnlyContain(s => s.UserId == "auth0|a");
    }

    [Fact]
    public async Task GetSessionsFiltered_respects_duration_bounds()
    {
        await using var db = InMemoryDbFactory.Create();
        var start = new DateTime(2025, 4, 11, 8, 0, 0, DateTimeKind.Utc);

        db.Sessions.AddRange(
            Session.Domain.Entities.Session.Create("u", "short", start, start.AddMinutes(10), SessionMode.Reading),
            Session.Domain.Entities.Session.Create("u", "mid",   start.AddHours(1), start.AddHours(1).AddMinutes(30), SessionMode.Reading),
            Session.Domain.Entities.Session.Create("u", "long",  start.AddHours(2), start.AddHours(2).AddMinutes(120), SessionMode.Reading));
        await db.SaveChangesAsync();

        var handler = new GetSessionsFilteredHandler(new SessionRepository(db));
        var (items, total) = await handler.Handle(
            new GetSessionsFilteredQuery(null, null, null, null, 20m, 60m, 1, 10),
            CancellationToken.None);

        total.Should().Be(1);
        items.Single().Topic.Should().Be("mid");
    }

    [Fact]
    public async Task GetMonthlyStatistics_returns_projection_rows()
    {
        await using var db = InMemoryDbFactory.Create();
        db.MonthlyFocusItems.AddRange(
            new MonthlyFocusEntity { Id = Guid.NewGuid(), UserId = "u1", Year = 2025, Month = 4, TotalDurationMin = 200m },
            new MonthlyFocusEntity { Id = Guid.NewGuid(), UserId = "u2", Year = 2025, Month = 4, TotalDurationMin = 350m });
        await db.SaveChangesAsync();

        var handler = new GetMonthlyStatisticsHandler(new SessionRepository(db));
        var results = (await handler.Handle(new GetMonthlyStatisticsQuery(1, 10), CancellationToken.None)).ToList();

        results.Should().HaveCount(2);
    }
}
