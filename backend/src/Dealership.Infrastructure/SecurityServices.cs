using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using Dealership.Application;
using Dealership.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure;

public sealed class PasswordWorkService : IPasswordWorkService
{
    public const int OwaspIterationCount = 600_000;
    private readonly PasswordHasher<User> hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = OwaspIterationCount }));
    // Created once by the singleton; every unknown-account login performs the same PBKDF2 work.
    private readonly string unknownHash;
    public PasswordWorkService() => unknownHash = hasher.HashPassword(new User(), "unknown-account-placeholder");
    public int IterationCount => OwaspIterationCount;
    // ASP.NET Identity's v3 payload stores the PBKDF2 iteration count at byte offset 5.
    public int GetIterationCount(string hash) => BinaryPrimitives.ReadInt32BigEndian(Convert.FromBase64String(hash).AsSpan(5, 4));
    public string UnknownHashForTesting => unknownHash;
    public string Hash(User user, string password) => hasher.HashPassword(user, password);
    public bool Verify(User user, string hash, string password) => hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
    public bool VerifyUnknown(string password) => hasher.VerifyHashedPassword(new User(), unknownHash, password) != PasswordVerificationResult.Failed;
}

public sealed class AccountRateLimitService(DealershipDbContext db, IConfiguration configuration, IHostEnvironment environment) : IAccountRateLimitService
{
    private const int MaximumActiveCounters = 10_000;
    private readonly int permitLimit = configuration.GetValue("Security:AccountAttemptLimit", 3);
    private readonly int maximumDelayMilliseconds = configuration.GetValue("Security:ProgressiveDelayMaxMilliseconds", 1_000);
    private readonly byte[] key = Encoding.UTF8.GetBytes(configuration["Security:AccountHashKey"] ?? (environment.IsDevelopment() ? "development-only-account-hash-key-change-me" : throw new InvalidOperationException("Security:AccountHashKey is required outside Development.")));
    public async Task<bool> AllowAsync(string purpose, string normalizedEmail, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalizedEmail)));
        var now = DateTimeOffset.UtcNow;
        var expired = db.AccountRateLimits.Where(x => x.ExpiresAt <= now);
        if (db.Database.IsRelational()) await expired.ExecuteDeleteAsync(cancellationToken);
        else
        {
            db.AccountRateLimits.RemoveRange(await expired.ToListAsync(cancellationToken));
            await db.SaveChangesAsync(cancellationToken);
        }
        // This table is deliberately a short-lived cache, not an event ledger. Prune before
        // accepting a new account key so an attacker cannot retain an unbounded key space.
        var activeCounters = await db.AccountRateLimits.CountAsync(cancellationToken);
        if (activeCounters >= MaximumActiveCounters)
        {
            var oldest = await db.AccountRateLimits.OrderBy(x => x.ExpiresAt).Take(100).Select(x => x.Id).ToListAsync(cancellationToken);
            var oldestCounters = db.AccountRateLimits.Where(x => oldest.Contains(x.Id));
            if (db.Database.IsRelational()) await oldestCounters.ExecuteDeleteAsync(cancellationToken);
            else db.AccountRateLimits.RemoveRange(await oldestCounters.ToListAsync(cancellationToken));
        }
        var entry = await db.AccountRateLimits.SingleOrDefaultAsync(x => x.Purpose == purpose && x.AccountHash == hash, cancellationToken);
        var attempts = 1;
        if (entry is null)
        {
            db.AccountRateLimits.Add(new AccountRateLimit { Purpose = purpose, AccountHash = hash, AttemptCount = 1, ExpiresAt = now.AddMinutes(10) });
        }
        else
        {
            entry.AttemptCount++;
            attempts = entry.AttemptCount;
        }
        await db.SaveChangesAsync(cancellationToken);
        // A small, capped delay is applied before the generic response for every account hash.
        // It increases friction without leaving a durable account lockout state.
        if (attempts > 1 && maximumDelayMilliseconds > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(200 * (attempts - 1), maximumDelayMilliseconds)), cancellationToken);
        }
        
        if (purpose == "login") return true;
        return attempts <= permitLimit;
    }
}
