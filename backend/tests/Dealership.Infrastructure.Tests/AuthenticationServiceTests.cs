using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dealership.Infrastructure.Tests;
public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task Rejects_invalid_credentials()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DealershipDbContext(options);
        var user = new User { Email = "admin@example.com" }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "correct-password"); db.Users.Add(user); await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build();
        var service = new AuthenticationService(db, config);
        Assert.Null(await service.LoginAsync(new LoginCommand("admin@example.com", "incorrect-password"), CancellationToken.None));
    }
}
