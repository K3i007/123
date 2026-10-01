using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Dealership.Infrastructure.Tests;

[Collection("Public catalog HTTP")]
public sealed class CustomerNoEnumerationHttpTests(WebApplicationFactory<Program> factory) : IAsyncLifetime, IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = factory.CreateClient();
    private string existing = string.Empty;

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N"); existing = $"enumeration-{suffix}@test.invalid";
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
        var user = new User { Email = existing, AccountType = AccountType.Customer };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Existing-password-123"); user.Roles.Add(new UserRole { User = user, Role = role }); db.Users.Add(user); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>(); db.Users.RemoveRange(db.Users.Where(x => x.Email == existing)); await db.SaveChangesAsync(); }

    [Fact]
    public async Task Existing_and_unknown_emails_have_equivalent_public_responses()
    {
        var unknown = $"unknown-{Guid.NewGuid():N}@test.invalid";
        await AssertEquivalentAsync("/api/v1/customers/register", new { email = existing, password = "New-password-123", name = "One", privacyPolicyVersion = "test" }, new { email = unknown, password = "New-password-123", name = "Two", privacyPolicyVersion = "test" });
        await AssertEquivalentAsync("/api/v1/customers/login", new { email = existing, password = "wrong-password-123" }, new { email = unknown, password = "wrong-password-123" });
        await AssertEquivalentAsync("/api/v1/customers/password-reset", new { email = existing }, new { email = unknown });
    }

    [Fact]
    public async Task Account_limit_returns_the_same_n_plus_one_sequence_for_existing_and_unknown_emails()
    {
        var unknown = $"limit-{Guid.NewGuid():N}@test.invalid";
        await AssertNPlusOneEquivalentAsync("/api/v1/customers/login", email => new { email, password = "wrong-password-123" }, unknown);
        await AssertNPlusOneEquivalentAsync("/api/v1/customers/password-reset", email => new { email }, unknown);
        await AssertNPlusOneEquivalentAsync("/api/v1/customers/register", email => new { email, password = "New-password-123", name = "One", privacyPolicyVersion = "test" }, unknown);
    }

    private async Task AssertEquivalentAsync(string path, object existingPayload, object unknownPayload)
    {
        using var existingResponse = await client.PostAsJsonAsync(path, existingPayload); using var unknownResponse = await client.PostAsJsonAsync(path, unknownPayload);
        Assert.Equal(existingResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(await existingResponse.Content.ReadAsStringAsync(), await unknownResponse.Content.ReadAsStringAsync());
        Assert.Equal(existingResponse.Content.Headers.ContentType?.ToString(), unknownResponse.Content.Headers.ContentType?.ToString());
        AssertSecurityHeadersEqual(existingResponse, unknownResponse);
    }

    private async Task AssertNPlusOneEquivalentAsync(string path, Func<string, object> payload, string unknown)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var knownResponse = await client.PostAsJsonAsync(path, payload(existing));
            using var unknownResponse = await client.PostAsJsonAsync(path, payload(unknown));
            Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
            Assert.Equal(await knownResponse.Content.ReadAsStringAsync(), await unknownResponse.Content.ReadAsStringAsync());
            Assert.Equal(knownResponse.Content.Headers.ContentType?.ToString(), unknownResponse.Content.Headers.ContentType?.ToString());
            AssertSecurityHeadersEqual(knownResponse, unknownResponse);
        }
    }

    private static void AssertSecurityHeadersEqual(HttpResponseMessage first, HttpResponseMessage second)
    {
        foreach (var header in new[] { "Set-Cookie", "Retry-After", "RateLimit-Limit", "RateLimit-Remaining", "RateLimit-Reset", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset" })
        {
            Assert.Equal(HeaderValues(first, header), HeaderValues(second, header));
        }
    }

    private static string[] HeaderValues(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.OrderBy(value => value, StringComparer.Ordinal).ToArray() : [];
}
