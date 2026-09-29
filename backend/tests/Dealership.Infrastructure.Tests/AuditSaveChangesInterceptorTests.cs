using Dealership.Application;
using Dealership.Domain;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure;
using Xunit;
namespace Dealership.Infrastructure.Tests;
public sealed class AuditSaveChangesInterceptorTests
{
    [Fact]
    public async Task Excludes_password_hash_from_audit_values()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditSaveChangesInterceptor(new TestUser(), new TestCorrelation())).Options;
        await using var db = new DealershipDbContext(options); db.Users.Add(new User { Email = "test@example.com", PasswordHash = "not-for-audit" }); await db.SaveChangesAsync();
        var audit = Assert.Single(db.AuditLogs); Assert.DoesNotContain("PasswordHash", audit.NewValues!); Assert.Equal("test-actor", audit.ActorId);
    }
    private sealed class TestUser : ICurrentUser { public string? Id => "test-actor"; }
    private sealed class TestCorrelation : ICorrelationContext { public string Id => "test-correlation"; }
}
