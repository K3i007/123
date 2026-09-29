using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dealership.Application;
using Dealership.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Dealership.Infrastructure;

public sealed class AuthenticationService(DealershipDbContext db, IConfiguration configuration) : IAuthenticationService
{
    private readonly PasswordHasher<User> passwordHasher = new();
    private readonly byte[] tokenHashKey = Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is missing."));

    public async Task<TokenResult?> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password) == PasswordVerificationResult.Failed) return null;
        return await CreateTokenPairAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await db.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Roles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (token is null || token.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        if (token.UsedAt is not null || token.RevokedAt is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, cancellationToken);
            return null;
        }
        token.UsedAt = DateTimeOffset.UtcNow;
        var result = await CreateTokenPairAsync(token.User, token.FamilyId, cancellationToken);
        token.ReplacedById = db.RefreshTokens.Local.Last(x => x.FamilyId == token.FamilyId && x.Id != token.Id).Id;
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (token is not null) await RevokeFamilyAsync(token.FamilyId, cancellationToken);
    }

    private async Task<TokenResult> CreateTokenPairAsync(User user, Guid familyId, CancellationToken cancellationToken)
    {
        var accessExpires = DateTimeOffset.UtcNow.AddMinutes(10);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, FamilyId = familyId, TokenHash = Hash(refresh), ExpiresAt = DateTimeOffset.UtcNow.AddDays(14) });
        await db.SaveChangesAsync(cancellationToken);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(JwtRegisteredClaimNames.Email, user.Email) };
        claims.AddRange(user.Roles.Select(x => new Claim(ClaimTypes.Role, x.Role.Name)));
        var key = new SymmetricSecurityKey(tokenHashKey);
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: accessExpires.UtcDateTime, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, accessExpires);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens.Where(x => x.FamilyId == familyId && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(tokenHashKey, Encoding.UTF8.GetBytes(value)));
}

