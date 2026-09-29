using Asp.Versioning;
using Dealership.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dealership.Api.Controllers;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<TokenResult>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await authentication.LoginAsync(command, cancellationToken);
        return result is null ? Unauthorized(new ProblemDetails { Title = "Credenciales inválidas.", Detail = "Verifica tu correo y contraseña." }) : Ok(result);
    }
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResult>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authentication.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken) { await authentication.RevokeAsync(request.RefreshToken, cancellationToken); return NoContent(); }
}
public sealed record RefreshRequest(string RefreshToken);

