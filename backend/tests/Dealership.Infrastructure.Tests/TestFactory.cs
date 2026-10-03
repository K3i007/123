using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Dealership.Infrastructure;
using Dealership.Application;
using Xunit;

namespace Dealership.Infrastructure.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// PostgreSQL skip guard
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Centralised helper to resolve the PostgreSQL connection string and enforce
/// REQUIRE_POSTGRES in CI.
/// </summary>
public static class PostgresGuard
{
    /// <summary>
    /// Returns ConnectionStrings__Default from the environment.
    /// If absent and REQUIRE_POSTGRES=true → throws (CI build failure).
    /// If absent otherwise → Assert.Skip with explicit reason.
    /// </summary>
    public static string Resolve()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;

        var require = string.Equals(
            Environment.GetEnvironmentVariable("REQUIRE_POSTGRES"), "true",
            StringComparison.OrdinalIgnoreCase);

        const string reason =
            "ConnectionStrings__Default not set — this test requires a real PostgreSQL instance. " +
            "Set REQUIRE_POSTGRES=true to turn skips into CI build failures.";

        if (require) throw new InvalidOperationException(reason);
        // In xUnit 2, throw SkipException (Xunit.Sdk namespace) to produce a Skipped result.
        // We use reflection to avoid constructor-signature issues between minor xUnit versions.
        var skipExType = Type.GetType(
            "Xunit.Sdk.SkipException, xunit.assert",
            throwOnError: false)
            ?? typeof(Xunit.Sdk.XunitException).Assembly.GetType("Xunit.Sdk.SkipException")!;
        throw (Exception)skipExType.GetConstructors()[0].Invoke([reason]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Base PostgreSQL-backed WebApplicationFactory
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that runs the API against
/// the PostgreSQL database specified by <c>ConnectionStrings__Default</c>.
///
/// Unlike creating a separate test database (which requires CREATEDB privilege),
/// this factory reuses the existing database.  Test isolation is achieved by:
/// <list type="bullet">
///   <item>Using unique UUID-based emails, names and keys in every test.</item>
///   <item>Each test class performs its own cleanup in <c>DisposeAsync</c>
///         so it never leaves orphaned rows.</item>
/// </list>
///
/// The factory never modifies the schema — it relies on the existing migrations
/// already applied to the target database.
/// </summary>
public class PostgresTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected string ConnectionString { get; private set; } = string.Empty;

    // IAsyncLifetime.InitializeAsync — called by xUnit before any test in the collection.
    async Task IAsyncLifetime.InitializeAsync()
    {
        ConnectionString = PostgresGuard.Resolve();
        // Verify connectivity and that migrations have been applied.
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new DealershipDbContext(options);
        // EnsureCreated would wipe migrations; use a simple connectivity check instead.
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect) throw new InvalidOperationException(
            $"Cannot connect to PostgreSQL at '{ConnectionString}'. " +
            "Run 'dotnet ef database update' before running tests.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Security:AccountHashKey",
            "this-is-a-test-key-for-account-hash-which-must-be-long-enough");
        builder.UseSetting("Frontend:Origins:0", "http://localhost:3000");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        // Raise IP-bucket rate limits so sequential test requests from the same
        // IP do not trigger 429 across unrelated tests.
        builder.UseSetting("RateLimiting:CustomerLoginPermitLimit", "1000");
        builder.UseSetting("RateLimiting:CustomerAccountPermitLimit", "1000");
        builder.UseSetting("RateLimiting:CustomerIpPermitLimit", "1000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DealershipDbContext>>();
            services.AddDbContext<DealershipDbContext>(o => o.UseNpgsql(ConnectionString));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, CaptureEmailSender>();
            // Use 1 PBKDF2 iteration in tests — ~600x faster than the production 600 000.
            // Security properties are irrelevant for integration tests; speed matters.
            services.Configure<PasswordHasherOptions>(o => o.IterationCount = 1);
            // Replace the async BackgroundTaskQueue with one that executes tasks
            // synchronously so that emails are captured before assertions run.
            services.RemoveAll<IBackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue, SynchronousTaskQueue>();
        });
    }

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}

// ─────────────────────────────────────────────────────────────────────────────
// Backward-compat alias — existing [IClassFixture<TestFactory>] compile unchanged
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// HTTP integration tests declare <c>[IClassFixture&lt;TestFactory&gt;]</c>
/// and automatically run against a real PostgreSQL database.
/// </summary>
public sealed class TestFactory : PostgresTestFactory { }

// ─────────────────────────────────────────────────────────────────────────────
// InMemory factory — for Application-layer unit tests only (no JSONB, no ILike)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Use only in tests that do not require PostgreSQL-specific features.
/// HTTP integration tests must use <see cref="TestFactory"/> instead.
/// </summary>
public sealed class InMemoryTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"dealership-inmemory-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Security:AccountHashKey",
            "this-is-a-test-key-for-account-hash-which-must-be-long-enough");
        builder.UseSetting("Frontend:Origins:0", "http://localhost:3000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DealershipDbContext>>();
            services.AddDbContext<DealershipDbContext>(o =>
                o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, CaptureEmailSender>();
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Email stub
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// In-memory email sender. Always available. Captures the last sent message
/// so tests can inspect verification links, reset tokens, etc.
/// </summary>
public sealed class CaptureEmailSender : IEmailSender
{
    public bool IsAvailable => true;
    public string LastRecipient { get; private set; } = string.Empty;
    public string LastSubject   { get; private set; } = string.Empty;
    public string LastBody      { get; private set; } = string.Empty;

    public Task SendAsync(
        string recipient, string subject, string body,
        CancellationToken cancellationToken = default)
    {
        LastRecipient = recipient;
        LastSubject   = subject;
        LastBody      = body;
        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Synchronous background queue
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Replacement for <see cref="BackgroundTaskQueue"/> in the test environment.
/// Executes queued work items immediately (synchronously) on the calling thread
/// so that side-effects such as emails are observable before assertions run.
/// </summary>
internal sealed class SynchronousTaskQueue : IBackgroundTaskQueue
{
    public async ValueTask QueueAsync(
        Func<CancellationToken, Task> workItem,
        CancellationToken cancellationToken = default)
        => await workItem(cancellationToken);
}
