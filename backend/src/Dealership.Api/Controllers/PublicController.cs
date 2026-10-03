using Asp.Versioning;
using Dealership.Application;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Dealership.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public")]
[AllowAnonymous]
[EnableRateLimiting("public-catalog")]
public sealed class PublicController(DealershipDbContext db) : ControllerBase
{
    [HttpGet("vehicles/compare")]
    public async Task<ActionResult<IReadOnlyCollection<PublicComparisonVehicle>>> Compare([FromQuery] Guid[] ids, CancellationToken cancellationToken)
    {
        var requested = ids.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (requested.Length is < 2 or > 4)
            return BadRequest(new ProblemDetails { Title = "Comparación inválida.", Detail = "Selecciona entre dos y cuatro vehículos.", Status = StatusCodes.Status400BadRequest });
        var vehicles = await db.Vehicles.AsNoTracking().Where(x => requested.Contains(x.Id) && x.Status == VehicleStatus.Published && !x.IsDeleted)
            .Include(x => x.Make).Include(x => x.Model).Include(x => x.Variant).Include(x => x.Equipment).ThenInclude(x => x.Equipment)
            .ToListAsync(cancellationToken);
        return Ok(vehicles.OrderBy(x => Array.IndexOf(requested, x.Id)).Select(x => new PublicComparisonVehicle(x.Id, x.Make.Name, x.Model.Name, x.Variant?.Name, x.Year, x.Mileage, x.Price, x.Currency, x.Transmission, x.Fuel, x.Drivetrain, x.BodyStyle, null, x.Equipment.Select(e => e.Equipment.Name).OrderBy(x => x).ToArray())).ToList());
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<PublicPagedResult<PublicVehicleListItemDto>>> GetVehicles([FromQuery] PublicVehicleQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1) query = query with { Page = 1 };
        if (query.PageSize is < 1 or > 48)
            return BadRequest(new ProblemDetails { Title = "Tamaño de página inválido.", Detail = "pageSize debe estar entre 1 y 48.", Status = StatusCodes.Status400BadRequest });
        if (query.Q?.Length > 100)
            return BadRequest(new ProblemDetails { Title = "Búsqueda inválida.", Detail = "q no puede exceder 100 caracteres.", Status = StatusCodes.Status400BadRequest });
        if (query.MinPrice < 0 || query.MaxPrice < 0 || (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice))
            return BadRequest(new ProblemDetails { Title = "Rango de precio inválido.", Detail = "El precio mínimo no puede ser mayor al precio máximo ni menor a cero." });
        if (query.MinYear < 1900 || query.MaxYear > 2100 || (query.MinYear.HasValue && query.MaxYear.HasValue && query.MinYear > query.MaxYear))
            return BadRequest(new ProblemDetails { Title = "Rango de año inválido.", Detail = "El año mínimo no puede ser mayor al año máximo." });
        if (query.MaxMileage < 0)
            return BadRequest(new ProblemDetails { Title = "Kilometraje máximo inválido.", Detail = "maxMileage debe ser mayor o igual a cero.", Status = StatusCodes.Status400BadRequest });

        Response.Headers.Append("Cache-Control", "public, max-age=45");

        var baseQuery = db.Vehicles
            .AsNoTracking()
            .Where(x => x.Status == VehicleStatus.Published && !x.IsDeleted);

        // Keyword Search strategy: case-insensitive via Npgsql ILike (requires PostgreSQL provider)
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var pattern = $"%{EscapeLike(term)}%";

            var matchedMakeIds = await db.Makes.Where(x => !x.IsDeleted && EF.Functions.ILike(x.Name, pattern, "\\")).Select(x => x.Id).ToListAsync(cancellationToken);
            var matchedModelIds = await db.Models.Where(x => !x.IsDeleted && EF.Functions.ILike(x.Name, pattern, "\\")).Select(x => x.Id).ToListAsync(cancellationToken);
            var matchedVariantIds = await db.Variants.Where(x => !x.IsDeleted && EF.Functions.ILike(x.Name, pattern, "\\")).Select(x => x.Id).ToListAsync(cancellationToken);

            if (matchedMakeIds.Count == 0 && matchedModelIds.Count == 0 && matchedVariantIds.Count == 0)
            {
                return Ok(new PublicPagedResult<PublicVehicleListItemDto>([], 0, query.Page, query.PageSize));
            }

            baseQuery = baseQuery.Where(v =>
                matchedMakeIds.Contains(v.MakeId) ||
                matchedModelIds.Contains(v.ModelId) ||
                (v.VariantId != null && matchedVariantIds.Contains(v.VariantId.Value)));
        }

