using Asp.Versioning;
using Dealership.Application;
using Dealership.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dealership.Api.Controllers;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/customers")]
public sealed class CustomerController(ICustomerAccountService accounts, IAuthenticationService authentication, ICurrentUser currentUser, IAccountRateLimitService accountLimits) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("customer-register")]
    public async Task<IActionResult> Register(CustomerRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (!await AccountAllowedAsync("register", request.Email, cancellationToken)) return TooManyAttempts();
        try
        {
            var accepted = await accounts.RegisterAsync(new CustomerRegistrationCommand(request.Email, request.Password, request.Name, request.Phone, request.PrivacyPolicyVersion), cancellationToken);
            if (!accepted) return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "El registro no está disponible.", Detail = "Intenta nuevamente más tarde." });
            return Accepted(new { message = "Si el correo puede registrarse, recibirás instrucciones para verificarlo." });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "No se pudo crear la cuenta.", Detail = exception.Message });
        }
    }

    [HttpPost("login"), EnableRateLimiting("customer-login")]
    public async Task<ActionResult<TokenResult>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        if (!await AccountAllowedAsync("login", command.Email, cancellationToken)) return TooManyAttempts();
        var result = await authentication.LoginAsync(command, cancellationToken, AccountType.Customer);
        return result is null
            ? Unauthorized(new ProblemDetails { Title = "No fue posible iniciar sesión.", Detail = "Verifica tus datos o espera antes de intentarlo de nuevo." })
            : Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(OneTimeTokenRequest request, CancellationToken cancellationToken) =>
        await accounts.VerifyEmailAsync(request.Token, cancellationToken)
            ? NoContent()
            : BadRequest(new ProblemDetails { Title = "El enlace ya no es válido.", Detail = "Solicita uno nuevo." });

    [HttpPost("verify-email/resend"), EnableRateLimiting("customer-resend")]
    public async Task<IActionResult> ResendVerification(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        if (!await AccountAllowedAsync("resend", request.Email, cancellationToken)) return TooManyAttempts();
        await accounts.RequestEmailVerificationAsync(request.Email, cancellationToken);
        return Accepted(new { message = "Si existe una cuenta pendiente, recibirás instrucciones para verificarla." });
    }

    [HttpPost("password-reset"), EnableRateLimiting("customer-reset")]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        if (!await AccountAllowedAsync("reset", request.Email, cancellationToken)) return TooManyAttempts();
        await accounts.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted(new { message = "Si existe una cuenta, recibirás instrucciones para restablecer la contraseña." });
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await accounts.ResetPasswordAsync(request.Token, request.Password, cancellationToken)
                ? NoContent()
                : BadRequest(new ProblemDetails { Title = "El enlace ya no es válido.", Detail = "Solicita uno nuevo." });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new ProblemDetails { Title = "No se pudo cambiar la contraseña.", Detail = exception.Message });
        }
    }

    [HttpGet("profile"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<CustomerProfile>> Profile(CancellationToken cancellationToken)
    {
        var profile = await accounts.GetProfileAsync(UserId(), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> UpdateProfile(CustomerProfileRequest request, CancellationToken cancellationToken) =>
        await accounts.UpdateProfileAsync(UserId(), request.Name, request.Phone, request.Language, request.MarketingConsent, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("export"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<CustomerExport>> Export(PasswordConfirmationRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.ExportAsync(UserId(), request.Password, cancellationToken);
        return result is null ? Unauthorized(new ProblemDetails { Title = "Contraseña incorrecta." }) : Ok(result);
    }

    [HttpPost("password"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try { return await accounts.ChangePasswordAsync(UserId(), request.CurrentPassword, request.NewPassword, cancellationToken) ? NoContent() : Unauthorized(new ProblemDetails { Title = "Contraseña incorrecta." }); }
        catch (DomainRuleException exception) { return BadRequest(new ProblemDetails { Title = "Contraseña inválida.", Detail = exception.Message }); }
    }

    [HttpDelete("account"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> DeleteAccount(PasswordConfirmationRequest request, CancellationToken cancellationToken) =>
        await accounts.DeleteAsync(UserId(), request.Password, cancellationToken) ? NoContent() : Unauthorized(new ProblemDetails { Title = "Contraseña incorrecta." });

    [HttpGet("sessions"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<IReadOnlyCollection<CustomerSession>>> Sessions(CancellationToken cancellationToken) => Ok(await accounts.GetSessionsAsync(UserId(), cancellationToken));

    [HttpDelete("sessions/{sessionId:guid}"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken) => await accounts.RevokeSessionAsync(UserId(), sessionId, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("sessions"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<object>> RevokeAllSessions(CancellationToken cancellationToken) => Ok(new { revokedSessions = await accounts.RevokeAllSessionsAsync(UserId(), cancellationToken) });

    [HttpGet("favorites"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<object>> Favorites(CancellationToken cancellationToken) => Ok(new { vehicleIds = await accounts.GetFavoriteIdsAsync(UserId(), cancellationToken) });

    [HttpPost("favorites/merge"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<object>> MergeFavorites(VehicleIdsRequest request, CancellationToken cancellationToken)
    {
        if (!await accounts.IsEmailVerifiedAsync(UserId(), cancellationToken)) return EmailNotVerified();
        var before = (await accounts.GetFavoriteIdsAsync(UserId(), cancellationToken)).Count;
        var vehicleIds = await accounts.MergeFavoritesAsync(UserId(), request.VehicleIds ?? [], cancellationToken);
        return Ok(new { vehicleIds, addedCount = Math.Max(0, vehicleIds.Count - before) });
    }

    [HttpPut("favorites/{vehicleId:guid}"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> SetFavorite(Guid vehicleId, FavoriteRequest request, CancellationToken cancellationToken)
    {
        if (!await accounts.IsEmailVerifiedAsync(UserId(), cancellationToken)) return EmailNotVerified();
        return await accounts.SetFavoriteAsync(UserId(), vehicleId, request.Favorite, cancellationToken) ? NoContent() : BadRequest(new ProblemDetails { Title = "No se puede guardar este vehículo." });
    }

    [HttpGet("comparison"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<object>> Comparison(CancellationToken cancellationToken) => Ok(new { vehicleIds = await accounts.GetComparisonIdsAsync(UserId(), cancellationToken) });

    [HttpPut("comparison"), Authorize(Policy = "Customer")]
    public async Task<ActionResult<object>> SetComparison(VehicleIdsRequest request, CancellationToken cancellationToken)
    {
        if (!await accounts.IsEmailVerifiedAsync(UserId(), cancellationToken)) return EmailNotVerified();
        return Ok(new { vehicleIds = await accounts.SetComparisonAsync(UserId(), request.VehicleIds ?? [], cancellationToken) });
    }

    private Guid UserId() => Guid.TryParse(currentUser.Id, out var id) ? id : throw new UnauthorizedAccessException();
    private Task<bool> AccountAllowedAsync(string purpose, string email, CancellationToken cancellationToken) => accountLimits.AllowAsync(purpose, email.Trim().ToLowerInvariant(), cancellationToken);
    private ObjectResult TooManyAttempts()
    {
        Response.Headers.RetryAfter = "60";
        return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Demasiadas solicitudes.", Detail = "Espera un minuto antes de intentar nuevamente." });
    }
    private ObjectResult EmailNotVerified() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Verifica tu correo", Detail = "Verifica tu correo antes de guardar datos en tu cuenta.", Extensions = { ["code"] = "email_not_verified" } });
}

public sealed record CustomerRegistrationRequest(string Email, string Password, string Name, string? Phone, string PrivacyPolicyVersion);
public sealed record CustomerProfileRequest(string Name, string? Phone, string Language, bool MarketingConsent);
public sealed record OneTimeTokenRequest(string Token);
public sealed record PasswordResetRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string Password);
public sealed record VehicleIdsRequest(IReadOnlyCollection<Guid>? VehicleIds);
public sealed record FavoriteRequest(bool Favorite);
public sealed record PasswordConfirmationRequest(string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
