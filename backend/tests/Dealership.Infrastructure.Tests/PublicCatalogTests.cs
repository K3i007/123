using System.Text.Json;
using Dealership.Api.Controllers;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dealership.Infrastructure.Tests;

public sealed class PublicCatalogTests
{
    private static DealershipDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealershipDbContext(options);
    }

    private static (Make make, Model model, Variant variant, Branch branch) CreateBaseEntities(DealershipDbContext db, string makeName = "Toyota", string modelName = "Corolla", string variantName = "SE")
    {
        var make = new Make { Name = makeName };
        var model = new Model { Name = modelName, Make = make };
        var variant = new Variant { Name = variantName, Model = model };
        var branch = new Branch { Name = "Sucursal " + Guid.NewGuid().ToString("N")[..6], Address = "Dir", Phones = "[]", Hours = "{}" };
        db.AddRange(make, model, variant, branch);
        return (make, model, variant, branch);
    }

    /// <summary>Transitions a vehicle through all required states to reach Published.</summary>
    private static void PublishVehicle(Vehicle vehicle, Guid userId)
    {
        vehicle.TransitionTo(VehicleStatus.InReview,    "Test", userId);
        vehicle.TransitionTo(VehicleStatus.Photography, "Test", userId);
        vehicle.TransitionTo(VehicleStatus.Inspection,  "Test", userId, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Approved,    "Test", userId, manualOverride: true);
        vehicle.TransitionTo(VehicleStatus.Published,   "Test", userId);
    }

    [Fact]
    public async Task Only_Published_Vehicles_Are_Returned_In_Listings()
    {
        await using var db = CreateInMemoryDb();
        var (make, model, variant, branch) = CreateBaseEntities(db);

        var user = new User { Email = "admin@test.local" };
        db.Users.Add(user);

        var published = new Vehicle { Make = make, MakeId = make.Id, Model = model, ModelId = model.Id, Variant = variant, VariantId = variant.Id, Branch = branch, BranchId = branch.Id, Year = 2022, Mileage = 10000, Price = 350000m, Color = "Azul", CreatedById = user.Id };
        published.SetIdentifiers("VINPUB0001", "PLTPUB01");
        published.TransitionTo(VehicleStatus.InReview, "Test", user.Id);
        published.TransitionTo(VehicleStatus.Photography, "Test", user.Id);
        published.TransitionTo(VehicleStatus.Inspection, "Test", user.Id, manualOverride: true);
        published.TransitionTo(VehicleStatus.Approved, "Test", user.Id, manualOverride: true);
        published.TransitionTo(VehicleStatus.Published, "Test", user.Id);

        var draft = new Vehicle { Make = make, MakeId = make.Id, Model = model, ModelId = model.Id, Variant = variant, VariantId = variant.Id, Branch = branch, BranchId = branch.Id, Year = 2022, Mileage = 10000, Price = 350000m, CreatedById = user.Id };
        draft.SetIdentifiers("VINDRAFT01", "PLTDFT01");

        var withdrawn = new Vehicle { Make = make, MakeId = make.Id, Model = model, ModelId = model.Id, Variant = variant, VariantId = variant.Id, Branch = branch, BranchId = branch.Id, Year = 2022, Mileage = 10000, Price = 350000m, CreatedById = user.Id };
        withdrawn.SetIdentifiers("VINWTH0001", "PLTWTH01");
        withdrawn.TransitionTo(VehicleStatus.Withdrawn, "Test", user.Id);

        db.Vehicles.AddRange(published, draft, withdrawn);
        await db.SaveChangesAsync();

        var controller = new PublicController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.GetVehicles(new PublicVehicleQuery(), CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var pagedResult = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(okResult.Value);

        Assert.Equal(1, pagedResult.Total);
        var item = Assert.Single(pagedResult.Items);
        Assert.Equal(published.Id, item.Id);
        Assert.NotNull(published.PublishedAt);
    }

    [Fact]
    public async Task Non_Published_Vehicle_Returns_404_By_Id()
    {
        await using var db = CreateInMemoryDb();
        var (make, model, variant, branch) = CreateBaseEntities(db);
        var user = new User { Email = "admin@test.local" };
        db.Users.Add(user);

        var draft = new Vehicle { Make = make, Model = model, Variant = variant, Branch = branch, Year = 2022, Mileage = 10000, Price = 350000m, CreatedById = user.Id };
        db.Vehicles.Add(draft);
        await db.SaveChangesAsync();

        var controller = new PublicController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.GetVehicleById(draft.Id, CancellationToken.None);
        var draftNotFound = Assert.IsType<NotFoundObjectResult>(actionResult.Result);

        var nonExistentResult = await controller.GetVehicleById(Guid.NewGuid(), CancellationToken.None);
        var missingNotFound = Assert.IsType<NotFoundObjectResult>(nonExistentResult.Result);
        Assert.Equal(JsonSerializer.Serialize(draftNotFound.Value), JsonSerializer.Serialize(missingNotFound.Value));
    }

    [Fact]
    public void Public_DTO_Does_Not_Expose_Internal_Or_Sensitive_Fields()
    {
        var listItem = new PublicVehicleListItemDto(
            Guid.NewGuid(), "Toyota", "Corolla", "SE", 2023, 15000, 380000m, "MXN",
            VehicleCondition.Used, "Rojo", "Automática", "Gasolina", "Delantera", "Sedán",
            "Centro", Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        var detailItem = new PublicVehicleDetailDto(
            Guid.NewGuid(), "Toyota", "Corolla", "SE", 2023, 15000, 380000m, "MXN",
            VehicleCondition.Used, "Rojo", "Automática", "Gasolina", "Delantera", "Sedán",
            new PublicVehicleBranchDto("Centro", "Dirección", ["5550300"], new Dictionary<string, string>()),
            ["Aire acondicionado"], "{\"doors\":4}", DateOnly.FromDateTime(DateTime.UtcNow), []);

        var listJson = JsonSerializer.Serialize(listItem);
        var detailJson = JsonSerializer.Serialize(detailItem);

        string[] forbiddenKeys = ["vin", "plate", "xmin", "version", "history", "audit", "notes", "actorid", "createdby"];

        foreach (var key in forbiddenKeys)
        {
            Assert.DoesNotContain($"\"{key}\"", listJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"\"{key}\"", detailJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Search_By_Keyword_Matches_Correct_Vehicles()
    {
        // ILike (case-insensitive LIKE) is a PostgreSQL function; this test requires a real database.
        var connectionString = PostgresGuard.Resolve();
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var suffix = Guid.NewGuid().ToString("N");
        Guid vehicleId1 = Guid.Empty, vehicleId2 = Guid.Empty;
        Guid makeIdToyota = Guid.Empty, makeIdBmw = Guid.Empty;

        await using (var db = new DealershipDbContext(options))
        {
            var (makeToyota, modelCorolla, varSe, branch) = CreateBaseEntities(db, $"SearchKwToyota{suffix}", "Corolla", "SE");
            var (makeBmw, model3, varSport, _)            = CreateBaseEntities(db, $"SearchKwBMW{suffix}", "Serie 3", "330i Sport");

            var user = new User { Email = $"search-kw-{suffix}@test.invalid" };
            db.Users.Add(user);

            var vehicle1 = new Vehicle { Make = makeToyota, MakeId = makeToyota.Id, Model = modelCorolla, ModelId = modelCorolla.Id, Variant = varSe, VariantId = varSe.Id, Branch = branch, BranchId = branch.Id, Year = 2022, Mileage = 10_000, Price = 350_000m, CreatedById = user.Id, CustomFields = "{}" };
            PublishVehicle(vehicle1, user.Id);

            var vehicle2 = new Vehicle { Make = makeBmw, MakeId = makeBmw.Id, Model = model3, ModelId = model3.Id, Variant = varSport, VariantId = varSport.Id, Branch = branch, BranchId = branch.Id, Year = 2023, Mileage = 5_000, Price = 750_000m, CreatedById = user.Id, CustomFields = "{}" };
            PublishVehicle(vehicle2, user.Id);

            db.Vehicles.AddRange(vehicle1, vehicle2);
            await db.SaveChangesAsync();
            vehicleId1 = vehicle1.Id; vehicleId2 = vehicle2.Id;
            makeIdToyota = makeToyota.Id; makeIdBmw = makeBmw.Id;
        }

        try
        {
            await using var db = new DealershipDbContext(options);
            var controller = new PublicController(db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Case-insensitive match on make "SearchKwBMW..."
            var bmwResult = await controller.GetVehicles(new PublicVehicleQuery(Q: $"SearchKwBMW{suffix}"), CancellationToken.None);
            var bmwOk = Assert.IsType<OkObjectResult>(bmwResult.Result);
            var bmwPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(bmwOk.Value);
            Assert.Equal(1, bmwPaged.Total);
            Assert.Contains("BMW", bmwPaged.Items.First().Make);

            // Case-insensitive match (lowercase suffix)
            var lcResult = await controller.GetVehicles(new PublicVehicleQuery(Q: $"searchkwbmw{suffix}"), CancellationToken.None);
            var lcOk = Assert.IsType<OkObjectResult>(lcResult.Result);
            var lcPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(lcOk.Value);
            Assert.Equal(1, lcPaged.Total);

            // Partial match on model "Corol"
            var corolResult = await controller.GetVehicles(new PublicVehicleQuery(Q: "Corol"), CancellationToken.None);
            var corolOk = Assert.IsType<OkObjectResult>(corolResult.Result);
            var corolPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(corolOk.Value);
            Assert.True(corolPaged.Total >= 1);
            Assert.Contains(corolPaged.Items, i => i.Model == "Corolla");
        }
        finally
        {
            await using var cleanup = new DealershipDbContext(options);
            await cleanup.VehicleStatusHistories.Where(x => x.VehicleId == vehicleId1 || x.VehicleId == vehicleId2).ExecuteDeleteAsync();
            await cleanup.VehiclePriceHistories.Where(x => x.VehicleId == vehicleId1 || x.VehicleId == vehicleId2).ExecuteDeleteAsync();
            await cleanup.Vehicles.Where(x => x.Id == vehicleId1 || x.Id == vehicleId2).ExecuteDeleteAsync();
            await cleanup.Variants.Where(x => x.Model.Make.Id == makeIdToyota || x.Model.Make.Id == makeIdBmw).ExecuteDeleteAsync();
            await cleanup.Models.Where(x => x.Make.Id == makeIdToyota || x.Make.Id == makeIdBmw).ExecuteDeleteAsync();
            await cleanup.Makes.Where(x => x.Id == makeIdToyota || x.Id == makeIdBmw).ExecuteDeleteAsync();
            await cleanup.Branches.Where(x => x.Name.StartsWith("Sucursal ")).ExecuteDeleteAsync();
            await cleanup.Users.Where(x => x.Email == $"search-kw-{suffix}@test.invalid").ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Invalid_Price_Or_Year_Range_Returns_BadRequest()
    {
        await using var db = CreateInMemoryDb();
        var controller = new PublicController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var badPrice = await controller.GetVehicles(new PublicVehicleQuery(MinPrice: 500000, MaxPrice: 100000), CancellationToken.None);
        var badPriceObj = Assert.IsType<BadRequestObjectResult>(badPrice.Result);
        var badPriceProblem = Assert.IsType<ProblemDetails>(badPriceObj.Value);
        Assert.Contains("precio", badPriceProblem.Title, StringComparison.OrdinalIgnoreCase);

        var badYear = await controller.GetVehicles(new PublicVehicleQuery(MinYear: 2025, MaxYear: 2020), CancellationToken.None);
        var badYearObj = Assert.IsType<BadRequestObjectResult>(badYear.Result);
        var badYearProblem = Assert.IsType<ProblemDetails>(badYearObj.Value);
        Assert.Contains("año", badYearProblem.Title, StringComparison.OrdinalIgnoreCase);
    }
}
