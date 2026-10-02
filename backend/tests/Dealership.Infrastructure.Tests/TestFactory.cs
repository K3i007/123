using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Dealership.Infrastructure;
using Dealership.Application;

namespace Dealership.Infrastructure.Tests;

/// <summary>
/// Always-available email sender for the test environment.
/// Stores the last sent body for assertion.
/// </summary>
public sealed class AlwaysAvailableEmailSender : IEmailSender
{
    public bool IsAvailable => true;
    public string LastRecipient { get; private set; } = string.Empty;
    public string LastSubject { get; private set; } = string.Empty;
    public string LastBody { get; private set; } = string.Empty;

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        LastRecipient = recipient;
        LastSubject = subject;
        LastBody = body;
        return Task.CompletedTask;
    }
}

public class TestFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"dealership-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Security:AccountHashKey", "this-is-a-test-key-for-account-hash-which-must-be-long-enough");
        builder.UseSetting("Frontend:Origins:0", "http://localhost:3000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DealershipDbContext>>();
            services.AddDbContext<DealershipDbContext>(options => options.UseInMemoryDatabase(databaseName));
            // Replace the DevelopmentEmailSender (which is not available in Testing) with a test double.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, AlwaysAvailableEmailSender>();
        });
    }
}