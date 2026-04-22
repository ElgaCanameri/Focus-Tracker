namespace Contracts.Events
{
    public record UserLoggedInEvent(
        string UserId,
        string Email);
}
