namespace Dealership.Domain;

public interface IAuditableEntity { Guid Id { get; } }

public sealed class User : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<UserRole> Roles { get; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
}

public sealed class Role : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRole> Users { get; } = new List<UserRole>();
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public sealed class RefreshToken : IAuditableEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; }
    public string? ActorId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

public static class SystemRoles
{
    public const string Customer = "Customer";
    public const string Salesperson = "Salesperson";
    public const string InventoryManager = "InventoryManager";
    public const string Photographer = "Photographer";
    public const string Inspector = "Inspector";
    public const string Manager = "Manager";
    public const string Administrator = "Administrator";
    public static readonly string[] All = [Customer, Salesperson, InventoryManager, Photographer, Inspector, Manager, Administrator];
}
