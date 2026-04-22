using Contracts.Events;
using MassTransit;

namespace FocusTrack.Gateway.Consumer
{
    public class UserStatusChangedConsumer : IConsumer<UserStatusChangedEvent>
    {
        private readonly SuspendedUsers _cache;
        public UserStatusChangedConsumer(SuspendedUsers cache) => _cache = cache;

        public Task Consume(ConsumeContext<UserStatusChangedEvent> ctx)
        {
            if (ctx.Message.NewStatus == "Suspended")
                _cache.Add(ctx.Message.UserId);
            else
                _cache.Remove(ctx.Message.UserId); // un-suspend
            return Task.CompletedTask;
        }
    }
}
