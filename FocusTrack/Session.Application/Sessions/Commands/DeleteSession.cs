using MassTransit;
using MediatR;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Commands;

public record DeleteSessionCommand(Guid Id, string UserId) : IRequest;

public class DeleteSessionHandler : IRequestHandler<DeleteSessionCommand>
{
    private readonly ISessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public DeleteSessionHandler(
        ISessionRepository repository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(DeleteSessionCommand request, CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.Id, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.Id);

        if (session.UserId != request.UserId)
            throw new ForbiddenException();

        session.Delete();
        _repository.Delete(session);
        await _unitOfWork.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new Contracts.Events.SessionDeletedEvent(
        session.Id,
        session.UserId,
        DateTime.UtcNow), ct);
    }
}