using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Dealership.Infrastructure.Tests;

/// <summary>
/// End-to-end journey test that exercises the full registration → email
/// verification → guest-favorite → login → merge flow against the real
/// PostgreSQL database.  Requires <c>ConnectionStrings__Default</c>.
///
/// The test reads the verification link from the <see cref="CaptureEmailSender"/>
/// that the <see cref="TestFactory"/> wires into the DI container — no real SMTP
/// server is needed.
/// </summary>
[Collection("Public catalog HTTP")]
[Trait("Category", "PostgresRequired")]
public sealed class CustomerE2EJourneyTests(TestFactory factory, ITestOutputHelper output) : IAsyncLifetime, IClassFixture<TestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private Guid _vehicleId;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Seed one published vehicle so favourites can reference a real ID.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = new User { Email = $"e2e-seed-{Guid.NewGuid():N}@test.invalid" };
        var make   = new Make   { Name = $"E2E Make {Guid.NewGuid():N}" };
        var model  = new Model  { Make = make, Name = "E2E Model" };
        var branch = new Branch { Name = $"E2E Branch {Guid.NewGuid():N}", Address = "Test", Phones = "[]", Hours = "{}" };
        var vehicle = new Vehicle
        {
            Make = make, MakeId = make.Id, Model = model, ModelId = model.Id,
            Branch = branch, BranchId = branch.Id, CreatedById = user.Id,
            Year = 2023, Mileage = 10_000, Price = 350_000, Currency = "MXN",
            Drivetrain = "AWD", Fuel = "Gasolina", Transmission = "Automática",
            BodyStyle = "SUV", CustomFields = "{}"
        };
        // Transition through all required states to reach Published.
        vehicle.TransitionTo(VehicleStatus.InReview,    "E2E seed", user.Id);
        vehicle.TransitionTo(VehicleStatus.Photography, "E2E seed", user.Id);
        vehicle.TransitionTo(VehicleStatus.Inspection,  "E2E seed", user.Id, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Approved,    "E2E seed", user.Id, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Published,   "E2E seed", user.Id);
        db.AddRange(user, make, model, branch, vehicle);
        await db.SaveChangesAsync();
        _vehicleId = vehicle.Id;
    }


    public async Task DisposeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await db.FavoriteVehicles.Where(x => x.VehicleId == _vehicleId).ExecuteDeleteAsync();
        await db.VehicleStatusHistories.Where(x => x.VehicleId == _vehicleId).ExecuteDeleteAsync();
        await db.VehiclePriceHistories.Where(x => x.VehicleId == _vehicleId).ExecuteDeleteAsync();
        await db.Vehicles.Where(x => x.Id == _vehicleId).ExecuteDeleteAsync();
        await db.Models.Where(x => x.Name == "E2E Model").ExecuteDeleteAsync();
        await db.Makes.Where(x => x.Name.StartsWith("E2E Make ")).ExecuteDeleteAsync();
        await db.Branches.Where(x => x.Name.StartsWith("E2E Branch ")).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Email.StartsWith("e2e-")).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (8) Real E2E journey
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full user journey:
    /// 1. Register → receives verification email (via CaptureEmailSender)
    /// 2. Extract token from email body → POST verify-email
    /// 3. Add vehicle to guest favourites (local list)
    /// 4. Login with correct password → receives JWT
    /// 5. POST favorites/merge with the guest vehicle ID → verify addedCount = 1
    /// 6. GET favorites → vehicle appears
    /// </summary>
    [Fact]
    public async Task Registration_verification_guest_favourites_login_and_merge_succeed()
    {
        var email    = $"e2e-journey-{Guid.NewGuid():N}@test.invalid";
        const string password = "E2E-journey-password-1!";

        // ── Step 1: Register ──────────────────────────────────────────────────
        var registerRes = await _client.PostAsJsonAsync("/api/v1/customers/register", new
        {
            email,
            password,
            name = "E2E User",
            privacyPolicyVersion = "v1"
        });
        Assert.Equal(System.Net.HttpStatusCode.Accepted, registerRes.StatusCode);
        output.WriteLine($"[1] Register → {(int)registerRes.StatusCode} Accepted");

        // ── Step 2: Extract verification link from dev-email stub ─────────────
        var emailSender = factory.Services.GetRequiredService<IEmailSender>() as CaptureEmailSender;
        Assert.NotNull(emailSender);
        var emailBody = emailSender.LastBody;
        output.WriteLine($"[2] Email body (first 400 chars): {emailBody[..Math.Min(400, emailBody.Length)]}");

        // Extract the raw token from the email body.
        // DevelopmentEmailSender format: "Codigo de verificacion: <token>" or URL with ?token=<token>
        var tokenMatch = Regex.Match(emailBody, @"(?:[?&]token=|[Cc].digo de verificaci.n:\s*)([A-Za-z0-9+/=_%-]+)");
        Assert.True(tokenMatch.Success, $"Could not find token in email body:\n{emailBody}");
        var rawToken = Uri.UnescapeDataString(tokenMatch.Groups[1].Value);
        output.WriteLine($"[2] Extracted token (first 20 chars): {rawToken[..Math.Min(20, rawToken.Length)]}...");

        // ── Step 3: Verify email ──────────────────────────────────────────────
        var verifyRes = await _client.PostAsJsonAsync("/api/v1/customers/verify-email", new { token = rawToken });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, verifyRes.StatusCode);
        output.WriteLine($"[3] Verify email → {(int)verifyRes.StatusCode} NoContent");

        // ── Step 4: Login ─────────────────────────────────────────────────────
        var loginRes = await _client.PostAsJsonAsync("/api/v1/customers/login", new { email, password });
        Assert.Equal(System.Net.HttpStatusCode.OK, loginRes.StatusCode);
        var tokenResult = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenResult.GetProperty("accessToken").GetString()!;
        output.WriteLine($"[4] Login → OK, access token obtained");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // ── Step 5: Merge guest favourites ────────────────────────────────────
        var mergeRes = await _client.PostAsJsonAsync("/api/v1/customers/favorites/merge",
            new { vehicleIds = new[] { _vehicleId } });
        Assert.Equal(System.Net.HttpStatusCode.OK, mergeRes.StatusCode);
        var mergeBody = await mergeRes.Content.ReadFromJsonAsync<JsonElement>();
        var addedCount = mergeBody.GetProperty("addedCount").GetInt32();
        Assert.Equal(1, addedCount);
        output.WriteLine($"[5] Merge favourites → addedCount={addedCount}");

        // ── Step 6: GET favourites → vehicle appears ──────────────────────────
        var favRes = await _client.GetAsync("/api/v1/customers/favorites");
        Assert.Equal(System.Net.HttpStatusCode.OK, favRes.StatusCode);
        var favBody = await favRes.Content.ReadFromJsonAsync<JsonElement>();
        var ids = favBody.GetProperty("vehicleIds").EnumerateArray().Select(j => j.GetGuid()).ToList();
        Assert.Contains(_vehicleId, ids);
        output.WriteLine($"[6] GET favourites → vehicleIds=[{string.Join(", ", ids)}]");

        output.WriteLine("[E2E] All steps passed ✅");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (9) Customer endpoint smoke test against PostgreSQL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Smoke test: every customer API endpoint responds with the expected HTTP
    /// status code (not 500, not 404 from a wrong route).
    /// Tests are run without authentication where appropriate and with a valid
    /// Bearer token where required.
    /// </summary>
    [Fact]
    public async Task Customer_endpoints_smoke_test_against_postgresql()
    {
        // Clear any auth header left by a previously-run test in this class instance.
        _client.DefaultRequestHeaders.Authorization = null;

        var email    = $"e2e-smoke-{Guid.NewGuid():N}@test.invalid";
        const string password = "Smoke-test-password-1!";

        // Register
        var reg = await _client.PostAsJsonAsync("/api/v1/customers/register", new
            { email, password, name = "Smoke User", privacyPolicyVersion = "v1" });
        AssertSmoke(reg, "POST /register", System.Net.HttpStatusCode.Accepted);

        // Verify email via stub
        var emailSender = factory.Services.GetRequiredService<IEmailSender>() as CaptureEmailSender;
        var match = Regex.Match(emailSender!.LastBody, @"(?:[?&]token=|[Cc].digo de verificaci.n:\s*)([A-Za-z0-9+/=_%-]+)");
        if (match.Success)
        {
            var token = Uri.UnescapeDataString(match.Groups[1].Value);
            var verify = await _client.PostAsJsonAsync("/api/v1/customers/verify-email", new { token });
            AssertSmoke(verify, "POST /verify-email", System.Net.HttpStatusCode.NoContent);
        }

        // Login
        var login = await _client.PostAsJsonAsync("/api/v1/customers/login", new { email, password });
        AssertSmoke(login, "POST /login", System.Net.HttpStatusCode.OK);
        var tokenEl = await login.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = tokenEl.GetProperty("accessToken").GetString()!;
        var refresh = tokenEl.GetProperty("refreshToken").GetString()!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        // Profile endpoints
        var profile = await _client.GetAsync("/api/v1/customers/profile");
        AssertSmoke(profile, "GET /profile", System.Net.HttpStatusCode.OK);

        var patchProfile = await _client.PutAsJsonAsync("/api/v1/customers/profile",
            new { name = "Smoke Updated", phone = (string?)null, language = "es-MX", marketingConsent = false });
        AssertSmoke(patchProfile, "PUT /profile", System.Net.HttpStatusCode.NoContent);

        // Favourites
        var favGet = await _client.GetAsync("/api/v1/customers/favorites");
        AssertSmoke(favGet, "GET /favorites", System.Net.HttpStatusCode.OK);

        var favPut = await _client.PutAsJsonAsync($"/api/v1/customers/favorites/{_vehicleId}", new { favorite = true });
        AssertSmoke(favPut, "PUT /favorites/{id}", System.Net.HttpStatusCode.NoContent);

        var merge = await _client.PostAsJsonAsync("/api/v1/customers/favorites/merge",
            new { vehicleIds = new[] { _vehicleId } });
        AssertSmoke(merge, "POST /favorites/merge", System.Net.HttpStatusCode.OK);

        // Comparison
        var compPut = await _client.PutAsJsonAsync("/api/v1/customers/comparison",
            new { vehicleIds = new[] { _vehicleId } });
        AssertSmoke(compPut, "PUT /comparison", System.Net.HttpStatusCode.OK);

        var compGet = await _client.GetAsync("/api/v1/customers/comparison");
        AssertSmoke(compGet, "GET /comparison", System.Net.HttpStatusCode.OK);

        // Sessions
        var sessions = await _client.GetAsync("/api/v1/customers/sessions");
        AssertSmoke(sessions, "GET /sessions", System.Net.HttpStatusCode.OK);

        // Export (requires password confirmation)
        var export = await _client.PostAsJsonAsync("/api/v1/customers/export", new { password });
        AssertSmoke(export, "POST /export", System.Net.HttpStatusCode.OK);

        // Password-reset flow (unauthenticated)
        _client.DefaultRequestHeaders.Authorization = null;
        var pwdReset = await _client.PostAsJsonAsync("/api/v1/customers/password-reset", new { email });
        AssertSmoke(pwdReset, "POST /password-reset", System.Net.HttpStatusCode.Accepted);

        // Resend verification (unauthenticated)
        var resend = await _client.PostAsJsonAsync("/api/v1/customers/verify-email/resend", new { email });
        AssertSmoke(resend, "POST /verify-email/resend", System.Net.HttpStatusCode.Accepted);

        // Refresh token (via auth controller)
        var refreshRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        // Could be 200 (if session valid) or 401 (used-token detection) — both are acceptable smoke results.
        Assert.True(
            refreshRes.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Unauthorized,
            $"POST /auth/refresh → unexpected {(int)refreshRes.StatusCode}");
        output.WriteLine($"  POST /auth/refresh → {(int)refreshRes.StatusCode}");

        // Delete account (re-authenticate first)
        _client.DefaultRequestHeaders.Authorization = null;
        var login2 = await _client.PostAsJsonAsync("/api/v1/customers/login", new { email, password });
        var jwt2 = (await login2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt2);

        var deleteAccount = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/customers/account")
        {
            Content = JsonContent.Create(new { password }),
            Headers = { { "Origin", "http://localhost:3000" } }
        });
        AssertSmoke(deleteAccount, "DELETE /account", System.Net.HttpStatusCode.NoContent);

        output.WriteLine("[Smoke] All customer endpoints returned expected status codes ✅");
    }

    private void AssertSmoke(HttpResponseMessage response, string label, System.Net.HttpStatusCode expected)
    {
        output.WriteLine($"  {label} → {(int)response.StatusCode}");
        Assert.Equal(expected, response.StatusCode);
    }
}
