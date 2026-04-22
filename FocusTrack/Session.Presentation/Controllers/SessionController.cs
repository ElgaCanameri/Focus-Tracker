using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Session.Application.Common;
using Session.Application.Sessions.Commands;
using Session.Application.Sessions.Queries;
using System.Security.Claims;

namespace Session.Presentation.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SessionsController(IMediator mediator) => _mediator = mediator;
    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    #region Basic CRUD endpoints

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(
            command with { UserId = CurrentUserId }, ct);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var session = await _mediator.Send(
            new GetSessionByIdQuery(id, CurrentUserId), ct);

        return Ok(session);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _mediator.Send(new GetSessionsPaginatedQuery(CurrentUserId, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionCommand command, CancellationToken ct)
    {
        await _mediator.Send(command with { Id = id, UserId = CurrentUserId }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteSessionCommand(id, CurrentUserId), ct);
        return NoContent();
    }
    
    #endregion

    #region Session Share

    //U4 - Share Session with other users
    [HttpPost("{id:guid}/share")]
    public async Task<IActionResult> Share(Guid id, [FromBody] List<string> recipientUserIds, CancellationToken ct)
    {
        await _mediator.Send(new ShareSessionCommand(id, CurrentUserId, recipientUserIds), ct);
        return Ok();
    }

    // U4 - Generate public link
    [HttpPost("{id:guid}/public-link")]
    public async Task<IActionResult> GeneratePublicLink(Guid id, CancellationToken ct)
    {
        var token = await _mediator.Send(new GeneratePublicLinkCommand(id, CurrentUserId), ct);
        return Ok(new { token, url = $"/api/sessions/public/{token}" });
    }

    // U4 - Revoke public link
    [HttpDelete("{id:guid}/public-link")]
    public async Task<IActionResult> RevokePublicLink(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RevokePublicLinkCommand(id, CurrentUserId), ct);
        return NoContent();
    }

    // U4 - Access via public link (no auth required)
    [HttpGet("public/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPublicLink(string token, CancellationToken ct)
    {
        try
        {
            var session = await _mediator.Send(new GetSessionByPublicLinkQuery(token), ct);
            return Ok(session);
        }
        catch (RevokedException)
        {
            return StatusCode(410, new { message = "This link has been revoked." });
        }
    }
   
    #endregion
}