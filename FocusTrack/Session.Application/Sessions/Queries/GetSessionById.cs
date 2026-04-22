using MediatR;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Queries;

public record GetSessionByIdQuery(Guid Id, string UserId)
    : IRequest<Domain.Entities.Session>;

public class GetSessionByIdHandler
    : IRequestHandler<GetSessionByIdQuery, Domain.Entities.Session>
{
    private readonly ISessionRepository _repository;

    public GetSessionByIdHandler(ISessionRepository repository)
        => _repository = repository;

    public async Task<Domain.Entities.Session> Handle(
        GetSessionByIdQuery request,
        CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.Id, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.Id);

        if (session.UserId != request.UserId)
            throw new ForbiddenException();

        return session;
    }
}