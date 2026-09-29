using Asp.Versioning;
using Dealership.Application;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Api.Controllers;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/admin"), Authorize(Policy = "Administration")]
public sealed class AdminController(DealershipDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary(CancellationToken cancellationToken) => Ok(new { auditEvents = await db.AuditLogs.CountAsync(cancellationToken) });
    [HttpGet("audit")]
    public async Task<ActionResult<object>> Audit(CancellationToken cancellationToken) => Ok(await db.AuditLogs.OrderByDescending(x => x.OccurredAt).Take(50).ToListAsync(cancellationToken));

    [HttpPost("sessions/revoke")]
    public async Task<ActionResult<object>> RevokeCurrentSessions(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.Id, out var userId)) return Unauthorized();
        var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { revokedSessions = tokens.Count });
    }
}
