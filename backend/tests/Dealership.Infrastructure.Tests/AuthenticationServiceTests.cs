using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace Dealership.Infrastructure.Tests;
public sealed class AuthenticationServiceTests
{
    [Fact]
    public void Singleton_password_work_uses_the_same_owasp_parameters_for_real_and_unknown_hashes()
    {
        using var provider = new ServiceCollection().AddSingleton<IPasswordWorkService, PasswordWorkService>().BuildServiceProvider();
        var passwords = Assert.IsType<PasswordWorkService>(provider.GetRequiredService<IPasswordWorkService>());
        Assert.Same(passwords, provider.GetRequiredService<IPasswordWorkService>());
        var user = new User();
        var hash = passwords.Hash(user, "Real-password-123");
        Assert.Equal(PasswordWorkService.OwaspIterationCount, passwords.IterationCount);
        Assert.Equal(passwords.GetIterationCount(hash), passwords.GetIterationCount(passwords.UnknownHashForTesting));
        Assert.True(passwords.Verify(user, hash, "Real-password-123"));
        Assert.False(passwords.VerifyUnknown("wrong-password"));
    }
    [Fact]
    public async Task Rejects_invalid_credentials()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DealershipDbContext(options);
        var user = new User { Email = "admin@example.com" }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "correct-password"); db.Users.Add(user); await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build();
        var service = new AuthenticationService(db, config, new PasswordWorkService());
        Assert.Null(await service.LoginAsync(new LoginCommand("admin@example.com", "incorrect-password"), CancellationToken.None));
    }

    [Fact]
    public async Task Customer_token_contains_account_type_and_staff_login_rejects_customer()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DealershipDbContext(options);
        var customerRole = new Role { Name = SystemRoles.Customer };
        var customer = new User { Email = "customer@example.com", AccountType = AccountType.Customer };
        customer.PasswordHash = new PasswordHasher<User>().HashPassword(customer, "correct-password");
        customer.Roles.Add(new UserRole { User = customer, Role = customerRole });
        db.AddRange(customerRole, customer);
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build();
        var service = new AuthenticationService(db, config, new PasswordWorkService());

        Assert.Null(await service.LoginAsync(new LoginCommand(customer.Email, "correct-password"), CancellationToken.None, AccountType.Staff));
        var result = await service.LoginAsync(new LoginCommand(customer.Email, "correct-password"), CancellationToken.None, AccountType.Customer);

        Assert.NotNull(result);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken);
        Assert.Equal("Customer", jwt.Claims.Single(x => x.Type == "account_type").Value);
        Assert.Contains(jwt.Claims, x => x.Type == ClaimTypes.Role && x.Value == SystemRoles.Customer);
    }
}
