using System.Text.Json;
namespace Dealership.Domain;

public enum VehicleStatus { Draft, InReview, Photography, Inspection, Approved, Published, Reserved, Sold, Delivered, Withdrawn }
public enum VehicleCondition { New, Used }
public enum CustomFieldType { Text, Number, Boolean, Select, Date }

public sealed class Branch : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; public string Address { get; set; } = string.Empty;
    public string Phones { get; set; } = "[]"; public string Hours { get; set; } = "{}"; public string? ManagerName { get; set; } public bool IsActive { get; set; } = true;
}
public abstract class CatalogItem : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; public bool IsDeleted { get; set; }
}
public sealed class Make : CatalogItem { public ICollection<Model> Models { get; } = new List<Model>(); }
public sealed class Model : CatalogItem { public Guid MakeId { get; set; } public Make Make { get; set; } = null!; public ICollection<Variant> Variants { get; } = new List<Variant>(); }
public sealed class Variant : CatalogItem { public Guid ModelId { get; set; } public Model Model { get; set; } = null!; }
public sealed class TechnicalCatalog : CatalogItem { public string Category { get; set; } = string.Empty; }
public sealed class Equipment : CatalogItem { }

public sealed class CustomFieldDefinition : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid(); public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty;
    public CustomFieldType Type { get; set; } public bool IsRequired { get; set; } public string? VehicleType { get; set; } public string Options { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    // Public exposure and filtering are opt-in; existing definitions remain private.
    public bool IsPublic { get; set; }
    public bool IsFilterable { get; set; }
}

