using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Dealership.Infrastructure.Tests;

public sealed class PublicRateLimitingTests : IClassFixture<PublicRateLimitingTests.LocalTestFactory>
{
    private readonly HttpClient _client;

    public PublicRateLimitingTests(LocalTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    public sealed class LocalTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("RateLimiting:PublicCatalogPermitLimit", "3");
        }
    }

    [Fact]
    public async Task Public_Endpoints_Reject_Requests_When_Limit_Exceeded()
    {
        // Each request presents a different fake address. It would evade a limiter that trusted it.
        for (var i = 0; i < 3; i++)
        {
            using var response = await SendWithSpoofedForwardedAddressAsync($"198.51.100.{i + 1}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejected = await SendWithSpoofedForwardedAddressAsync("203.0.113.101");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), rejected.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpResponseMessage> SendWithSpoofedForwardedAddressAsync(string address)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/public/branches");
        request.Headers.Add("X-Forwarded-For", address);
        return await _client.SendAsync(request);
    }

}
