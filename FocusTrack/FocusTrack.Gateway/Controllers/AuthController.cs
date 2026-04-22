using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusTrack.Gateway.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        // redirects to Auth0 login page
        [HttpGet("login")]
        public IActionResult Login(string returnUrl = "/", string prompt = null)
        {
            var props = new AuthenticationProperties { RedirectUri = returnUrl };

            if (!string.IsNullOrEmpty(prompt))
                props.Items["prompt"] = prompt;

            return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
        }

        // clears local cookie AND revokes with Auth0
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // 1. clear local cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // 2. revoke with Auth0 → triggers OnRedirectToIdentityProviderForSignOut
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
            return Ok();
        }
       
        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            var claims = User.Claims
                .Select(c => new { c.Type, c.Value });
            return Ok(claims);
        }
    }
}
