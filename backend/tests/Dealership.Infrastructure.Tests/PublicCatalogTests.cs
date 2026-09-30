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
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);

        var nonExistentResult = await controller.GetVehicleById(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(nonExistentResult.Result);
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
            "Centro", Guid.NewGuid(), "{\"doors\":4}", null, DateTimeOffset.UtcNow);

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
        await using var db = CreateInMemoryDb();
        var (makeToyota, modelCorolla, varSe, branch) = CreateBaseEntities(db, "Toyota", "Corolla", "SE");
        var (makeBmw, model3, varSport, _) = CreateBaseEntities(db, "BMW", "Serie 3", "330i Sport");

        var user = new User { Email = "admin@test.local" };
        db.Users.Add(user);

        var vehicle1 = new Vehicle { Make = makeToyota, MakeId = makeToyota.Id, Model = modelCorolla, ModelId = modelCorolla.Id, Variant = varSe, VariantId = varSe.Id, Branch = branch, BranchId = branch.Id, Year = 2022, Mileage = 10000, Price = 350000m, CreatedById = user.Id };
        vehicle1.TransitionTo(VehicleStatus.InReview, "Test", user.Id);
        vehicle1.TransitionTo(VehicleStatus.Photography, "Test", user.Id);
        vehicle1.TransitionTo(VehicleStatus.Inspection, "Test", user.Id, manualOverride: true);
        vehicle1.TransitionTo(VehicleStatus.Approved, "Test", user.Id, manualOverride: true);
        vehicle1.TransitionTo(VehicleStatus.Published, "Test", user.Id);

        var vehicle2 = new Vehicle { Make = makeBmw, MakeId = makeBmw.Id, Model = model3, ModelId = model3.Id, Variant = varSport, VariantId = varSport.Id, Branch = branch, BranchId = branch.Id, Year = 2023, Mileage = 5000, Price = 750000m, CreatedById = user.Id };
        vehicle2.TransitionTo(VehicleStatus.InReview, "Test", user.Id);
        vehicle2.TransitionTo(VehicleStatus.Photography, "Test", user.Id);
        vehicle2.TransitionTo(VehicleStatus.Inspection, "Test", user.Id, manualOverride: true);
        vehicle2.TransitionTo(VehicleStatus.Approved, "Test", user.Id, manualOverride: true);
        vehicle2.TransitionTo(VehicleStatus.Published, "Test", user.Id);

        db.Vehicles.AddRange(vehicle1, vehicle2);
        await db.SaveChangesAsync();

        var controller = new PublicController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Case-insensitive match on make "bmw"
        var bmwResult = await controller.GetVehicles(new PublicVehicleQuery(Q: "bmw"), CancellationToken.None);
        var bmwOk = Assert.IsType<OkObjectResult>(bmwResult.Result);
        var bmwPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(bmwOk.Value);
        Assert.Equal(1, bmwPaged.Total);
        Assert.Equal("BMW", bmwPaged.Items.First().Make);

        // Case-insensitive match on make "BMW"
        var bmwUpperResult = await controller.GetVehicles(new PublicVehicleQuery(Q: "BMW"), CancellationToken.None);
        var bmwUpperOk = Assert.IsType<OkObjectResult>(bmwUpperResult.Result);
        var bmwUpperPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(bmwUpperOk.Value);
        Assert.Equal(1, bmwUpperPaged.Total);
        Assert.Equal("BMW", bmwUpperPaged.Items.First().Make);

        // Partial match on model "corol"
        var corolResult = await controller.GetVehicles(new PublicVehicleQuery(Q: "corol"), CancellationToken.None);
        var corolOk = Assert.IsType<OkObjectResult>(corolResult.Result);
        var corolPaged = Assert.IsType<PublicPagedResult<PublicVehicleListItemDto>>(corolOk.Value);
        Assert.Equal(1, corolPaged.Total);
        Assert.Equal("Corolla", corolPaged.Items.First().Model);
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
