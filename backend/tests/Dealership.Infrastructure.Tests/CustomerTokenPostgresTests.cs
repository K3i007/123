using System.Security.Cryptography;
using System.Text;
using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dealership.Infrastructure.Tests;

[Collection("Public catalog HTTP")]
public sealed class CustomerTokenPostgresTests
{
    [Fact]
    public async Task Verification_token_cannot_be_reused_sequentially_and_an_expired_token_is_rejected()
    {
        // PostgresGuard.Resolve() skips (or fails if REQUIRE_POSTGRES=true) when no DB is configured.
        var connectionString = PostgresGuard.Resolve();
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(connectionString).Options;
        var configuration = TestConfiguration();
        var suffix = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var expiredRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        Guid userId;
        await using (var setup = new DealershipDbContext(options))
        {
            var role = await setup.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
            var user = new User { Email = $"sequential-{suffix}@test.invalid", AccountType = AccountType.Customer };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Original-password-123");
            user.Roles.Add(new UserRole { User = user, Role = role });
            setup.Users.Add(user);
            setup.OneTimeTokens.AddRange(
                Token(user, raw, OneTimeTokenPurpose.EmailVerification, DateTimeOffset.UtcNow.AddMinutes(5), configuration),
                Token(user, expiredRaw, OneTimeTokenPurpose.EmailVerification, DateTimeOffset.UtcNow.AddMinutes(-1), configuration));
            await setup.SaveChangesAsync(); userId = user.Id;
        }
        try
        {
            await using var context = new DealershipDbContext(options);
            var service = new CustomerAccountService(context, configuration, new TestEmailSender(), new PasswordWorkService());
            Assert.True(await service.VerifyEmailAsync(raw, CancellationToken.None));
            Assert.False(await service.VerifyEmailAsync(raw, CancellationToken.None));
            Assert.False(await service.VerifyEmailAsync(expiredRaw, CancellationToken.None));
        }
        finally { await CleanupAsync(options, userId); }
    }

    [Fact]
    public async Task Successful_reset_invalidates_pending_reset_tokens_and_revokes_sessions()
    {
        // PostgresGuard.Resolve() skips (or fails if REQUIRE_POSTGRES=true) when no DB is configured.
        var connectionString = PostgresGuard.Resolve();
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(connectionString).Options;
        var configuration = TestConfiguration();
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var pendingRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        Guid userId;
        await using (var setup = new DealershipDbContext(options))
        {
            var role = await setup.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
            var user = new User { Email = $"reset-{Guid.NewGuid():N}@test.invalid", AccountType = AccountType.Customer };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Original-password-123");
            user.Roles.Add(new UserRole { User = user, Role = role });
            setup.Users.Add(user);
            setup.OneTimeTokens.AddRange(
                Token(user, raw, OneTimeTokenPurpose.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(20), configuration),
                Token(user, pendingRaw, OneTimeTokenPurpose.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(20), configuration));
            setup.RefreshTokens.Add(new RefreshToken { User = user, FamilyId = Guid.NewGuid(), TokenHash = "session-hash", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            await setup.SaveChangesAsync(); userId = user.Id;
        }
        try
        {
            await using (var context = new DealershipDbContext(options))
            {
                var service = new CustomerAccountService(context, configuration, new TestEmailSender(), new PasswordWorkService());
                Assert.True(await service.ResetPasswordAsync(raw, "Replacement-password-123", CancellationToken.None));
                Assert.False(await service.ResetPasswordAsync(raw, "Replacement-password-123", CancellationToken.None));
                Assert.False(await service.ResetPasswordAsync(pendingRaw, "Replacement-password-123", CancellationToken.None));
            }
            await using var verify = new DealershipDbContext(options);
            Assert.All(await verify.OneTimeTokens.Where(x => x.UserId == userId).ToListAsync(), token => Assert.NotNull(token.UsedAt));
            Assert.All(await verify.RefreshTokens.Where(x => x.UserId == userId).ToListAsync(), session => Assert.NotNull(session.RevokedAt));
        }
        finally { await CleanupAsync(options, userId); }
    }

    [Theory]
    [InlineData(OneTimeTokenPurpose.EmailVerification)]
    [InlineData(OneTimeTokenPurpose.PasswordReset)]
    public async Task Same_one_time_token_allows_exactly_one_concurrent_consumer(OneTimeTokenPurpose purpose)
    {
        // PostgresGuard.Resolve() skips (or fails if REQUIRE_POSTGRES=true) when no DB is configured.
        var connectionString = PostgresGuard.Resolve();
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(connectionString).Options;
        var configuration = TestConfiguration();
        var suffix = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!), Encoding.UTF8.GetBytes(raw)));
        Guid userId;
        await using (var setup = new DealershipDbContext(options))
        {
            var role = await setup.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
            var user = new User { Email = $"token-{suffix}@test.invalid", AccountType = AccountType.Customer, EmailVerified = false };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Original-password-123");
            user.Roles.Add(new UserRole { User = user, Role = role });
            setup.Users.Add(user);
            setup.OneTimeTokens.Add(new OneTimeToken { User = user, Purpose = purpose, TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            await setup.SaveChangesAsync();
            userId = user.Id;
        }
        try
        {
            async Task<bool> ConsumeAsync()
            {
                await using var context = new DealershipDbContext(options);
                var service = new CustomerAccountService(context, configuration, new TestEmailSender(), new PasswordWorkService());
                return purpose == OneTimeTokenPurpose.EmailVerification
                    ? await service.VerifyEmailAsync(raw, CancellationToken.None)
                    : await service.ResetPasswordAsync(raw, "Replacement-password-123", CancellationToken.None);
            }
            var results = await Task.WhenAll(ConsumeAsync(), ConsumeAsync());
            Assert.Equal(1, results.Count(x => x));
            await using var verify = new DealershipDbContext(options);
            var stored = await verify.OneTimeTokens.SingleAsync(x => x.UserId == userId);
            Assert.NotNull(stored.UsedAt);
            if (purpose == OneTimeTokenPurpose.EmailVerification) Assert.True((await verify.Users.SingleAsync(x => x.Id == userId)).EmailVerified);
        }
        finally
        {
            await using var cleanup = new DealershipDbContext(options);
            cleanup.OneTimeTokens.RemoveRange(cleanup.OneTimeTokens.Where(x => x.UserId == userId));
            cleanup.Users.RemoveRange(cleanup.Users.Where(x => x.Id == userId));
            await cleanup.SaveChangesAsync();
        }
    }

    private static IConfiguration TestConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "customer-token-postgres-test-key-that-is-long-enough", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build();
    private static OneTimeToken Token(User user, string raw, OneTimeTokenPurpose purpose, DateTimeOffset expiresAt, IConfiguration configuration) => new() { User = user, Purpose = purpose, TokenHash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!), Encoding.UTF8.GetBytes(raw))), ExpiresAt = expiresAt };
    private static async Task CleanupAsync(DbContextOptions<DealershipDbContext> options, Guid userId)
    {
        await using var cleanup = new DealershipDbContext(options);
        cleanup.OneTimeTokens.RemoveRange(cleanup.OneTimeTokens.Where(x => x.UserId == userId));
        cleanup.RefreshTokens.RemoveRange(cleanup.RefreshTokens.Where(x => x.UserId == userId));
        cleanup.Users.RemoveRange(cleanup.Users.Where(x => x.Id == userId));
        await cleanup.SaveChangesAsync();
    }

    private sealed class TestEmailSender : IEmailSender { public bool IsAvailable => true; public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask; }
}
