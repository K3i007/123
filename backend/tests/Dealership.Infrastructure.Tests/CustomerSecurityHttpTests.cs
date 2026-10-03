using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Api;
using Dealership.Domain;
using Dealership.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Concurrent;

namespace Dealership.Infrastructure.Tests;

public sealed class TestLoggerProvider : ILoggerProvider
{
    public readonly ConcurrentQueue<string> Messages = new();
    public ILogger CreateLogger(string categoryName) => new TestLogger(this);
    public void Dispose() {}

    private class TestLogger(TestLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            provider.Messages.Enqueue(formatter(state, exception));
        }
    }
}

[CollectionDefinition("Customer Security HTTP", DisableParallelization = true)]
public sealed class CustomerSecurityHttpCollection;

[Collection("Customer Security HTTP")]
public sealed class CustomerSecurityHttpTests(TestFactory factory) : IAsyncLifetime, IClassFixture<TestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private Guid _customerA;
    private Guid _customerB;
    private Guid _staffUser;
    private Guid _testVehicleId;
    private string _tokenA = string.Empty;
    private string _tokenB = string.Empty;
    private string _tokenStaff = string.Empty;
    private Guid _sessionB;

    public async Task InitializeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordWorkService>();
        
        var suffix = Guid.NewGuid().ToString("N");
        var emailA = $"customer-a-{suffix}@test.local";
        var emailB = $"customer-b-{suffix}@test.local";
        var emailStaff = $"staff-a-{suffix}@test.local";

        var cA = new User { Email = emailA, AccountType = AccountType.Customer, EmailVerified = true };
        cA.PasswordHash = passwords.Hash(cA, "StrongPassword123!");
        
        var cB = new User { Email = emailB, AccountType = AccountType.Customer, EmailVerified = true };
        cB.PasswordHash = passwords.Hash(cB, "StrongPassword123!");
        
        var staff = new User { Email = emailStaff, AccountType = AccountType.Staff, EmailVerified = true };
        staff.PasswordHash = passwords.Hash(staff, "StrongPassword123!");
        
        db.Users.AddRange(cA, cB, staff);
        
        var make = new Make { Id = Guid.NewGuid(), Name = $"Toyota {Guid.NewGuid():N}" };
        var model = new Model { Id = Guid.NewGuid(), MakeId = make.Id, Name = $"Corolla {Guid.NewGuid():N}" };
        var branch = new Branch { Id = Guid.NewGuid(), Name = $"Main {Guid.NewGuid():N}" };
        db.Makes.Add(make); db.Models.Add(model); db.Branches.Add(branch);
        var v = new Vehicle { Id = Guid.NewGuid(), MakeId = make.Id, Make = make, ModelId = model.Id, Model = model, BranchId = branch.Id, Branch = branch, Year = 2022, Mileage = 10000, Price = 20000 };
        v.SetIdentifiers($"VIN{suffix[..14]}".ToUpper(), null);
        v.TransitionTo(VehicleStatus.InReview, "Test", staff.Id);
        v.TransitionTo(VehicleStatus.Photography, "Test", staff.Id);
        v.TransitionTo(VehicleStatus.Inspection, "Test", staff.Id);
        v.TransitionTo(VehicleStatus.Approved, "Test", staff.Id);
        v.TransitionTo(VehicleStatus.Published, "Test", staff.Id);
        db.Vehicles.Add(v);
        
        await db.SaveChangesAsync();
        var persistedA = await db.Users.SingleAsync(x => x.Id == cA.Id);
        Assert.True(passwords.Verify(persistedA, persistedA.PasswordHash, "StrongPassword123!"));
        
        _customerA = cA.Id;
        _customerB = cB.Id;
        _staffUser = staff.Id;
        _testVehicleId = v.Id;

        _tokenA = await LoginAsync(emailA, "StrongPassword123!", true);
        _tokenB = await LoginAsync(emailB, "StrongPassword123!", true);
        _tokenStaff = await LoginAsync(emailStaff, "StrongPassword123!", false);
        
        var bSessionToken = await db.RefreshTokens.OrderByDescending(x => x.ExpiresAt).FirstOrDefaultAsync(x => x.UserId == _customerB);
        if (bSessionToken is not null) _sessionB = bSessionToken.Id;
    }

    public async Task DisposeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userIds = new[] { _customerA, _customerB, _staffUser };
        await db.FavoriteVehicles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.SavedComparisonVehicles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.OneTimeTokens.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.RefreshTokens.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.VehicleStatusHistories.Where(x => x.VehicleId == _testVehicleId).ExecuteDeleteAsync();
        await db.VehiclePriceHistories.Where(x => x.VehicleId == _testVehicleId).ExecuteDeleteAsync();
        await db.Vehicles.Where(x => x.Id == _testVehicleId).ExecuteDeleteAsync();
        await db.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync();
    }

    private async Task<string> LoginAsync(string email, string password, bool isCustomer)
    {
        var endpoint = isCustomer ? "/api/v1/customers/login" : "/api/v1/auth/login";
        var response = await _client.PostAsJsonAsync(endpoint, new { email, password });
        if (!response.IsSuccessStatusCode) throw new Xunit.Sdk.XunitException($"Login failed for {endpoint}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Client_isolation_and_cross_roles_are_enforced()
    {
        // 1. Cross-role: Staff cannot use customer profile endpoint
        var staffRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/profile");
        staffRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenStaff);
        var staffRes = await _client.SendAsync(staffRequest);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, staffRes.StatusCode);
        
        // 2. Client isolation: Customer A cannot view/revoke Customer B's sessions
        var revokeReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/customers/sessions/{_sessionB}");
        revokeReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        var revokeRes = await _client.SendAsync(revokeReq);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, revokeRes.StatusCode);
        
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var bSessionCheck = await db.RefreshTokens.SingleAsync(x => x.Id == _sessionB);
        Assert.Null(bSessionCheck.RevokedAt);
        var bSessions = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/sessions");
        bSessions.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenB);
        using var bSessionsResponse = await _client.SendAsync(bSessions);
        Assert.Equal(System.Net.HttpStatusCode.OK, bSessionsResponse.StatusCode);
        Assert.Contains(_sessionB.ToString(), await bSessionsResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        
        // 3. Client isolation: Data separation (Favorites/Comparisons)
        var addFavReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/favorites/{_testVehicleId}");
        addFavReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenB);
        addFavReq.Content = new StringContent(JsonSerializer.Serialize(new { favorite = true }), System.Text.Encoding.UTF8, "application/json");
        using var addFavRes = await _client.SendAsync(addFavReq);
        addFavRes.EnsureSuccessStatusCode();
        
        var getFavReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/favorites");
        getFavReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenB);
        using var getFavRes = await _client.SendAsync(getFavReq);
        var getFavStr = await getFavRes.Content.ReadAsStringAsync();
        Assert.Contains(_testVehicleId.ToString(), getFavStr, StringComparison.OrdinalIgnoreCase); // B sees it
        
        var getFavReqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/favorites");
        getFavReqA.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        using var getFavResA = await _client.SendAsync(getFavReqA);
        var getFavStrA = await getFavResA.Content.ReadAsStringAsync();
        Assert.DoesNotContain(_testVehicleId.ToString(), getFavStrA, StringComparison.OrdinalIgnoreCase); // A does NOT see it

        // Comparisons
        var addCompReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/customers/comparison");
        addCompReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenB);
        addCompReq.Content = new StringContent(JsonSerializer.Serialize(new { vehicleIds = new[] { _testVehicleId } }), System.Text.Encoding.UTF8, "application/json");
        using var addCompRes = await _client.SendAsync(addCompReq);
        addCompRes.EnsureSuccessStatusCode();

        var getCompReqB = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/comparison");
        getCompReqB.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenB);
        using var getCompResB = await _client.SendAsync(getCompReqB);
        Assert.Equal(System.Net.HttpStatusCode.OK, getCompResB.StatusCode);
        Assert.Contains(_testVehicleId.ToString(), await getCompResB.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var getCompReqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers/comparison");
        getCompReqA.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        var getCompResA = await _client.SendAsync(getCompReqA);
        var getCompStrA = await getCompResA.Content.ReadAsStringAsync();
        Assert.DoesNotContain(_testVehicleId.ToString(), getCompStrA, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task Logs_and_audit_do_not_capture_personal_data()
    {
        var testLoggerProvider = new TestLoggerProvider();
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(testLoggerProvider);
            });
        });
        using var client = customFactory.CreateClient();

        var newEmail = $"audit-{Guid.NewGuid():N}@test.local";
        var regRes = await client.PostAsJsonAsync("/api/v1/customers/register", new { email = newEmail, password = "StrongPassword123!", name = "SensitiveName", privacyPolicyVersion = "1.0", marketingConsent = true });
        regRes.EnsureSuccessStatusCode();
        
        var loginRes = await client.PostAsJsonAsync("/api/v1/customers/login", new { email = newEmail, password = "StrongPassword123!" });
        loginRes.EnsureSuccessStatusCode();
        var json = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var loginToken = json.GetProperty("accessToken").GetString()!;
        
        var resetRes = await client.PostAsJsonAsync("/api/v1/customers/password-reset", new { email = newEmail });
        resetRes.EnsureSuccessStatusCode();

        var exportReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/export");
        exportReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginToken);
        exportReq.Content = new StringContent(JsonSerializer.Serialize(new { password = "StrongPassword123!" }), System.Text.Encoding.UTF8, "application/json");
        await client.SendAsync(exportReq);
        
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/customers/account");
        deleteReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginToken);
        deleteReq.Content = new StringContent(JsonSerializer.Serialize(new { password = "StrongPassword123!" }), System.Text.Encoding.UTF8, "application/json");
        await client.SendAsync(deleteReq);

        await using var scope = customFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        
        var audits = await db.AuditLogs.Where(x => x.NewValues != null && x.NewValues.Contains("AccountType")).ToListAsync();
        foreach(var audit in audits)
        {
            if (audit.NewValues != null)
            {
                Assert.DoesNotContain("test.local", audit.NewValues, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Password", audit.NewValues, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SensitiveName", audit.NewValues, StringComparison.OrdinalIgnoreCase);
            }
        }

        var allLogs = string.Join(" ", testLoggerProvider.Messages);
        Assert.DoesNotContain("test.local", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrongPassword", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SensitiveName", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(loginToken, allLogs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Csrf_and_export_and_deletion_rules_apply()
    {
        var csrfReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/favorites/merge");
        csrfReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        csrfReq.Headers.Add("Origin", "https://malicious-site.com");
        csrfReq.Content = new StringContent("{\"vehicleIds\":[]}", System.Text.Encoding.UTF8, "application/json");
        var csrfRes = await _client.SendAsync(csrfReq);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, csrfRes.StatusCode);

        // Export without hashes
        var exportReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/export");
        exportReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        exportReq.Content = new StringContent(JsonSerializer.Serialize(new { password = "StrongPassword123!" }), System.Text.Encoding.UTF8, "application/json");
        var exportRes = await _client.SendAsync(exportReq);
        exportRes.EnsureSuccessStatusCode();
        var exportStr = await exportRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", exportStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", exportStr, StringComparison.OrdinalIgnoreCase);

        // Delete account anonymizes and frees email
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/customers/account");
        deleteReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenA);
        deleteReq.Content = new StringContent(JsonSerializer.Serialize(new { password = "StrongPassword123!" }), System.Text.Encoding.UTF8, "application/json");
        var deleteRes = await _client.SendAsync(deleteReq);
        deleteRes.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var deletedUser = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == _customerA);
        Assert.NotNull(deletedUser);
        
        // Assert email is changed and PII is wiped (no NotEqual assertions)
        Assert.Contains("@deleted.invalid", deletedUser.Email);
        Assert.Null(deletedUser.DisplayName);
        Assert.Null(deletedUser.Phone);

        // Can register again and delete again (dos eliminaciones seguidas)
        var newEmail = $"customer-double-del-{Guid.NewGuid():N}@test.local";
        var regRes = await _client.PostAsJsonAsync("/api/v1/customers/register", new { email = newEmail, password = "StrongPassword123!", name = "Test", privacyPolicyVersion = "1.0" });
        regRes.EnsureSuccessStatusCode();
        var secondToken = await LoginAsync(newEmail, "StrongPassword123!", true);
        var secondDeleteReq = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/customers/account");
        secondDeleteReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secondToken);
        secondDeleteReq.Content = new StringContent(JsonSerializer.Serialize(new { password = "StrongPassword123!" }), System.Text.Encoding.UTF8, "application/json");
        var secondDeleteRes = await _client.SendAsync(secondDeleteReq);
        secondDeleteRes.EnsureSuccessStatusCode();

        // Third registration to prove the email is free
        var thirdRegRes = await _client.PostAsJsonAsync("/api/v1/customers/register", new { email = newEmail, password = "StrongPassword123!", name = "Test", privacyPolicyVersion = "1.0" });
        thirdRegRes.EnsureSuccessStatusCode();
    }
}
