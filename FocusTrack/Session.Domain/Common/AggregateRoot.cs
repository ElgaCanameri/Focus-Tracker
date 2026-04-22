using Session.Domain.DomainEvents;

namespace Session.Domain.Common
{
    public abstract class AggregateRoot
    {
        //collects all events
        public Guid Id { get; protected set; } = Guid.NewGuid();

        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
        //later raised in the entity
        protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
            _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
