using MediatR;
using Session.Application.Common;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Queries;

public record GetSessionByPublicLinkQuery(string Token)
    : IRequest<Domain.Entities.Session>;

public class GetSessionByPublicLinkHandler
    : IRequestHandler<GetSessionByPublicLinkQuery, Domain.Entities.Session>
{
    private readonly ISessionRepository _repository;

    public GetSessionByPublicLinkHandler(ISessionRepository repository)
        => _repository = repository;

    public async Task<Domain.Entities.Session> Handle(
        GetSessionByPublicLinkQuery request, CancellationToken ct)
    {
        var session = await _repository.GetByPublicTokenAsync(request.Token, ct);

        if (session is null)
            throw new NotFoundException("PublicLink", Guid.Empty);

        // 410 Gone if revoked
        if (session.IsPublicLinkRevoked)
            throw new RevokedException();

        return session;
    }
}