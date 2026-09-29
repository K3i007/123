using System.Text.Json;
using Asp.Versioning;
using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Api.Controllers;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/inventory"), Authorize]
public sealed class InventoryController(DealershipDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("vehicles"), Authorize(Roles = "InventoryManager,Manager,Administrator,Salesperson")]
    public async Task<ActionResult<PagedResult<VehicleResponse>>> List([FromQuery] VehicleQuery query, CancellationToken cancellationToken)
    {
        var items = db.Vehicles.AsNoTracking().Include(x => x.Make).Include(x => x.Model).Include(x => x.Variant).Include(x => x.Branch).Where(x => !x.IsDeleted);
        if (query.MakeId is not null) items = items.Where(x => x.MakeId == query.MakeId); if (query.ModelId is not null) items = items.Where(x => x.ModelId == query.ModelId); if (query.BranchId is not null) items = items.Where(x => x.BranchId == query.BranchId); if (query.Status is not null) items = items.Where(x => x.Status == query.Status);
        if (query.MinYear is not null) items = items.Where(x => x.Year >= query.MinYear); if (query.MaxPrice is not null) items = items.Where(x => x.Price <= query.MaxPrice);
        if (!string.IsNullOrWhiteSpace(query.Search)) { var pattern = $"%{EscapeLike(query.Search.Trim())}%"; items = items.Where(x => EF.Functions.ILike(x.Make.Name, pattern, "\\") || EF.Functions.ILike(x.Model.Name, pattern, "\\") || (x.Variant != null && EF.Functions.ILike(x.Variant.Name, pattern, "\\"))); }
        var total = await items.CountAsync(cancellationToken); var page = Math.Max(query.Page, 1); var size = Math.Clamp(query.PageSize, 1, 100);
        var ordered = query.Sort?.ToLowerInvariant() switch { "price" => items.OrderBy(x => x.Price), "year" => items.OrderByDescending(x => x.Year), _ => items.OrderByDescending(x => x.CreatedAt) };
        var result = await ordered.Skip((page - 1) * size).Take(size).Select(x => ToResponse(x, CanViewIdentifiers())).ToListAsync(cancellationToken);
        return Ok(new PagedResult<VehicleResponse>(result, total, page, size));
    }

    [HttpGet("vehicles/{id:guid}"), Authorize(Roles = "InventoryManager,Manager,Administrator,Salesperson")]
    public async Task<ActionResult<VehicleResponse>> Get(Guid id, CancellationToken cancellationToken) { var vehicle = await db.Vehicles.AsNoTracking().Include(x => x.Make).Include(x => x.Model).Include(x => x.Variant).Include(x => x.Branch).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken); return vehicle is null ? NotFound() : Ok(ToResponse(vehicle, CanViewIdentifiers())); }

    [HttpPost("vehicles"), Authorize(Roles = "InventoryManager,Manager,Administrator")]
    public async Task<ActionResult<VehicleResponse>> Create(VehicleRequest request, CancellationToken cancellationToken)
    {
        var actor = Actor(); var vehicle = new Vehicle { MakeId = request.MakeId, ModelId = request.ModelId, VariantId = request.VariantId, BranchId = request.BranchId, Year = request.Year, Mileage = request.Mileage, Price = request.Price, Currency = request.Currency, Condition = request.Condition, Color = request.Color, Transmission = request.Transmission, Fuel = request.Fuel, Drivetrain = request.Drivetrain, BodyStyle = request.BodyStyle, CustomFields = request.CustomFields, CreatedById = actor };
        vehicle.SetIdentifiers(request.Vin, request.Plate); await ValidateAsync(vehicle, cancellationToken); db.Vehicles.Add(vehicle); await db.SaveChangesAsync(cancellationToken); await LoadAsync(vehicle, cancellationToken); return CreatedAtAction(nameof(Get), new { id = vehicle.Id, version = "1" }, ToResponse(vehicle, CanViewIdentifiers()));
    }

    [HttpPut("vehicles/{id:guid}"), Authorize(Roles = "InventoryManager,Manager,Administrator")]
    public async Task<ActionResult<VehicleResponse>> Update(Guid id, VehicleRequest request, [FromHeader(Name = "If-Match")] string? version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version)) return StatusCode(StatusCodes.Status428PreconditionRequired, new ProblemDetails { Title = "Falta If-Match.", Detail = "Recarga el vehículo e incluye su versión para editarlo.", Status = StatusCodes.Status428PreconditionRequired });
        if (!uint.TryParse(version.Trim('"'), out var rowVersion)) return BadRequest(new ProblemDetails { Title = "If-Match no contiene una versión válida." }); var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken); if (vehicle is null) return NotFound(); db.Entry(vehicle).Property(x => x.Version).OriginalValue = rowVersion;
        vehicle.MakeId = request.MakeId; vehicle.ModelId = request.ModelId; vehicle.VariantId = request.VariantId; vehicle.BranchId = request.BranchId; vehicle.Year = request.Year; vehicle.Mileage = request.Mileage; vehicle.Currency = request.Currency; vehicle.Condition = request.Condition; vehicle.Color = request.Color; vehicle.Transmission = request.Transmission; vehicle.Fuel = request.Fuel; vehicle.Drivetrain = request.Drivetrain; vehicle.BodyStyle = request.BodyStyle; vehicle.CustomFields = request.CustomFields; vehicle.SetIdentifiers(request.Vin, request.Plate); if (vehicle.Price != request.Price) vehicle.ChangePrice(request.Price, request.PriceReason ?? "Actualización de precio", Actor()); await ValidateAsync(vehicle, cancellationToken);
        foreach (var priceHistory in vehicle.PriceHistory.Where(x => db.Entry(x).State == EntityState.Detached)) db.Add(priceHistory);
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "El vehículo fue actualizado por otra persona.", Detail = "Recarga la información e inténtalo nuevamente.", Status = StatusCodes.Status409Conflict }); }
        await LoadAsync(vehicle, cancellationToken); return Ok(ToResponse(vehicle, CanViewIdentifiers()));
    }

    [HttpPost("vehicles/{id:guid}/transitions"), Authorize(Roles = "InventoryManager,Manager,Administrator")]
    public async Task<ActionResult<VehicleResponse>> Transition(Guid id, TransitionRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken); if (vehicle is null) return NotFound(); var manual = vehicle.Status is VehicleStatus.Photography or VehicleStatus.Inspection;
        if (manual && (!User.IsInRole(SystemRoles.Manager) && !User.IsInRole(SystemRoles.Administrator))) return Forbid(); if (manual && string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new ProblemDetails { Title = "El motivo es obligatorio para un avance manual." });
        if (request.ToStatus == VehicleStatus.Published && (!User.IsInRole(SystemRoles.Manager) && !User.IsInRole(SystemRoles.Administrator))) return Forbid();
        try { vehicle.TransitionTo(request.ToStatus, request.Reason, Actor(), manual); foreach (var history in vehicle.StatusHistory.Where(x => db.Entry(x).State == EntityState.Detached)) db.Add(history); await db.SaveChangesAsync(cancellationToken); } catch (DomainRuleException exception) { return BadRequest(new ProblemDetails { Title = "No se pudo cambiar el estado.", Detail = exception.Message }); } catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "El vehículo fue actualizado por otra persona.", Status = StatusCodes.Status409Conflict }); }
        await LoadAsync(vehicle, cancellationToken); return Ok(ToResponse(vehicle, CanViewIdentifiers()));
    }

    [HttpGet("vehicles/{id:guid}/history"), Authorize(Roles = "InventoryManager,Manager,Administrator,Salesperson")]
    public async Task<ActionResult<object>> History(Guid id, CancellationToken cancellationToken) => Ok(new { states = await db.VehicleStatusHistories.Where(x => x.VehicleId == id).OrderByDescending(x => x.ChangedAt).ToListAsync(cancellationToken), prices = await db.VehiclePriceHistories.Where(x => x.VehicleId == id).OrderByDescending(x => x.ChangedAt).ToListAsync(cancellationToken) });

    private async Task ValidateAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        if (vehicle.Year is < 1900 or > 2100 || vehicle.Mileage < 0) throw new DomainRuleException("Revisa año y kilometraje.");
        var definitions = await db.CustomFieldDefinitions.ToListAsync(cancellationToken); CustomFieldValidator.Validate(vehicle.CustomFields, definitions);
    }
    private async Task LoadAsync(Vehicle vehicle, CancellationToken cancellationToken) { await db.Entry(vehicle).Reference(x => x.Make).LoadAsync(cancellationToken); await db.Entry(vehicle).Reference(x => x.Model).LoadAsync(cancellationToken); await db.Entry(vehicle).Reference(x => x.Variant).LoadAsync(cancellationToken); await db.Entry(vehicle).Reference(x => x.Branch).LoadAsync(cancellationToken); }
    private Guid Actor() => Guid.TryParse(currentUser.Id, out var id) ? id : throw new UnauthorizedAccessException(); private bool CanViewIdentifiers() => User.IsInRole(SystemRoles.InventoryManager) || User.IsInRole(SystemRoles.Manager) || User.IsInRole(SystemRoles.Administrator);
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
    private static VehicleResponse ToResponse(Vehicle x, bool identifiers) => new(x.Id, x.MakeId, x.ModelId, x.VariantId, x.BranchId, x.Make.Name, x.Model.Name, x.Variant?.Name, x.Branch.Name, x.Year, x.Mileage, x.Price, x.Currency, x.Status, x.Condition, x.Color, x.Transmission, x.Fuel, x.Drivetrain, x.BodyStyle, x.CustomFields, x.Version, identifiers ? x.Vin : null, identifiers ? x.Plate : null);
}
public sealed record VehicleRequest(Guid MakeId, Guid ModelId, Guid? VariantId, Guid BranchId, int Year, int Mileage, decimal Price, string Currency, VehicleCondition Condition, string? Vin, string? Plate, string? Color, string? Transmission, string? Fuel, string? Drivetrain, string? BodyStyle, string CustomFields, string? PriceReason);
public sealed record TransitionRequest(VehicleStatus ToStatus, string Reason);
public sealed record VehicleQuery(Guid? MakeId, Guid? ModelId, Guid? BranchId, VehicleStatus? Status, int? MinYear, decimal? MaxPrice, string? Search, string? Sort, int Page = 1, int PageSize = 20);
public sealed record VehicleResponse(Guid Id, Guid MakeId, Guid ModelId, Guid? VariantId, Guid BranchId, string Make, string Model, string? Variant, string Branch, int Year, int Mileage, decimal Price, string Currency, VehicleStatus Status, VehicleCondition Condition, string? Color, string? Transmission, string? Fuel, string? Drivetrain, string? BodyStyle, string CustomFields, uint Version, string? Vin, string? Plate);
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);
