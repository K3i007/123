using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Domain;
using Dealership.Application;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Dealership.Infrastructure.Tests;

[Collection("Public catalog HTTP")]
public sealed class CustomerNoEnumerationHttpTests(TestFactory factory) : IAsyncLifetime, IClassFixture<TestFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private string existing = string.Empty;

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N"); existing = $"enumeration-{suffix}@test.invalid";
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordWorkService>();
        var role = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
        var user = new User { Email = existing, AccountType = AccountType.Customer };
        user.PasswordHash = passwords.Hash(user, "Existing-password-123"); user.Roles.Add(new UserRole { User = user, Role = role }); db.Users.Add(user); await db.SaveChangesAsync();
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

    [Fact]
    public async Task Victim_can_login_even_after_brute_force_attempts_from_other_ips()
    {
        var victimEmail = $"victim-{Guid.NewGuid():N}@test.invalid";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordWorkService>();
        var role = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer);
        var user = new User { Email = victimEmail, AccountType = AccountType.Customer };
        user.PasswordHash = passwords.Hash(user, "CorrectPassword123!");
        user.Roles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 10 attempts from different IPs (attacker)
        for (var i = 0; i < 10; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/login");
            req.Headers.Add("X-Forwarded-For", $"10.0.0.{i}");
            req.Content = JsonContent.Create(new { email = victimEmail, password = "wrong-password" });
            using var res = await client.SendAsync(req);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // Victim logs in from their own IP
        using var victimReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/login");
        victimReq.Headers.Add("X-Forwarded-For", "192.168.100.1");
        victimReq.Content = JsonContent.Create(new { email = victimEmail, password = "CorrectPassword123!" });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var victimRes = await client.SendAsync(victimReq);
        sw.Stop();

        Assert.Equal(System.Net.HttpStatusCode.OK, victimRes.StatusCode);
        // Delay must be at least the maximum delay (e.g. 1 second) because of the 10 previous attempts!
        Assert.True(sw.ElapsedMilliseconds > 800, "Should have experienced progressive delay.");
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
            var ip = $"192.168.1.{attempt}";
            using var knownReq = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload(existing)) };
            knownReq.Headers.Add("X-Forwarded-For", ip);
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            using var knownResponse = await client.SendAsync(knownReq);
            sw1.Stop();
            var knownBody = await knownResponse.Content.ReadAsStringAsync();
            
            using var unknownReq = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload(unknown)) };
            unknownReq.Headers.Add("X-Forwarded-For", ip);
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            using var unknownResponse = await client.SendAsync(unknownReq);
            sw2.Stop();
            var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
            Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
            Assert.Equal(knownBody, unknownBody);
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
