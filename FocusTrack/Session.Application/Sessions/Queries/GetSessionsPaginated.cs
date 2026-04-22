using MediatR;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Queries;

public record GetSessionsPaginatedQuery(
    string UserId,
    int Page,
    int PageSize) : IRequest<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)>;

public class GetSessionsPaginatedHandler
    : IRequestHandler<GetSessionsPaginatedQuery,
        (IEnumerable<Domain.Entities.Session> Items, int TotalCount)>
{
    private readonly ISessionRepository _repository;

    public GetSessionsPaginatedHandler(ISessionRepository repository)
        => _repository = repository;

    public async Task<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)> Handle(
        GetSessionsPaginatedQuery request,
        CancellationToken ct)
    {
        return await _repository.GetPagedAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            ct);
    }
}