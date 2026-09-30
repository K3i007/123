using System.Text.Json;
using System.Net.Http.Json;
using Dealership.Infrastructure;
using Dealership.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using Xunit;

namespace Dealership.Infrastructure.Tests;

[CollectionDefinition("Public catalog HTTP", DisableParallelization = true)]
public sealed class PublicCatalogHttpCollection;

[Collection("Public catalog HTTP")]
public sealed class PublicCatalogHttpTests(WebApplicationFactory<Program> factory) : IAsyncLifetime, IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private Guid _makeId;
    private Guid _publishedId;
    private Guid _draftId;
    private Guid _withdrawnId;
    private string _fieldKey = string.Empty;
    private string _privateFieldKey = string.Empty;

    public async Task InitializeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await CleanHttpFixturesAsync(db);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _fieldKey = $"httpfilter{suffix}";
        _privateFieldKey = $"httpprivate{suffix}";
        var user = new User { Email = $"http-{suffix}@test.local" };
        var make = new Make { Name = $"Http Make {suffix}" };
        var model = new Model { Make = make, Name = "Http Model" };
        var branch = new Branch { Name = $"Http Branch {suffix}", Address = "Test", Phones = "[]", Hours = "{}" };
        db.AddRange(user, make, model, branch, new CustomFieldDefinition { Key = _fieldKey, Label = "HTTP filter", Type = CustomFieldType.Select, Options = "[\"yes\",\"no\"]", IsPublic = true, IsFilterable = true }, new CustomFieldDefinition { Key = _privateFieldKey, Label = "Private HTTP field", Type = CustomFieldType.Text });

        var published = CreateVehicle(make, model, branch, user, 2024, 12000, 500000m, "AWD", _fieldKey, "yes");
        published.CustomFields = $"{{\"{_fieldKey}\":\"yes\",\"{_privateFieldKey}\":\"internal\"}}";
        Publish(published, user.Id);
        var secondPublished = CreateVehicle(make, model, branch, user, 2023, 32000, 400000m, "Delantera", _fieldKey, "no");
        Publish(secondPublished, user.Id);
        var draft = CreateVehicle(make, model, branch, user, 2024, 1000, 600000m, "AWD", _fieldKey, "yes");
        var withdrawn = CreateVehicle(make, model, branch, user, 2021, 44000, 350000m, "Delantera", _fieldKey, "no");
        withdrawn.TransitionTo(VehicleStatus.Withdrawn, "HTTP test", user.Id);
        db.AddRange(published, secondPublished, draft, withdrawn);
        await db.SaveChangesAsync();
        _makeId = make.Id; _publishedId = published.Id; _draftId = draft.Id; _withdrawnId = withdrawn.Id;
    }

    public async Task DisposeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await CleanHttpFixturesAsync(scope.ServiceProvider.GetRequiredService<DealershipDbContext>());
    }

    [Fact]
    public async Task Public_catalog_filters_paginate_and_never_serializes_private_identifiers()
    {
        // This request guards the query that previously produced an EF translation 500 for branches.
        var filterResponse = await _client.GetAsync("/api/v1/public/vehicles/filters");
        filterResponse.EnsureSuccessStatusCode();
        var filters = (await filterResponse.Content.ReadFromJsonAsync<JsonElement>())!;
        Assert.Contains(filters.GetProperty("drivetrains").EnumerateArray().Select(x => x.GetString()), value => value == "AWD");
        Assert.Contains(filters.GetProperty("customFields").EnumerateArray(), value => value.GetProperty("key").GetString() == _fieldKey);
        Assert.DoesNotContain(filters.GetProperty("customFields").EnumerateArray(), value => value.GetProperty("key").GetString() == _privateFieldKey);

        var response = await _client.GetAsync($"/api/v1/public/vehicles?makeId={_makeId}&drivetrain=AWD&maxMileage=20000&cf.{_fieldKey}=yes&pageSize=1");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(_publishedId, document.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain("vin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_draftId.ToString(), json, StringComparison.OrdinalIgnoreCase);

        var detailJson = await _client.GetStringAsync($"/api/v1/public/vehicles/{_publishedId}");
        Assert.Contains(_fieldKey, detailJson, StringComparison.Ordinal);
        Assert.DoesNotContain(_privateFieldKey, detailJson, StringComparison.Ordinal);
        var branchesJson = await _client.GetStringAsync("/api/v1/public/branches");
        Assert.DoesNotContain("managerName", branchesJson, StringComparison.OrdinalIgnoreCase);

        var draftDetail = await _client.GetAsync($"/api/v1/public/vehicles/{_draftId}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, draftDetail.StatusCode);

        var privateFilter = await _client.GetAsync($"/api/v1/public/vehicles?cf.{_privateFieldKey}=internal");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, privateFilter.StatusCode);
        Assert.Equal("application/problem+json", privateFilter.Content.Headers.ContentType?.MediaType);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var privateDefinition = await verificationDb.CustomFieldDefinitions.SingleAsync(x => x.Key == _privateFieldKey);
        Assert.False(privateDefinition.IsPublic);
        Assert.False(privateDefinition.IsFilterable);

        var pageTwo = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/public/vehicles?makeId={_makeId}&pageSize=1&page=2");
        Assert.Equal(2, pageTwo.GetProperty("page").GetInt32());
        Assert.Equal(2, pageTwo.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=49")]
    [InlineData("?maxMileage=-1")]
    public async Task Invalid_public_query_returns_problem_details(string query)
    {
        var response = await _client.GetAsync($"/api/v1/public/vehicles{query}");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Published_at_migration_backfills_existing_published_vehicles()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var migration = new Dealership.Infrastructure.Migrations.AddVehiclePublishedAt();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(Dealership.Infrastructure.Migrations.AddVehiclePublishedAt)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var backfill = Assert.Single(builder.Operations.OfType<SqlOperation>());
        Assert.Contains("UPDATE \"Vehicles\" SET \"PublishedAt\" = \"CreatedAt\" WHERE \"Status\" = 5", backfill.Sql, StringComparison.Ordinal);

        var missingPublicationDates = await db.Vehicles.CountAsync(x => x.Status == VehicleStatus.Published && x.PublishedAt == null);
        Assert.Equal(0, missingPublicationDates);
    }

    [Fact]
    public async Task Public_endpoints_smoke_test_against_postgresql_and_detail_hides_unpublished_inventory()
    {
        var endpoints = new[]
        {
            "/api/v1/public/vehicles?pageSize=12",
            "/api/v1/public/vehicles/filters",
            "/api/v1/public/vehicles/featured",
            "/api/v1/public/vehicles/recent",
            $"/api/v1/public/vehicles/{_publishedId}",
            $"/api/v1/public/vehicles/{_publishedId}/similar",
            "/api/v1/public/branches"
        };
        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        var publishedDetail = await _client.GetStringAsync($"/api/v1/public/vehicles/{_publishedId}");
        Assert.Contains("publishedOn", publishedDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("vin", publishedDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plate", publishedDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("history", publishedDetail, StringComparison.OrdinalIgnoreCase);

        var similar = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/public/vehicles/{_publishedId}/similar");
        Assert.InRange(similar.GetArrayLength(), 0, 6);
        Assert.DoesNotContain(similar.EnumerateArray(), item => item.GetProperty("id").GetGuid() == _publishedId);

        var hiddenResponses = await Task.WhenAll(new[]
        {
            _client.GetAsync($"/api/v1/public/vehicles/{_draftId}"),
            _client.GetAsync($"/api/v1/public/vehicles/{_withdrawnId}"),
            _client.GetAsync($"/api/v1/public/vehicles/{Guid.NewGuid()}")
        });
        var bodies = new List<string>();
        foreach (var response in hiddenResponses)
        {
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("public, max-age=45", response.Headers.CacheControl?.ToString());
            bodies.Add(await response.Content.ReadAsStringAsync());
        }
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));
    }

    private static Vehicle CreateVehicle(Make make, Model model, Branch branch, User user, int year, int mileage, decimal price, string drivetrain, string fieldKey, string filterValue)
    {
        var vehicle = new Vehicle { Make = make, MakeId = make.Id, Model = model, ModelId = model.Id, Branch = branch, BranchId = branch.Id, Year = year, Mileage = mileage, Price = price, Currency = "MXN", Drivetrain = drivetrain, Fuel = "Gasolina", Transmission = "Automática", BodyStyle = "SUV", CreatedById = user.Id, CustomFields = $"{{\"{fieldKey}\":\"{filterValue}\"}}" };
        return vehicle;
    }

    private static void Publish(Vehicle vehicle, Guid userId)
    {
        vehicle.TransitionTo(VehicleStatus.InReview, "HTTP test", userId);
        vehicle.TransitionTo(VehicleStatus.Photography, "HTTP test", userId);
        vehicle.TransitionTo(VehicleStatus.Inspection, "HTTP test", userId, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Approved, "HTTP test", userId, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Published, "HTTP test", userId);
    }

    private static async Task CleanHttpFixturesAsync(DealershipDbContext db)
    {
        var vehicleIds = await db.Vehicles.Where(x => x.Make.Name.StartsWith("Http Make ")).Select(x => x.Id).ToListAsync();
        if (vehicleIds.Count > 0)
        {
            await db.VehicleStatusHistories.Where(x => vehicleIds.Contains(x.VehicleId)).ExecuteDeleteAsync();
            await db.VehiclePriceHistories.Where(x => vehicleIds.Contains(x.VehicleId)).ExecuteDeleteAsync();
            await db.Vehicles.Where(x => vehicleIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await db.Variants.Where(x => x.Model.Make.Name.StartsWith("Http Make ")).ExecuteDeleteAsync();
        await db.Models.Where(x => x.Make.Name.StartsWith("Http Make ")).ExecuteDeleteAsync();
        await db.Makes.Where(x => x.Name.StartsWith("Http Make ")).ExecuteDeleteAsync();
        await db.Branches.Where(x => x.Name.StartsWith("Http Branch ")).ExecuteDeleteAsync();
        await db.CustomFieldDefinitions.Where(x => x.Key.StartsWith("httpfilter") || x.Key.StartsWith("httpprivate")).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Email.StartsWith("http-") && x.Email.EndsWith("@test.local")).ExecuteDeleteAsync();
    }
}