public sealed class Vehicle : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid(); public Guid MakeId { get; set; } public Make Make { get; set; } = null!;
    public Guid ModelId { get; set; } public Model Model { get; set; } = null!; public Guid? VariantId { get; set; } public Variant? Variant { get; set; }
    public Guid BranchId { get; set; } public Branch Branch { get; set; } = null!; public int Year { get; set; } public int Mileage { get; set; }
    public decimal Price { get; set; } public string Currency { get; set; } = "MXN"; public VehicleCondition Condition { get; set; }
    public string? Vin { get; private set; } public string? Plate { get; private set; } public string? Color { get; set; }
    public string? Transmission { get; set; } public string? Fuel { get; set; } public string? Drivetrain { get; set; } public string? BodyStyle { get; set; }
    public string CustomFields { get; set; } = "{}"; public VehicleStatus Status { get; private set; } = VehicleStatus.Draft; public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid CreatedById { get; set; } public bool IsDeleted { get; set; } public uint Version { get; private set; }
    public ICollection<VehicleEquipment> Equipment { get; } = new List<VehicleEquipment>(); public ICollection<VehicleStatusHistory> StatusHistory { get; } = new List<VehicleStatusHistory>(); public ICollection<VehiclePriceHistory> PriceHistory { get; } = new List<VehiclePriceHistory>();
    public void SetIdentifiers(string? vin, string? plate) { Vin = Normalize(vin); Plate = Normalize(plate); }
    public void ChangePrice(decimal price, string reason, Guid actorId) { if (price <= 0) throw new DomainRuleException("El precio debe ser mayor a cero."); if (Price == price) return; PriceHistory.Add(new VehiclePriceHistory { VehicleId = Id, PreviousPrice = Price, NewPrice = price, Currency = Currency, Reason = reason, ChangedById = actorId }); Price = price; }
    public void TransitionTo(VehicleStatus next, string reason, Guid actorId, bool manualOverride = false)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleException("El motivo es obligatorio.");
        if (!VehicleStateMachine.CanTransition(Status, next)) throw new DomainRuleException($"No se puede cambiar de {Status} a {next}.");
        if (next == VehicleStatus.Published && !PublicationRequirements.Evaluate(this).All(x => x.IsSatisfied)) throw new DomainRuleException("El vehículo no cumple los requisitos para publicarse.");
        StatusHistory.Add(new VehicleStatusHistory { VehicleId = Id, FromStatus = Status, ToStatus = next, Reason = reason.Trim(), ChangedById = actorId, ManualOverride = manualOverride }); Status = next;
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
public sealed class VehicleEquipment { public Guid VehicleId { get; set; } public Vehicle Vehicle { get; set; } = null!; public Guid EquipmentId { get; set; } public Equipment Equipment { get; set; } = null!; }
public sealed class VehicleStatusHistory : IAuditableEntity { public Guid Id { get; init; } = Guid.NewGuid(); public Guid VehicleId { get; set; } public VehicleStatus FromStatus { get; set; } public VehicleStatus ToStatus { get; set; } public Guid ChangedById { get; set; } public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow; public string Reason { get; set; } = string.Empty; public bool ManualOverride { get; set; } }
public sealed class VehiclePriceHistory : IAuditableEntity { public Guid Id { get; init; } = Guid.NewGuid(); public Guid VehicleId { get; set; } public decimal PreviousPrice { get; set; } public decimal NewPrice { get; set; } public string Currency { get; set; } = "MXN"; public Guid ChangedById { get; set; } public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow; public string Reason { get; set; } = string.Empty; }
public sealed record PublicationRequirement(string Code, string Message, bool IsSatisfied);
public static class PublicationRequirements { public static IReadOnlyList<PublicationRequirement> Evaluate(Vehicle vehicle) => [new("required-data", "Marca, modelo, sucursal, año y kilometraje son obligatorios.", vehicle.MakeId != Guid.Empty && vehicle.ModelId != Guid.Empty && vehicle.BranchId != Guid.Empty && vehicle.Year >= 1900 && vehicle.Mileage >= 0), new("valid-price", "El precio debe ser mayor a cero.", vehicle.Price > 0)]; }
public static class VehicleStateMachine
{
    private static readonly HashSet<(VehicleStatus, VehicleStatus)> Transitions = [(VehicleStatus.Draft, VehicleStatus.InReview), (VehicleStatus.InReview, VehicleStatus.Photography), (VehicleStatus.Photography, VehicleStatus.Inspection), (VehicleStatus.Inspection, VehicleStatus.Approved), (VehicleStatus.Approved, VehicleStatus.Published), (VehicleStatus.Published, VehicleStatus.Reserved), (VehicleStatus.Reserved, VehicleStatus.Sold), (VehicleStatus.Sold, VehicleStatus.Delivered), (VehicleStatus.Draft, VehicleStatus.Withdrawn), (VehicleStatus.InReview, VehicleStatus.Withdrawn), (VehicleStatus.Photography, VehicleStatus.Withdrawn), (VehicleStatus.Inspection, VehicleStatus.Withdrawn), (VehicleStatus.Approved, VehicleStatus.Withdrawn), (VehicleStatus.Published, VehicleStatus.Withdrawn), (VehicleStatus.Reserved, VehicleStatus.Withdrawn)];
    public static bool CanTransition(VehicleStatus from, VehicleStatus to) => Transitions.Contains((from, to));
}
public sealed class DomainRuleException(string message) : Exception(message);
public static class CustomFieldValidator
{
    public static void Validate(string rawValues, IEnumerable<CustomFieldDefinition> definitions)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(rawValues); if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException(); }
        catch (JsonException) { throw new DomainRuleException("Los campos configurables deben ser un objeto JSON válido."); }
        using (document)
        foreach (var definition in definitions.Where(x => x.IsActive))
        {
            if (!document.RootElement.TryGetProperty(definition.Key, out var value)) { if (definition.IsRequired) throw new DomainRuleException($"Falta el campo obligatorio: {definition.Label}."); continue; }
            var valid = definition.Type switch { CustomFieldType.Text => value.ValueKind == JsonValueKind.String, CustomFieldType.Number => value.ValueKind == JsonValueKind.Number, CustomFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False, CustomFieldType.Date => value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out _), CustomFieldType.Select => value.ValueKind == JsonValueKind.String && IsOption(value.GetString(), definition.Options), _ => false };
            if (!valid) throw new DomainRuleException($"El valor de {definition.Label} no corresponde al tipo configurado.");
        }
    }
    private static bool IsOption(string? value, string options) { try { using var optionsDocument = JsonDocument.Parse(options); return optionsDocument.RootElement.EnumerateArray().Any(x => x.GetString() == value); } catch (JsonException) { return false; } }
}
