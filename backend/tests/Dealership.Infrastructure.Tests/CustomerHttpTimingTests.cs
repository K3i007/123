using System.Diagnostics;
using System.Net.Http.Json;
using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Dealership.Infrastructure.Tests;

/// <summary>
/// HTTP-level timing and security tests that REQUIRE PostgreSQL.
/// All tests call <see cref="PostgresGuard.Resolve"/> at construction time; if no database is
/// configured they are marked Skipped (or fail the build when REQUIRE_POSTGRES=true).
/// </summary>
[Collection("Public catalog HTTP")]
[Trait("Category", "PostgresRequired")]
public sealed class CustomerHttpTimingTests(TestFactory factory, ITestOutputHelper output) : IAsyncLifetime, IClassFixture<TestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private string _existingEmail = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        _existingEmail = $"timing-{suffix}@test.invalid";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordWorkService>();
        var role = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
        var user = new User { Email = _existingEmail, AccountType = AccountType.Customer };
        user.PasswordHash = passwords.Hash(user, "Existing-password-123!");
        user.Roles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await db.Users.Where(x => x.Email == _existingEmail).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (1) N+1 timing: existing vs unknown email — HTTP level, PostgreSQL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Measures 30+ sequential login attempts for each of an existing and an
    /// unknown email, then asserts the medians and p90 values differ by less
    /// than 10%.  Also prints raw measurements.
    /// </summary>
    [Fact]
    public async Task Login_timing_for_existing_and_unknown_emails_is_indistinguishable()
    {
        const int Samples = 32;
        var unknown = $"timing-unknown-{Guid.NewGuid():N}@test.invalid";
        var payload = (string email) => new { email, password = "wrong-password-999!" };

        // Warm-up: 3 rounds, results discarded
        for (var i = 0; i < 3; i++)
        {
            using var r1 = await _client.PostAsJsonAsync("/api/v1/customers/login", payload(_existingEmail));
            using var r2 = await _client.PostAsJsonAsync("/api/v1/customers/login", payload(unknown));
        }

        var existingMs = new List<double>(Samples);
        var unknownMs  = new List<double>(Samples);

        // Interleave requests so scheduling noise cancels out
        for (var i = 0; i < Samples; i++)
        {
            var sw = Stopwatch.StartNew();
            using var r1 = await _client.PostAsJsonAsync("/api/v1/customers/login", payload(_existingEmail));
            existingMs.Add(sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            using var r2 = await _client.PostAsJsonAsync("/api/v1/customers/login", payload(unknown));
            unknownMs.Add(sw.Elapsed.TotalMilliseconds);
        }

        existingMs.Sort();
        unknownMs.Sort();

        var existingMedian = Median(existingMs);
        var unknownMedian  = Median(unknownMs);
        var existingP90    = Percentile(existingMs, 90);
        var unknownP90     = Percentile(unknownMs, 90);

        output.WriteLine("=== N+1 HTTP Login Timing (PostgreSQL) ===");
        output.WriteLine($"  Existing  median={existingMedian:F1}ms  p90={existingP90:F1}ms");
        output.WriteLine($"  Unknown   median={unknownMedian:F1}ms   p90={unknownP90:F1}ms");
        output.WriteLine($"  Raw existing (ms): {string.Join(", ", existingMs.Select(v => $"{v:F0}"))}");
        output.WriteLine($"  Raw unknown  (ms): {string.Join(", ", unknownMs.Select(v => $"{v:F0}"))}");

        var medianDiff = RelativeDiff(existingMedian, unknownMedian);
        var p90Diff    = RelativeDiff(existingP90, unknownP90);
        output.WriteLine($"  Median relative diff={medianDiff:P1}  P90 relative diff={p90Diff:P1}");

        Assert.True(medianDiff <= 0.10,
            $"Medians differ by {medianDiff:P1} (existing={existingMedian:F1}ms unknown={unknownMedian:F1}ms); max 10%.");
        Assert.True(p90Diff <= 0.10,
            $"P90s differ by {p90Diff:P1} (existing={existingP90:F1}ms unknown={unknownP90:F1}ms); max 10%.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (5) 429 + Retry-After with a test-specific low limit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the customer-login IP rate-limit returns 429 with a
    /// <c>Retry-After</c> header once the permit limit is exhausted.
    /// Uses a dedicated <see cref="PostgresTestFactory"/> with PermitLimit = 4
    /// so the test is not affected by the global test-suite bucket.
    /// </summary>
    [Fact]
    public async Task Rate_limit_returns_429_with_retry_after_header()
    {
        // Spin up a factory with a very low permit limit dedicated to this test.
        await using var lowLimitFactory = new LowLimitTestFactory();
        await ((IAsyncLifetime)lowLimitFactory).InitializeAsync();
        using var client = lowLimitFactory.CreateClient();

        var email = $"ratelimit-{Guid.NewGuid():N}@test.invalid";
        var payload = new { email, password = "wrong-password-1!" };

        HttpResponseMessage? lastResponse = null;
        var statusCodes = new List<System.Net.HttpStatusCode>();

        for (var i = 0; i < 10; i++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.PostAsJsonAsync("/api/v1/customers/login", payload);
            statusCodes.Add(lastResponse.StatusCode);
            if (lastResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests) break;
        }

        output.WriteLine($"Status sequence: {string.Join(", ", statusCodes)}");

        Assert.True(
            statusCodes.Contains(System.Net.HttpStatusCode.TooManyRequests),
            $"Expected a 429 TooManyRequests within 10 attempts; got: {string.Join(", ", statusCodes)}");

        Assert.NotNull(lastResponse);
        Assert.True(
            lastResponse.Headers.Contains("Retry-After"),
            "Response must include Retry-After header on 429.");

        output.WriteLine($"Retry-After: {string.Join(",", lastResponse.Headers.GetValues("Retry-After"))}");
        lastResponse.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (7) Progressive delay cap and reset after correct login
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that:
    /// <list type="bullet">
    ///   <item>After N wrong attempts the login delay is capped (does not grow indefinitely).</item>
    ///   <item>After a correct login the delay counter resets: the next wrong attempt from
    ///         the same email incurs no more than the first-attempt baseline.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Progressive_delay_is_capped_and_resets_after_successful_login()
    {
        // Use a factory with a very low delay cap so the test is fast.
        await using var capFactory = new LowDelayCapTestFactory();
        await ((IAsyncLifetime)capFactory).InitializeAsync();
        using var client = capFactory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N");
        var email = $"delay-cap-{suffix}@test.invalid";
        const string correctPassword = "Cap-test-password-1!";

        // Register the user directly in the DB so there's no registration overhead.
        await using var scope = capFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordWorkService>();
        var role = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
        var user = new User { Email = email, AccountType = AccountType.Customer };
        user.PasswordHash = passwords.Hash(user, correctPassword);
        user.Roles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 10 wrong attempts — the delay should cap at LowDelayCapTestFactory.DelayCapMs.
        for (var i = 0; i < 10; i++)
        {
            using var r = await client.PostAsJsonAsync("/api/v1/customers/login", new { email, password = "wrong!" });
        }

        var sw = Stopwatch.StartNew();
        using var cappedRes = await client.PostAsJsonAsync("/api/v1/customers/login", new { email, password = "wrong!" });
        sw.Stop();
        var delayAfter10 = sw.Elapsed.TotalMilliseconds;
        output.WriteLine($"  Delay after 10 wrong attempts: {delayAfter10:F0}ms (cap={LowDelayCapTestFactory.DelayCapMs}ms)");
        // Must be capped — not 10× the base 200ms = 2000ms.
        Assert.True(delayAfter10 < LowDelayCapTestFactory.DelayCapMs + 600,
            $"Delay {delayAfter10:F0}ms exceeded cap {LowDelayCapTestFactory.DelayCapMs}ms + 600ms grace.");

        // Now login correctly — counter resets.
        using var successRes = await client.PostAsJsonAsync("/api/v1/customers/login", new { email, password = correctPassword });
        Assert.Equal(System.Net.HttpStatusCode.OK, successRes.StatusCode);

        // Next wrong attempt must have near-zero delay (counter was reset).
        sw.Restart();
        using var afterResetRes = await client.PostAsJsonAsync("/api/v1/customers/login", new { email, password = "wrong!" });
        sw.Stop();
        output.WriteLine($"  Delay after reset (1 wrong attempt): {sw.Elapsed.TotalMilliseconds:F0}ms");
        Assert.True(sw.Elapsed.TotalMilliseconds < 600,
            $"Expected near-zero delay after reset, got {sw.Elapsed.TotalMilliseconds:F0}ms (should be < 600ms).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0
            : sorted[sorted.Count / 2];

    private static double Percentile(List<double> sorted, int percentile)
    {
        var idx = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(idx, sorted.Count - 1))];
    }

    private static double RelativeDiff(double a, double b) =>
        Math.Abs(a - b) / Math.Max(a, b);

    // ─────────────────────────────────────────────────────────────────────────
    // Inner factories with test-specific configuration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>PostgreSQL factory with IP rate limit = 4 for 429 test.</summary>
    private sealed class LowLimitTestFactory : PostgresTestFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:CustomerLoginPermitLimit", "4");
        }
    }

    /// <summary>PostgreSQL factory with a 400 ms delay cap for the reset test.</summary>
    private sealed class LowDelayCapTestFactory : PostgresTestFactory
    {
        public const int DelayCapMs = 400;
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Security:ProgressiveDelayMaxMilliseconds", DelayCapMs.ToString());
        }
    }
}
