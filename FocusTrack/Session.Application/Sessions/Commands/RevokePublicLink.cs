using MediatR;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Commands;

public record RevokePublicLinkCommand(
    Guid SessionId,
    string RequestingUserId) : IRequest;

public class RevokePublicLinkHandler : IRequestHandler<RevokePublicLinkCommand>
{
    private readonly ISessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokePublicLinkHandler(
        ISessionRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RevokePublicLinkCommand request, CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.SessionId, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.SessionId);

        if (session.UserId != request.RequestingUserId)
            throw new ForbiddenException();

        session.RevokePublicLink();
        _repository.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}