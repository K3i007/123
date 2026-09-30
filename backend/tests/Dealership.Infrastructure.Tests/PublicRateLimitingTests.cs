using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Dealership.Infrastructure.Tests;

public sealed class PublicRateLimitingTests : IClassFixture<PublicRateLimitingTests.TestFactory>
{
    private readonly HttpClient _client;

    public PublicRateLimitingTests(TestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_Endpoints_Reject_Requests_When_Limit_Exceeded()
    {
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await _client.GetAsync("/api/v1/public/branches");
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }

    public sealed class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("RateLimiting:PublicCatalogPermitLimit", "3");
        }
    }
}