        if (query.MakeId.HasValue) baseQuery = baseQuery.Where(x => x.MakeId == query.MakeId.Value);
        if (query.ModelId.HasValue) baseQuery = baseQuery.Where(x => x.ModelId == query.ModelId.Value);
        if (query.VariantId.HasValue) baseQuery = baseQuery.Where(x => x.VariantId == query.VariantId.Value);
        if (query.BranchId.HasValue) baseQuery = baseQuery.Where(x => x.BranchId == query.BranchId.Value);
        if (query.MinYear.HasValue) baseQuery = baseQuery.Where(x => x.Year >= query.MinYear.Value);
        if (query.MaxYear.HasValue) baseQuery = baseQuery.Where(x => x.Year <= query.MaxYear.Value);
        if (query.MinPrice.HasValue) baseQuery = baseQuery.Where(x => x.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) baseQuery = baseQuery.Where(x => x.Price <= query.MaxPrice.Value);
        if (query.MaxMileage.HasValue) baseQuery = baseQuery.Where(x => x.Mileage <= query.MaxMileage.Value);
        if (query.Condition.HasValue) baseQuery = baseQuery.Where(x => x.Condition == query.Condition.Value);
        if (!string.IsNullOrWhiteSpace(query.Transmission)) baseQuery = baseQuery.Where(x => x.Transmission == query.Transmission);
        if (!string.IsNullOrWhiteSpace(query.Fuel)) baseQuery = baseQuery.Where(x => x.Fuel == query.Fuel);
        if (!string.IsNullOrWhiteSpace(query.Drivetrain)) baseQuery = baseQuery.Where(x => x.Drivetrain == query.Drivetrain);
        if (!string.IsNullOrWhiteSpace(query.BodyStyle)) baseQuery = baseQuery.Where(x => x.BodyStyle == query.BodyStyle);

        var requestedCustomFilters = Request.Query
            .Where(x => x.Key.StartsWith("cf.", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new { Key = x.Key[3..], Value = x.Value.ToString() })
            .ToList();
        if (requestedCustomFilters.Count > 8)
            return BadRequest(new ProblemDetails { Title = "Demasiados filtros configurables.", Detail = "Se permiten hasta ocho filtros configurables.", Status = StatusCodes.Status400BadRequest });
        if (requestedCustomFilters.Count > 0)
        {
            var definitions = await db.CustomFieldDefinitions.AsNoTracking()
                .Where(x => x.IsActive && x.IsPublic && x.IsFilterable)
                .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var filter in requestedCustomFilters)
            {
                if (!definitions.TryGetValue(filter.Key, out var definition) || !TryBuildCustomFieldFilter(definition, filter.Value, out var filterJson))
                    return BadRequest(new ProblemDetails { Title = "Filtro configurable inválido.", Detail = $"El filtro '{filter.Key}' no es válido.", Status = StatusCodes.Status400BadRequest });
                baseQuery = baseQuery.Where(x => EF.Functions.JsonContains(x.CustomFields, filterJson));
            }
        }

        var total = await baseQuery.CountAsync(cancellationToken);

        baseQuery = query.Sort?.ToLowerInvariant() switch
        {
            "price_asc" => baseQuery.OrderBy(x => x.Price),
            "price_desc" => baseQuery.OrderByDescending(x => x.Price),
            "mileage_asc" => baseQuery.OrderBy(x => x.Mileage),
            "year_desc" => baseQuery.OrderByDescending(x => x.Year),
            _ => baseQuery.OrderByDescending(x => x.CreatedAt)
        };

        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new PublicVehicleListItemDto(
                x.Id,
                x.Make.Name,
                x.Model.Name,
                x.Variant != null ? x.Variant.Name : null,
                x.Year,
                x.Mileage,
                x.Price,
                x.Currency,
                x.Condition,
                x.Color,
                x.Transmission,
                x.Fuel,
                x.Drivetrain,
                x.BodyStyle,
                x.Branch.Name,
                x.BranchId,
                null,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(new PublicPagedResult<PublicVehicleListItemDto>(items, total, query.Page, query.PageSize));
    }

