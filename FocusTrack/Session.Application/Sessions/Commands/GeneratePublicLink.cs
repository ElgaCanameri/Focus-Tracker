using MediatR;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Commands;

public record GeneratePublicLinkCommand(
    Guid SessionId,
    string RequestingUserId) : IRequest<string>;

public class GeneratePublicLinkHandler
    : IRequestHandler<GeneratePublicLinkCommand, string>
{
    private readonly ISessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public GeneratePublicLinkHandler(
        ISessionRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(
        GeneratePublicLinkCommand request, CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.SessionId, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.SessionId);

        if (session.UserId != request.RequestingUserId)
            throw new ForbiddenException();

        var token = session.GeneratePublicLink();
        _repository.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return token;
    }
}