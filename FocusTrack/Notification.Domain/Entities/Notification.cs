namespace Notification.Domain.Entities;

// tracks whether a user is online or offline
public sealed class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public bool IsOnline { get; private set; }

    private Notification() { }

    public static Notification Create(string userId, string email)
    {
        return new Notification
        {
            UserId = userId,
            Email = email,
            IsOnline = false
        };
    }

    public void SetOnline() => IsOnline = true;
    public void SetOffline() => IsOnline = false;
}