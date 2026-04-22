using MediatR;
using Session.Domain.Entities;
using Session.Domain.Interfaces;

namespace Session.Application.Admin.Queries;

public record GetMonthlyStatisticsQuery(
    int Page,
    int PageSize) : IRequest<IEnumerable<MonthlyFocusEntity>>;

public class GetMonthlyStatisticsHandler
    : IRequestHandler<GetMonthlyStatisticsQuery, IEnumerable<MonthlyFocusEntity>>
{
    private readonly ISessionRepository _repository;

    public GetMonthlyStatisticsHandler(ISessionRepository repository)
        => _repository = repository;

    public async Task<IEnumerable<MonthlyFocusEntity>> Handle(
        GetMonthlyStatisticsQuery request,
        CancellationToken ct)
    {
        return await _repository.GetMonthlyStatisticsAsync(
            request.Page,
            request.PageSize,
            ct);
    }
}