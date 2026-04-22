using MediatR;
using Session.Domain.Enums;
using Session.Domain.Interfaces;

namespace Session.Application.Admin.Queries;

public record GetSessionsFilteredQuery(
    string? UserId,
    SessionMode? Mode,
    DateTime? StartDateFrom,
    DateTime? StartDateTo,
    decimal? MinDuration,
    decimal? MaxDuration,
    int Page,
    int PageSize) : IRequest<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)>;

public class GetSessionsFilteredHandler
    : IRequestHandler<GetSessionsFilteredQuery,
        (IEnumerable<Domain.Entities.Session> Items, int TotalCount)>
{
    private readonly ISessionRepository _repository;

    public GetSessionsFilteredHandler(ISessionRepository repository)
        => _repository = repository;

    public async Task<(IEnumerable<Domain.Entities.Session> Items, int TotalCount)> Handle(
        GetSessionsFilteredQuery request,
        CancellationToken ct)
    {
        return await _repository.GetFilteredAsync(
            request.UserId,
            request.Mode,
            request.StartDateFrom,
            request.StartDateTo,
            request.MinDuration,
            request.MaxDuration,
            request.Page,
            request.PageSize,
            ct);
    }
}