    [HttpGet("vehicles/filters")]
    public async Task<ActionResult<PublicFilterOptionsDto>> GetFilterOptions(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        var publishedVehicles = db.Vehicles
            .AsNoTracking()
            .Where(x => x.Status == VehicleStatus.Published && !x.IsDeleted);

        if (!await publishedVehicles.AnyAsync(cancellationToken))
        {
            return Ok(new PublicFilterOptionsDto([], [], [], [], [], [], 0, 0, 0, 0, 0, 0, [VehicleCondition.New, VehicleCondition.Used], []));
        }

        var minPrice = await publishedVehicles.MinAsync(x => x.Price, cancellationToken);
        var maxPrice = await publishedVehicles.MaxAsync(x => x.Price, cancellationToken);
        var minYear = await publishedVehicles.MinAsync(x => x.Year, cancellationToken);
        var maxYear = await publishedVehicles.MaxAsync(x => x.Year, cancellationToken);
        var minMileage = await publishedVehicles.MinAsync(x => x.Mileage, cancellationToken);
        var maxMileage = await publishedVehicles.MaxAsync(x => x.Mileage, cancellationToken);

        var transmissions = await publishedVehicles.Where(x => x.Transmission != null).Select(x => x.Transmission!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var fuels = await publishedVehicles.Where(x => x.Fuel != null).Select(x => x.Fuel!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var bodyStyles = await publishedVehicles.Where(x => x.BodyStyle != null).Select(x => x.BodyStyle!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var drivetrains = await publishedVehicles.Where(x => x.Drivetrain != null).Select(x => x.Drivetrain!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);

        var branchData = await publishedVehicles
            .Select(x => new { x.BranchId, Name = x.Branch.Name })
            .ToListAsync(cancellationToken);
        var branches = branchData.GroupBy(x => new { x.BranchId, x.Name })
            .OrderBy(x => x.Key.Name)
            .Select(x => new PublicBranchOptionDto(x.Key.BranchId, x.Key.Name))
            .ToList();

        // Active makes with their models and variants present in published inventory
        var publishedVehicleData = await publishedVehicles
            .Select(x => new { x.MakeId, MakeName = x.Make.Name, x.ModelId, ModelName = x.Model.Name, x.VariantId, VariantName = x.Variant != null ? x.Variant.Name : null })
            .Distinct()
            .ToListAsync(cancellationToken);

        var makes = publishedVehicleData
            .GroupBy(x => new { x.MakeId, x.MakeName })
            .OrderBy(g => g.Key.MakeName)
            .Select(g => new PublicMakeOptionDto(
                g.Key.MakeId,
                g.Key.MakeName,
                g.GroupBy(m => new { m.ModelId, m.ModelName })
                 .OrderBy(mg => mg.Key.ModelName)
                 .Select(mg => new PublicModelOptionDto(
                     mg.Key.ModelId,
                     mg.Key.ModelName,
                     mg.Where(v => v.VariantId.HasValue)
                       .Select(v => new PublicVariantOptionDto(v.VariantId!.Value, v.VariantName!))
                       .Distinct()
                       .OrderBy(v => v.Name)
                       .ToList()
                 )).ToList()
            )).ToList();

        return Ok(new PublicFilterOptionsDto(
            makes,
            branches,
            transmissions,
            fuels,
            bodyStyles,
            drivetrains,
            minPrice,
            maxPrice,
            minYear,
            maxYear,
            minMileage,
            maxMileage,
            [VehicleCondition.New, VehicleCondition.Used],
            await GetCustomFieldFiltersAsync(publishedVehicles, cancellationToken)
        ));
    }

    [HttpGet("vehicles/featured")]
    public async Task<ActionResult<IReadOnlyCollection<PublicVehicleListItemDto>>> GetFeaturedVehicles(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        var items = await db.Vehicles
            .AsNoTracking()
            .Where(x => x.Status == VehicleStatus.Published && !x.IsDeleted)
            .OrderByDescending(x => x.Price)
            .Take(6)
            .Select(x => new PublicVehicleListItemDto(
                x.Id,
                x.Make.Name,
                x.Model.Name,
                x.Variant != null ? x.Variant.Name : null,
                x.Year,
                x.Mileage,
                x.Price,
                x.Currency,
                x.Condition,
                x.Color,
                x.Transmission,
                x.Fuel,
                x.Drivetrain,
                x.BodyStyle,
                x.Branch.Name,
                x.BranchId,
                null,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("vehicles/recent")]
    public async Task<ActionResult<IReadOnlyCollection<PublicVehicleListItemDto>>> GetRecentVehicles(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=45");

        var items = await db.Vehicles
            .AsNoTracking()
            .Where(x => x.Status == VehicleStatus.Published && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => new PublicVehicleListItemDto(
                x.Id,
                x.Make.Name,
                x.Model.Name,
                x.Variant != null ? x.Variant.Name : null,
                x.Year,
                x.Mileage,
                x.Price,
                x.Currency,
                x.Condition,
                x.Color,
                x.Transmission,
                x.Fuel,
                x.Drivetrain,
                x.BodyStyle,
                x.Branch.Name,
                x.BranchId,
                null,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("vehicles/{id:guid}")]
    public async Task<ActionResult<PublicVehicleDetailDto>> GetVehicleById(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=45");

        var vehicle = await db.Vehicles
            .AsNoTracking()
            .Where(x => x.Id == id && x.Status == VehicleStatus.Published && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                Make = x.Make.Name,
                Model = x.Model.Name,
                Variant = x.Variant != null ? x.Variant.Name : null,
                x.Year, x.Mileage, x.Price, x.Currency, x.Condition, x.Color, x.Transmission, x.Fuel, x.Drivetrain, x.BodyStyle,
                BranchName = x.Branch.Name, x.Branch.Address, x.Branch.Phones, x.Branch.Hours, x.CustomFields, x.PublishedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (vehicle is null) return Unavailable();

        var publicKeys = await db.CustomFieldDefinitions.AsNoTracking().Where(x => x.IsActive && x.IsPublic).Select(x => x.Key).ToListAsync(cancellationToken);
        var equipment = await db.Set<VehicleEquipment>().AsNoTracking()
            .Where(x => x.VehicleId == id && !x.Equipment.IsDeleted)
            .OrderBy(x => x.Equipment.Name)
            .Select(x => x.Equipment.Name)
            .ToListAsync(cancellationToken);
        return Ok(new PublicVehicleDetailDto(
            vehicle.Id, vehicle.Make, vehicle.Model, vehicle.Variant, vehicle.Year, vehicle.Mileage, vehicle.Price, vehicle.Currency,
            vehicle.Condition, vehicle.Color, vehicle.Transmission, vehicle.Fuel, vehicle.Drivetrain, vehicle.BodyStyle,
            new PublicVehicleBranchDto(vehicle.BranchName, vehicle.Address, ParsePhones(vehicle.Phones), ParseHours(vehicle.Hours)),
            equipment, FilterPublicCustomFields(vehicle.CustomFields, publicKeys), vehicle.PublishedAt is null ? null : DateOnly.FromDateTime(vehicle.PublishedAt.Value.UtcDateTime), []));
    }

    [HttpGet("vehicles/{id:guid}/similar")]
    public async Task<ActionResult<IReadOnlyCollection<PublicVehicleListItemDto>>> GetSimilarVehicles(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=45");
        var source = await db.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.Status == VehicleStatus.Published && !x.IsDeleted, cancellationToken);
        if (source is null) return Unavailable();

        var candidates = db.Vehicles.AsNoTracking()
            .Where(x => x.Status == VehicleStatus.Published && !x.IsDeleted && x.Id != source.Id)
            .Where(x => x.MakeId == source.MakeId || x.ModelId == source.ModelId || x.BodyStyle == source.BodyStyle ||
                (x.Price >= source.Price * .75m && x.Price <= source.Price * 1.25m) ||
                (x.Year >= source.Year - 3 && x.Year <= source.Year + 3));

        // Keep the candidate set bounded in SQL before scoring, then make ties reproducible by ID.
        var similar = await candidates.OrderBy(x => x.Id).Take(120)
            .Select(x => new
            {
                x.Id, Make = x.Make.Name, Model = x.Model.Name, Variant = x.Variant != null ? x.Variant.Name : null,
                x.Year, x.Mileage, x.Price, x.Currency, x.Condition, x.Color, x.Transmission, x.Fuel, x.Drivetrain, x.BodyStyle,
                Branch = x.Branch.Name, x.BranchId, x.CreatedAt,
                Score = (x.MakeId == source.MakeId ? 40 : 0) + (x.ModelId == source.ModelId ? 35 : 0) +
                    (x.BodyStyle == source.BodyStyle ? 12 : 0) +
                    (x.Price >= source.Price * .75m && x.Price <= source.Price * 1.25m ? 8 : 0) +
                    (x.Year >= source.Year - 3 && x.Year <= source.Year + 3 ? 3 : 0) +
                    (x.Mileage >= source.Mileage - 30000 && x.Mileage <= source.Mileage + 30000 ? 2 : 0)
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id).Take(6).ToListAsync(cancellationToken);
        return Ok(similar.Select(x => new PublicVehicleListItemDto(x.Id, x.Make, x.Model, x.Variant, x.Year, x.Mileage, x.Price, x.Currency, x.Condition, x.Color, x.Transmission, x.Fuel, x.Drivetrain, x.BodyStyle, x.Branch, x.BranchId, null, x.CreatedAt)).ToList());
    }

    [HttpGet("branches")]
    public async Task<ActionResult<IReadOnlyCollection<PublicBranchDto>>> GetBranches(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "public, max-age=120");

        var branches = await db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Address, x.Phones, x.Hours })
            .ToListAsync(cancellationToken);

        return Ok(branches.Select(x => new PublicBranchDto(x.Id, x.Name, x.Address, ParsePhones(x.Phones), ParseHours(x.Hours))).ToList());
    }
    private NotFoundObjectResult Unavailable()
    {
        Response.Headers["Cache-Control"] = "no-store";
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Vehículo no disponible.",
            Detail = "El vehículo solicitado no se encuentra en el inventario publicado."
        });
    }

    private static IReadOnlyCollection<string> ParsePhones(string raw)
    {
        try { return JsonSerializer.Deserialize<string[]>(raw) ?? []; }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyDictionary<string, string> ParseHours(string raw)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? new Dictionary<string, string>(); }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

    private async Task<IReadOnlyCollection<PublicCustomFieldFilterDto>> GetCustomFieldFiltersAsync(IQueryable<Vehicle> publishedVehicles, CancellationToken cancellationToken)
    {
        var definitions = await db.CustomFieldDefinitions.AsNoTracking().Where(x => x.IsActive && x.IsPublic && x.IsFilterable).OrderBy(x => x.Label).ToListAsync(cancellationToken);
        if (definitions.Count == 0) return [];
        var rawValues = await publishedVehicles.Select(x => x.CustomFields).ToListAsync(cancellationToken);
        return definitions.Select(definition => new PublicCustomFieldFilterDto(definition.Key, definition.Label, definition.Type, rawValues.Select(raw => ReadCustomFieldValue(raw, definition.Key)).Where(value => value is not null).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(value => value).ToList())).ToList();
    }

    private static string? ReadCustomFieldValue(string raw, string key)
    {
        try { using var document = JsonDocument.Parse(raw); return document.RootElement.TryGetProperty(key, out var value) ? value.GetRawText().Trim('"') : null; }
        catch (JsonException) { return null; }
    }

    private static bool TryBuildCustomFieldFilter(CustomFieldDefinition definition, string rawValue, out string json)
    {
        json = string.Empty;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WritePropertyName(definition.Key);
            switch (definition.Type)
            {
                case CustomFieldType.Number when decimal.TryParse(rawValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number): writer.WriteNumberValue(number); break;
                case CustomFieldType.Boolean when bool.TryParse(rawValue, out var boolean): writer.WriteBooleanValue(boolean); break;
                case CustomFieldType.Select when !IsAllowedOption(rawValue, definition.Options): return false;
                case CustomFieldType.Text or CustomFieldType.Select or CustomFieldType.Date: writer.WriteStringValue(rawValue); break;
                default: return false;
            }
            writer.WriteEndObject();
        }
        json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        return true;
    }

    private static bool IsAllowedOption(string value, string options)
    {
        try { using var document = JsonDocument.Parse(options); return document.RootElement.EnumerateArray().Any(x => x.GetString() == value); }
        catch (JsonException) { return false; }
    }

    private static string FilterPublicCustomFields(string raw, IEnumerable<string> publicKeys)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var allowed = new HashSet<string>(publicKeys, StringComparer.Ordinal);
            using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject()) if (allowed.Contains(property.Name)) { writer.WritePropertyName(property.Name); property.Value.WriteTo(writer); }
            writer.WriteEndObject(); writer.Flush(); return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException) { return "{}"; }
    }
}

