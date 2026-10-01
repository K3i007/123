using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Dealership.Infrastructure.Tests;
public sealed class RefreshRotationTests
{
    [Fact]
    public async Task Reuse_revokes_entire_family()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; await using var db = new DealershipDbContext(options);
        var user = new User { Email = "user@example.com" }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "password"); db.Users.Add(user); await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build(); var service = new AuthenticationService(db, config, new PasswordWorkService());
        var first = await service.LoginAsync(new LoginCommand(user.Email, "password"), CancellationToken.None); Assert.NotNull(first); var rotated = await service.RefreshAsync(first!.RefreshToken, CancellationToken.None); Assert.NotNull(rotated); Assert.Null(await service.RefreshAsync(first.RefreshToken, CancellationToken.None)); Assert.All(db.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }
}
