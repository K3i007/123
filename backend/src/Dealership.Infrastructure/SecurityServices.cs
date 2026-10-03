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
    private readonly PasswordHasher<User> hasher;
    // Created once by the singleton; every unknown-account login performs the same PBKDF2 work.
    private readonly string unknownHash;

    public PasswordWorkService() : this(Options.Create(new PasswordHasherOptions { IterationCount = OwaspIterationCount })) { }

    public PasswordWorkService(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<User>(options);
        unknownHash = hasher.HashPassword(new User(), "unknown-account-placeholder");
    }

    public int IterationCount => hasher.HashPassword(new User(), "x").Length > 0
        ? BinaryPrimitives.ReadInt32BigEndian(Convert.FromBase64String(unknownHash).AsSpan(5, 4))
        : OwaspIterationCount;
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

    /// <summary>
    /// Per-account attempt limit before "register" and "reset" paths return false.
    /// Login is never hard-blocked — only delayed — to prevent denial-of-service via lockout.
    /// Default: 3.
    /// </summary>
    private readonly int permitLimit = configuration.GetValue("Security:AccountAttemptLimit", 3);

    /// <summary>
    /// Cap on the progressive delay in milliseconds.
    /// Formula: min(200 × (attempt − 1), cap).
    /// Default: 2000 ms (2 s).  The per-account counter resets on a successful login.
    /// </summary>
    private readonly int maximumDelayMilliseconds = configuration.GetValue("Security:ProgressiveDelayMaxMilliseconds", 2_000);

    private readonly byte[] key = Encoding.UTF8.GetBytes(
        configuration["Security:AccountHashKey"]
        ?? (environment.IsDevelopment()
            ? "development-only-account-hash-key-change-me"
            : throw new InvalidOperationException("Security:AccountHashKey is required outside Development.")));

    public async Task<bool> AllowAsync(string purpose, string normalizedEmail, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalizedEmail)));
        var now = DateTimeOffset.UtcNow;

        // Remove expired counters. Always uses ExecuteDeleteAsync because this service
        // requires a relational (PostgreSQL) database in all deployed environments.
        await db.AccountRateLimits.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        // Prune oldest counters if the table has grown too large (flood-protection).
        var activeCounters = await db.AccountRateLimits.CountAsync(cancellationToken);
        if (activeCounters >= MaximumActiveCounters)
        {
            var oldest = await db.AccountRateLimits
                .OrderBy(x => x.ExpiresAt).Take(100)
                .Select(x => x.Id).ToListAsync(cancellationToken);
            await db.AccountRateLimits.Where(x => oldest.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var entry = await db.AccountRateLimits.SingleOrDefaultAsync(
            x => x.Purpose == purpose && x.AccountHash == hash, cancellationToken);
        var attempts = 1;
        if (entry is null)
        {
            db.AccountRateLimits.Add(new AccountRateLimit
            {
                Purpose = purpose, AccountHash = hash, AttemptCount = 1,
                ExpiresAt = now.AddMinutes(10)
            });
        }
        else
        {
            entry.AttemptCount++;
            attempts = entry.AttemptCount;
        }
        await db.SaveChangesAsync(cancellationToken);

        // Progressive delay: 200 ms × (attempt − 1), capped at maximumDelayMilliseconds.
        // The delay is applied for every attempt after the first to increase friction for
        // brute-force attacks without permanently locking legitimate accounts.
        if (attempts > 1 && maximumDelayMilliseconds > 0)
        {
            var delayMs = Math.Min(200 * (attempts - 1), maximumDelayMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }

        // Login is never hard-blocked; the delay is the only friction mechanism.
        if (purpose == "login") return true;
        return attempts <= permitLimit;
    }

    /// <summary>
    /// Deletes the attempt counter for the given purpose + email after a successful login.
    /// This prevents a previous burst of failed attempts from penalising the next
    /// honest login attempt with an unnecessary progressive delay.
    /// </summary>
    public async Task ResetAsync(string purpose, string normalizedEmail, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalizedEmail)));
        await db.AccountRateLimits
            .Where(x => x.Purpose == purpose && x.AccountHash == hash)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
