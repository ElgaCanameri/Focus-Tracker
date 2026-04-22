namespace Session.Domain.Entities
{
    public sealed class SessionShare
    {
        public Guid Id { get; private set; }
        public Guid SessionId { get; private set; }
        public string RecipientUserId { get; private set; }
        public DateTime SharedAt { get; private set; }
        private SessionShare() { }
        public static SessionShare Create(Guid sessionId, string recipientUserId)
        {
            return new SessionShare
            {
                SessionId = sessionId,
                RecipientUserId = recipientUserId,
                SharedAt = DateTime.UtcNow
            };
        }
    }
}
