using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Dealership.Infrastructure;
namespace Dealership.Infrastructure.Tests;
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
        });
    }
}
