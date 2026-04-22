using Session.Domain.Enums;

namespace Session.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }

    //blocks direct instantiation, EF needs it as it must be a parameterles constructor to recreate an object from the database
    private User() { } // EF Core

    public static User Create(string externalId)
    {
        return new User
        {
            ExternalId = externalId,
            Status = UserStatus.Active
        };
    }

    public void ChangeStatus(UserStatus newStatus)
    {
        Status = newStatus;
    }

    public bool CanAuthenticate() =>
        Status == UserStatus.Active;
}