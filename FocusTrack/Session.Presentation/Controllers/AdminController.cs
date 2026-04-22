using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Session.Application.Admin.Commands;
using Session.Application.Admin.Queries;
using Session.Domain.Enums;
using Session.Domain.Models;
using System.Security.Claims;

namespace Session.Presentation.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("sessions")]
    public async Task<IActionResult> GetFilteredSessions(
        [FromQuery] string? userId,
        [FromQuery] SessionMode? mode,
        [FromQuery] DateTime? startDateFrom,
        [FromQuery] DateTime? startDateTo,
        [FromQuery] decimal? minDuration,
        [FromQuery] decimal? maxDuration,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, total) = await _mediator.Send(
            new GetSessionsFilteredQuery(
                userId,
                mode,
                startDateFrom,
                startDateTo,
                minDuration,
                maxDuration,
                page,
                pageSize), ct);

        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpGet("statistics/monthly-focus")]
    public async Task<IActionResult> GetMonthlyStatistics([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMonthlyStatisticsQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPatch("users/{id}/status")]
    public async Task<IActionResult> ChangeUserStatus(
     string id,
     [FromBody] ChangeUserStatus request,
     CancellationToken ct)
    {
        var performedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        await _mediator.Send(new ChangeUserStatusCommand(
            id,
            request.Status,
            performedBy), ct);

        return NoContent();
    }
}