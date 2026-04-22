namespace FocusTrack.Gateway
{
    public class SuspendedUsers
    {
        private readonly HashSet<string> _suspended = new();
        public void Add(string userId) => _suspended.Add(userId);
        public void Remove(string userId) => _suspended.Remove(userId);
        public bool IsSuspended(string userId) => _suspended.Contains(userId);
    }
}
