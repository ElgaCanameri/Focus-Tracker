using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Session.Domain.Enums;
using Session.Domain.Interfaces;

namespace Session.Presentation.Controllers;

[ApiController]
[Route("internal")]
[AllowAnonymous]
public class InternalController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public InternalController(IUserRepository userRepository)
        => _userRepository = userRepository;

    [HttpGet("users/{id}/is-suspended")]
    public async Task<IActionResult> IsSuspended(string id, CancellationToken ct)
    {
        var decoded = Uri.UnescapeDataString(id);
        var user = await _userRepository.GetByExternalIdAsync(decoded, ct);

        if (user is null) return Ok(false);

        var blocked = user.Status == UserStatus.Suspended
                   || user.Status == UserStatus.Deactivated;

        return Ok(blocked);
    }
}