public sealed record PublicVehicleQuery(
    string? Q = null,
    Guid? MakeId = null,
    Guid? ModelId = null,
    Guid? VariantId = null,
    Guid? BranchId = null,
    int? MinYear = null,
    int? MaxYear = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int? MaxMileage = null,
    VehicleCondition? Condition = null,
    string? Transmission = null,
    string? Fuel = null,
    string? Drivetrain = null,
    string? BodyStyle = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 12
);

public sealed record PublicVehicleListItemDto(
    Guid Id,
    string Make,
    string Model,
    string? Variant,
    int Year,
    int Mileage,
    decimal Price,
    string Currency,
    VehicleCondition Condition,
    string? Color,
    string? Transmission,
    string? Fuel,
    string? Drivetrain,
    string? BodyStyle,
    string Branch,
    Guid BranchId,
    string? ImageUrl,
    DateTimeOffset CreatedAt
);

public sealed record PublicVehicleDetailDto(
    Guid Id,
    string Make,
    string Model,
    string? Variant,
    int Year,
    int Mileage,
    decimal Price,
    string Currency,
    VehicleCondition Condition,
    string? Color,
    string? Transmission,
    string? Fuel,
    string? Drivetrain,
    string? BodyStyle,
    PublicVehicleBranchDto Branch,
    IReadOnlyCollection<string> Equipment,
    string CustomFields,
    DateOnly? PublishedOn,
    IReadOnlyCollection<PublicVehicleImageDto> Images
);

