using System.Text.Json;
using Dealership.Application;
using Dealership.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dealership.Infrastructure;

public sealed class DealershipDbContext(DbContextOptions<DealershipDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();
    public DbSet<FavoriteVehicle> FavoriteVehicles => Set<FavoriteVehicle>();
    public DbSet<SavedComparisonVehicle> SavedComparisonVehicles => Set<SavedComparisonVehicle>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AccountRateLimit> AccountRateLimits => Set<AccountRateLimit>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Branch> Branches => Set<Branch>(); public DbSet<Make> Makes => Set<Make>(); public DbSet<Model> Models => Set<Model>(); public DbSet<Variant> Variants => Set<Variant>();
    public DbSet<TechnicalCatalog> TechnicalCatalogs => Set<TechnicalCatalog>(); public DbSet<Equipment> Equipment => Set<Equipment>(); public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>(); public DbSet<VehicleStatusHistory> VehicleStatusHistories => Set<VehicleStatusHistory>(); public DbSet<VehiclePriceHistory> VehiclePriceHistories => Set<VehiclePriceHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>(entity => { entity.HasIndex(x => x.Email).IsUnique(); entity.Property(x => x.Email).HasMaxLength(256); entity.Property(x => x.DisplayName).HasMaxLength(160); entity.Property(x => x.Phone).HasMaxLength(32); entity.Property(x => x.Language).HasMaxLength(16); entity.Property(x => x.PrivacyPolicyVersion).HasMaxLength(32); });
        builder.Entity<Role>(entity => { entity.HasIndex(x => x.Name).IsUnique(); entity.Property(x => x.Name).HasMaxLength(100); });
        builder.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        builder.Entity<UserRole>().HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId);
        builder.Entity<UserRole>().HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => new { x.FamilyId, x.RevokedAt });
        builder.Entity<OneTimeToken>(entity => { entity.HasIndex(x => x.TokenHash).IsUnique(); entity.HasIndex(x => new { x.UserId, x.Purpose, x.UsedAt }); });
        builder.Entity<FavoriteVehicle>(entity => { entity.HasIndex(x => new { x.UserId, x.VehicleId }).IsUnique(); entity.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId); });
        builder.Entity<SavedComparisonVehicle>(entity => { entity.HasIndex(x => new { x.UserId, x.VehicleId }).IsUnique(); entity.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId); });
        builder.Entity<SecurityEvent>(entity => { entity.HasIndex(x => new { x.UserId, x.OccurredAt }); entity.Property(x => x.EventType).HasMaxLength(80); entity.Property(x => x.IpHash).HasMaxLength(128); });
        builder.Entity<AccountRateLimit>(entity => { entity.HasIndex(x => new { x.Purpose, x.AccountHash }).IsUnique(); entity.HasIndex(x => x.ExpiresAt); entity.Property(x => x.AccountHash).HasMaxLength(128); entity.Property(x => x.Purpose).HasMaxLength(40); });
        builder.Entity<AuditLog>().HasIndex(x => x.OccurredAt);
        builder.Entity<Branch>(x => { x.Property(p => p.Name).HasMaxLength(160); x.HasIndex(p => p.Name).IsUnique(); });
        builder.Entity<Make>(x => { x.Property(p => p.Name).HasMaxLength(100); x.HasIndex(p => new { p.Name, p.IsDeleted }).IsUnique(); });
        builder.Entity<Model>(x => { x.HasIndex(p => new { p.MakeId, p.Name, p.IsDeleted }).IsUnique(); });
        builder.Entity<Variant>(x => { x.HasIndex(p => new { p.ModelId, p.Name, p.IsDeleted }).IsUnique(); });
        builder.Entity<TechnicalCatalog>(x => x.HasIndex(p => new { p.Category, p.Name, p.IsDeleted }).IsUnique());
        builder.Entity<Equipment>(x => x.HasIndex(p => new { p.Name, p.IsDeleted }).IsUnique());
        builder.Entity<CustomFieldDefinition>(x => { x.HasIndex(p => p.Key).IsUnique(); x.Property(p => p.Options).HasColumnType("jsonb"); });
        builder.Entity<Vehicle>(x =>
        {
            x.Property(p => p.Price).HasPrecision(18, 2); x.Property(p => p.CustomFields).HasColumnType("jsonb"); x.Property(p => p.Version).IsRowVersion();
            x.HasIndex(p => p.CustomFields).HasMethod("gin"); x.HasIndex(p => p.Vin).IsUnique().HasFilter("\"Vin\" IS NOT NULL AND \"IsDeleted\" = false"); x.HasIndex(p => p.Plate).IsUnique().HasFilter("\"Plate\" IS NOT NULL AND \"IsDeleted\" = false");
            x.HasIndex(p => new { p.Status, p.BranchId }); x.HasIndex(p => new { p.MakeId, p.ModelId, p.Year }); x.HasIndex(p => p.Price);
            var published = (int)VehicleStatus.Published;
            x.HasIndex(p => new { p.ModelId, p.MakeId, p.Year, p.Price, p.Mileage })
             .HasDatabaseName("IX_Vehicles_Published_Catalog")
             .HasFilter($"\"Status\" = {published} AND \"IsDeleted\" = false");
        });
        builder.Entity<VehicleEquipment>().HasKey(x => new { x.VehicleId, x.EquipmentId });
        builder.Entity<VehiclePriceHistory>(x => { x.Property(p => p.PreviousPrice).HasPrecision(18, 2); x.Property(p => p.NewPrice).HasPrecision(18, 2); x.HasIndex(p => new { p.VehicleId, p.ChangedAt }); });
        builder.Entity<VehicleStatusHistory>().HasIndex(p => new { p.VehicleId, p.ChangedAt });
    }
}

public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, ICorrelationContext correlation) : SaveChangesInterceptor
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase) { "PasswordHash", "TokenHash", "RefreshToken", "AccessToken", "Password", "Email", "DisplayName", "Phone", "PrivacyPolicyVersion" };
    private static readonly HashSet<string> SensitiveEntities = new(StringComparer.Ordinal) { nameof(User), nameof(RefreshToken), nameof(OneTimeToken), nameof(SecurityEvent) };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAudits(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAudits(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAudits(DbContext? context)
    {
        if (context is not DealershipDbContext db) return;
        var entries = db.ChangeTracker.Entries().Where(x => x.Entity is IAuditableEntity && x.Entity is not AuditLog && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in entries)
        {
            var sensitiveEntity = SensitiveEntities.Contains(entry.Metadata.ClrType.Name);
            var oldValues = sensitiveEntity || entry.State == EntityState.Added ? null : Serialize(entry, true);
            var newValues = sensitiveEntity || entry.State == EntityState.Deleted ? null : Serialize(entry, false);
            db.AuditLogs.Add(new AuditLog { OccurredAt = DateTimeOffset.UtcNow, ActorId = currentUser.Id, Action = entry.State.ToString(), EntityName = entry.Metadata.ClrType.Name, EntityId = ((IAuditableEntity)entry.Entity).Id.ToString(), OldValues = oldValues, NewValues = newValues, CorrelationId = correlation.Id });
        }
    }

    private static string Serialize(EntityEntry entry, bool original) => JsonSerializer.Serialize(entry.Properties.Where(x => !SensitiveProperties.Contains(x.Metadata.Name)).ToDictionary(x => x.Metadata.Name, x => original ? x.OriginalValue : x.CurrentValue));
}
