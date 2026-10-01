using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dealership.Infrastructure.Tests;

public sealed class CustomerAccountTests
{
    [Fact]
    public async Task Registration_never_accepts_role_and_emits_a_verification_token_only_in_dev_sender()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DealershipDbContext(options);
        db.Roles.Add(new Role { Name = SystemRoles.Customer }); await db.SaveChangesAsync();
        var sender = new FakeEmailSender();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters" }).Build();
        var service = new CustomerAccountService(db, configuration, sender, new PasswordWorkService());

        Assert.True(await service.RegisterAsync(new CustomerRegistrationCommand("client@example.com", "a-secure-password", "Cliente", "55 (123) 456-7890", "2026-09"), CancellationToken.None));
        var user = await db.Users.SingleAsync();
        Assert.Equal(AccountType.Customer, user.AccountType);
        Assert.DoesNotContain(user.Roles, role => role.Role.Name == SystemRoles.Administrator);
        Assert.Equal("551234567890", user.Phone);
        Assert.Single(db.OneTimeTokens);
        Assert.DoesNotContain("a-secure-password", sender.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verification_token_can_be_consumed_only_once()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DealershipDbContext(options);
        db.Roles.Add(new Role { Name = SystemRoles.Customer }); await db.SaveChangesAsync();
        var sender = new FakeEmailSender();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "a-development-signing-key-with-more-than-32-characters" }).Build();
        var service = new CustomerAccountService(db, configuration, sender, new PasswordWorkService());
        await service.RegisterAsync(new CustomerRegistrationCommand("verify@example.com", "a-secure-password", "Cliente", null, "2026-09"), CancellationToken.None);
        var token = sender.Body.Split(": ", StringSplitOptions.None)[1];

        Assert.True(await service.VerifyEmailAsync(token, CancellationToken.None));
        Assert.False(await service.VerifyEmailAsync(token, CancellationToken.None));
        Assert.True((await db.Users.SingleAsync()).EmailVerified);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public bool IsAvailable => true;
        public string Body { get; private set; } = string.Empty;
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) { Body = body; return Task.CompletedTask; }
    }
}