public sealed class DevelopmentDataSeeder(DealershipDbContext db, IConfiguration config, IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await db.Roles.AnyAsync(cancellationToken))
        {
            db.Roles.AddRange(SystemRoles.All.Select(name => new Role { Name = name }));
            await db.SaveChangesAsync(cancellationToken);
        }
        if (!environment.IsDevelopment()) return;
        var email = config["SeedAdmin:Email"] ?? throw new InvalidOperationException("SeedAdmin email is required in Development.");
        var password = config["SeedAdmin:Password"] ?? throw new InvalidOperationException("SeedAdmin password is required in Development.");
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Email == email.ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            user = new User { Email = email.Trim().ToLowerInvariant() };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            db.Users.Add(user);
        }
        var administrator = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Administrator, cancellationToken);
        var inventoryManager = await db.Roles.SingleAsync(x => x.Name == SystemRoles.InventoryManager, cancellationToken);
        if (!user.Roles.Any(x => x.RoleId == administrator.Id)) user.Roles.Add(new UserRole { User = user, Role = administrator });
        if (!user.Roles.Any(x => x.RoleId == inventoryManager.Id)) user.Roles.Add(new UserRole { User = user, Role = inventoryManager });
        await EnsureRoleUserAsync(config["SeedInventory:Email"] ?? "inventory@concesionaria.local", config["SeedRolePassword"] ?? password, SystemRoles.InventoryManager, cancellationToken);
        await EnsureRoleUserAsync(config["SeedManager:Email"] ?? "manager@concesionaria.local", config["SeedRolePassword"] ?? password, SystemRoles.Manager, cancellationToken);
        await EnsureRoleUserAsync(config["SeedSales:Email"] ?? "sales@concesionaria.local", config["SeedRolePassword"] ?? password, SystemRoles.Salesperson, cancellationToken);
        if (!await db.Branches.AnyAsync(cancellationToken))
        {
            var downtown = new Branch { Name = "Centro", Address = "Av. Principal 100", Phones = "[\"5550100\"]", Hours = "{\"mon-fri\":\"09:00-18:00\"}", ManagerName = "Operación", IsActive = true };
            var north = new Branch { Name = "Norte", Address = "Blvd. Norte 250", Phones = "[\"5550200\"]", Hours = "{\"mon-sat\":\"09:00-18:00\"}", ManagerName = "Operación", IsActive = true };
            var toyota = new Make { Name = "Toyota" }; var corolla = new Model { Name = "Corolla", Make = toyota }; var sedan = new Variant { Name = "SE", Model = corolla };
            db.AddRange(downtown, north, toyota, corolla, sedan, new TechnicalCatalog { Category = "Transmission", Name = "Automática" }, new TechnicalCatalog { Category = "Fuel", Name = "Gasolina" }, new Equipment { Name = "Cámara de reversa" }, new CustomFieldDefinition { Key = "doors", Label = "Puertas", Type = CustomFieldType.Number, IsRequired = false, Options = "[]" });
            for (var index = 0; index < 20; index++)
            {
                var vehicle = new Vehicle { Make = toyota, Model = corolla, Variant = sedan, Branch = index % 2 == 0 ? downtown : north, Year = 2018 + index % 7, Mileage = index * 8500, Price = 180000m + index * 12500m, Currency = "MXN", Condition = VehicleCondition.Used, Color = "Blanco", Transmission = "Automática", Fuel = "Gasolina", CustomFields = "{\"doors\":4}", CreatedById = user.Id };
                vehicle.SetIdentifiers($"JT{index:D15}", $"DEV{index:D4}"); db.Vehicles.Add(vehicle);
            }
        }
        if (!await db.Makes.AnyAsync(x => x.Name == "Honda", cancellationToken))
        {
            var airport = new Branch { Name = "Aeropuerto", Address = "Carretera Aeropuerto 55", Phones = "[\"5550300\"]", Hours = "{\"mon-sun\":\"10:00-19:00\"}", ManagerName = "Operación", IsActive = true };
            var honda = new Make { Name = "Honda" }; var civic = new Model { Name = "Civic", Make = honda }; var touring = new Variant { Name = "Touring", Model = civic };
            var mazda = new Make { Name = "Mazda" }; var mazda3 = new Model { Name = "Mazda3", Make = mazda }; var signature = new Variant { Name = "Signature", Model = mazda3 };
            db.AddRange(airport, honda, civic, touring, mazda, mazda3, signature, new TechnicalCatalog { Category = "Transmission", Name = "Manual" }, new TechnicalCatalog { Category = "Fuel", Name = "Híbrido" }, new TechnicalCatalog { Category = "BodyStyle", Name = "SUV" }, new Equipment { Name = "Asientos de piel" });
            for (var index = 0; index < 12; index++)
            {
                var isHonda = index % 2 == 0; var vehicle = new Vehicle { Make = isHonda ? honda : mazda, Model = isHonda ? civic : mazda3, Variant = isHonda ? touring : signature, Branch = airport, Year = 2015 + index, Mileage = 5000 + index * 12000, Price = 150000m + index * 30000m, Currency = "MXN", Condition = index % 3 == 0 ? VehicleCondition.New : VehicleCondition.Used, Color = index % 2 == 0 ? "Rojo" : "Gris", Transmission = index % 2 == 0 ? "Manual" : "Automática", Fuel = index % 3 == 0 ? "Híbrido" : "Gasolina", BodyStyle = index % 2 == 0 ? "Sedán" : "SUV", CustomFields = "{\"doors\":4}", CreatedById = user.Id };
                vehicle.SetIdentifiers($"HD{index:D15}", $"MIX{index:D4}"); if (index is 1 or 2) vehicle.TransitionTo(VehicleStatus.InReview, "Semilla", user.Id); if (index == 2) vehicle.TransitionTo(VehicleStatus.Photography, "Semilla", user.Id); db.Vehicles.Add(vehicle);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRoleUserAsync(string email, string password, string roleName, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Email == email.ToLowerInvariant(), cancellationToken);
        if (user is null) { user = new User { Email = email.ToLowerInvariant() }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password); db.Users.Add(user); }
        var role = await db.Roles.SingleAsync(x => x.Name == roleName, cancellationToken);
        if (!user.Roles.Any(x => x.RoleId == role.Id)) user.Roles.Add(new UserRole { User = user, Role = role });
    }
}
