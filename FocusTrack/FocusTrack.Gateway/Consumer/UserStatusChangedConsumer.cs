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
            Console.WriteLine($"[Consumer] Received UserStatusChangedEvent: UserId={ctx.Message.UserId}, NewStatus={ctx.Message.NewStatus}");

            if (ctx.Message.NewStatus == "Suspended")
                _cache.Add(ctx.Message.UserId);
            else
                _cache.Remove(ctx.Message.UserId);

            Console.WriteLine($"[Consumer] Cache updated");
            return Task.CompletedTask;
        }
    }
}