public sealed record PublicPagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);

public sealed record PublicFilterOptionsDto(
    IReadOnlyCollection<PublicMakeOptionDto> Makes,
    IReadOnlyCollection<PublicBranchOptionDto> Branches,
    IReadOnlyCollection<string> Transmissions,
    IReadOnlyCollection<string> Fuels,
    IReadOnlyCollection<string> BodyStyles,
    IReadOnlyCollection<string> Drivetrains,
    decimal MinPrice,
    decimal MaxPrice,
    int MinYear,
    int MaxYear,
    int MinMileage,
    int MaxMileage,
    IReadOnlyCollection<VehicleCondition> Conditions,
    IReadOnlyCollection<PublicCustomFieldFilterDto> CustomFields
);

public sealed record PublicMakeOptionDto(Guid Id, string Name, IReadOnlyCollection<PublicModelOptionDto> Models);
public sealed record PublicModelOptionDto(Guid Id, string Name, IReadOnlyCollection<PublicVariantOptionDto> Variants);
public sealed record PublicVariantOptionDto(Guid Id, string Name);
public sealed record PublicBranchOptionDto(Guid Id, string Name);
public sealed record PublicVehicleBranchDto(string Name, string Address, IReadOnlyCollection<string> Phones, IReadOnlyDictionary<string, string> Hours);
public sealed record PublicVehicleImageDto(string Url, string Alt, int Position);
public sealed record PublicBranchDto(Guid Id, string Name, string Address, IReadOnlyCollection<string> Phones, IReadOnlyDictionary<string, string> Hours);
public sealed record PublicCustomFieldFilterDto(string Key, string Label, CustomFieldType Type, IReadOnlyCollection<string> Values);
