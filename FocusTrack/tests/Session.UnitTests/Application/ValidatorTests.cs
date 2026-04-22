using FluentValidation.TestHelper;
using Session.Application.Admin.Commands;
using Session.Application.Sessions.Commands;
using Session.Domain.Enums;
using Xunit;

namespace Session.UnitTests.Application.Validators;

public class CreateSessionValidatorTests
{
    private readonly CreateSessionValidator _validator = new();

    [Fact]
    public void Empty_topic_fails()
    {
        var start = DateTime.UtcNow;
        var cmd = new CreateSessionCommand("user", "", start, start.AddMinutes(10), SessionMode.Reading);

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Topic);
    }

    [Fact]
    public void Topic_longer_than_200_chars_fails()
    {
        var start = DateTime.UtcNow;
        var cmd = new CreateSessionCommand(
            "user", new string('x', 201), start, start.AddMinutes(10), SessionMode.Reading);

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Topic);
    }

    [Fact]
    public void EndTime_not_after_StartTime_fails()
    {
        var start = DateTime.UtcNow;
        var cmd = new CreateSessionCommand("user", "Topic", start, start, SessionMode.Reading);

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EndTime)
              .WithErrorMessage("EndTime must be after StartTime.");
    }

    [Fact]
    public void Invalid_mode_fails()
    {
        var start = DateTime.UtcNow;
        var cmd = new CreateSessionCommand("user", "Topic", start, start.AddMinutes(10), (SessionMode)999);

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Mode);
    }

    [Theory]
    [InlineData(SessionMode.Reading)]
    [InlineData(SessionMode.Coding)]
    [InlineData(SessionMode.VideoCourse)]
    [InlineData(SessionMode.Practice)]
    public void Valid_command_passes(SessionMode mode)
    {
        var start = DateTime.UtcNow;
        var cmd = new CreateSessionCommand("user", "Topic", start, start.AddMinutes(30), mode);

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UpdateSessionValidatorTests
{
    private readonly UpdateSessionValidator _validator = new();

    [Fact]
    public void Empty_topic_fails()
    {
        var start = DateTime.UtcNow;
        var cmd = new UpdateSessionCommand(Guid.NewGuid(), "user", "", start, start.AddMinutes(10), SessionMode.Reading);

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Topic);
    }

    [Fact]
    public void Reversed_times_fail()
    {
        var start = DateTime.UtcNow;
        var cmd = new UpdateSessionCommand(Guid.NewGuid(), "user", "Topic", start, start.AddMinutes(-1), SessionMode.Reading);

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.EndTime);
    }
}

public class ShareSessionValidatorTests
{
    private readonly ShareSessionValidator _validator = new();

    [Fact]
    public void Empty_recipient_list_fails()
    {
        var cmd = new ShareSessionCommand(Guid.NewGuid(), "owner", new List<string>());

        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(x => x.RecipientUserIds);
    }

    [Fact]
    public void Non_empty_recipient_list_passes()
    {
        var cmd = new ShareSessionCommand(Guid.NewGuid(), "owner", new List<string> { "a", "b" });

        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

public class ChangeUserStatusValidatorTests
{
    private readonly ChangeUserStatusValidator _validator = new();

    [Fact]
    public void Empty_userid_fails()
    {
        var cmd = new ChangeUserStatusCommand("", UserStatus.Suspended, "admin");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Empty_performedBy_fails()
    {
        var cmd = new ChangeUserStatusCommand("u", UserStatus.Suspended, "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.PerformedBy);
    }

    [Fact]
    public void Invalid_status_fails()
    {
        var cmd = new ChangeUserStatusCommand("u", (UserStatus)99, "admin");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.NewStatus);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var cmd = new ChangeUserStatusCommand("u", UserStatus.Suspended, "admin");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
