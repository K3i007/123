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

public sealed class AuthenticationService(DealershipDbContext db, IConfiguration configuration, IPasswordWorkService passwords) : IAuthenticationService
{
    private readonly byte[] tokenHashKey = Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is missing."));

    public async Task<TokenResult?> LoginAsync(LoginCommand command, CancellationToken cancellationToken, AccountType? expectedAccountType = null)
    {
        var user = await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            // Match the PBKDF2 work of a real password verification to reduce enumeration timing signal.
            _ = passwords.VerifyUnknown(command.Password);
            return null;
        }
        var passwordValid = passwords.Verify(user, user.PasswordHash, command.Password);
        var valid = passwordValid && user.IsActive && (expectedAccountType is null || user.AccountType == expectedAccountType);
        if (!valid)
        {
            user.FailedLoginCount++;
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        user.FailedLoginCount = 0;
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
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new("account_type", user.AccountType.ToString()) };
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

        // Technical catalogs
        string[] transmissions = ["Automática", "Manual", "CVT"];
        string[] fuels = ["Gasolina", "Diésel", "Híbrido", "Eléctrico"];
        string[] bodyStyles = ["Sedán", "SUV", "Hatchback", "Pickup", "Coupé"];
        string[] drivetrains = ["Delantera", "Trasera", "AWD", "4x4"];

        foreach (var t in transmissions) if (!await db.TechnicalCatalogs.AnyAsync(x => x.Category == "Transmission" && x.Name == t, cancellationToken)) db.TechnicalCatalogs.Add(new TechnicalCatalog { Category = "Transmission", Name = t });
        foreach (var f in fuels) if (!await db.TechnicalCatalogs.AnyAsync(x => x.Category == "Fuel" && x.Name == f, cancellationToken)) db.TechnicalCatalogs.Add(new TechnicalCatalog { Category = "Fuel", Name = f });
        foreach (var b in bodyStyles) if (!await db.TechnicalCatalogs.AnyAsync(x => x.Category == "BodyStyle" && x.Name == b, cancellationToken)) db.TechnicalCatalogs.Add(new TechnicalCatalog { Category = "BodyStyle", Name = b });
        foreach (var d in drivetrains) if (!await db.TechnicalCatalogs.AnyAsync(x => x.Category == "Drivetrain" && x.Name == d, cancellationToken)) db.TechnicalCatalogs.Add(new TechnicalCatalog { Category = "Drivetrain", Name = d });

        if (!await db.Branches.AnyAsync(x => x.Name == "Centro", cancellationToken)) db.Branches.Add(new Branch { Name = "Centro", Address = "Av. Principal 100", Phones = "[\"5550100\"]", Hours = "{\"mon-fri\":\"09:00-18:00\"}", ManagerName = "Roberto Soto", IsActive = true });
        if (!await db.Branches.AnyAsync(x => x.Name == "Norte", cancellationToken)) db.Branches.Add(new Branch { Name = "Norte", Address = "Blvd. Norte 250", Phones = "[\"5550200\"]", Hours = "{\"mon-sat\":\"09:00-18:00\"}", ManagerName = "Lucía Morales", IsActive = true });
        if (!await db.Branches.AnyAsync(x => x.Name == "Aeropuerto", cancellationToken)) db.Branches.Add(new Branch { Name = "Aeropuerto", Address = "Carretera Aeropuerto 55", Phones = "[\"5550300\"]", Hours = "{\"mon-sun\":\"10:00-19:00\"}", ManagerName = "Carlos Vera", IsActive = true });
        var doorsDefinition = await db.CustomFieldDefinitions.SingleOrDefaultAsync(x => x.Key == "doors", cancellationToken);
        if (doorsDefinition is null)
            db.CustomFieldDefinitions.Add(new CustomFieldDefinition { Key = "doors", Label = "Puertas", Type = CustomFieldType.Number, IsRequired = false, Options = "[]", IsPublic = true, IsFilterable = true });
        else
        {
            doorsDefinition.IsPublic = true;
            doorsDefinition.IsFilterable = true;
        }
        await db.SaveChangesAsync(cancellationToken);

        var branchesList = await db.Branches.Where(x => x.IsActive).ToListAsync(cancellationToken);

        // Brands and Models
        var brandDefinitions = new (string Make, (string Model, string[] Variants)[] Models)[]
        {
            ("BMW", [("Serie 3", ["330i Sport", "M340i"]), ("X3", ["xDrive30i"]), ("X5", ["xDrive40i"])]),
            ("Toyota", [("Corolla", ["SE", "LE"]), ("RAV4", ["XLE", "Hybrid"]), ("Hilux", ["Doble Cabina SR"])]),
            ("Honda", [("Civic", ["Touring", "Sport"]), ("CR-V", ["Touring"]), ("HR-V", ["Uniq"])]),
            ("Ford", [("Mustang", ["GT V8"]), ("Explorer", ["Limited"]), ("Ranger", ["XLT 4x4"])]),
            ("Volkswagen", [("Jetta", ["Highline"]), ("Golf", ["GTI"]), ("Tiguan", ["R-Line"])]),
            ("Mazda", [("Mazda 3", ["i Grand Touring"]), ("CX-5", ["Signature"])]),
            ("Mercedes-Benz", [("Clase C", ["C200 Exclusive"]), ("GLC", ["GLC 300"])]),
            ("Nissan", [("Sentra", ["SR Platinum"]), ("Frontier", ["PRO-4X"])])
        };

        foreach (var (makeName, models) in brandDefinitions)
        {
            var make = await db.Makes.SingleOrDefaultAsync(x => x.Name == makeName, cancellationToken);
            if (make is null)
            {
                make = new Make { Name = makeName };
                db.Makes.Add(make);
                await db.SaveChangesAsync(cancellationToken);
            }
            foreach (var (modelName, variants) in models)
            {
                var model = await db.Models.SingleOrDefaultAsync(x => x.MakeId == make.Id && x.Name == modelName, cancellationToken);
                if (model is null)
                {
                    model = new Model { Name = modelName, MakeId = make.Id };
                    db.Models.Add(model);
                    await db.SaveChangesAsync(cancellationToken);
                }
                foreach (var variantName in variants)
                {
                    if (!await db.Variants.AnyAsync(x => x.ModelId == model.Id && x.Name == variantName, cancellationToken))
                    {
                        db.Variants.Add(new Variant { Name = variantName, ModelId = model.Id });
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        var publishedCount = await db.Vehicles.CountAsync(x => x.Status == VehicleStatus.Published && !x.IsDeleted, cancellationToken);
        if (publishedCount < 32)
        {
            var allMakes = await db.Makes.Include(x => x.Models).ThenInclude(x => x.Variants).ToListAsync(cancellationToken);
            var seedCatalogVehicles = new List<(string Make, string Model, string Variant, int Year, int Mileage, decimal Price, VehicleCondition Condition, string Color, string Transmission, string Fuel, string Drivetrain, string BodyStyle)>
            {
                ("BMW", "Serie 3", "330i Sport", 2023, 12000, 780000m, VehicleCondition.Used, "Gris Grafito", "Automática", "Gasolina", "Trasera", "Sedán"),
                ("BMW", "Serie 3", "M340i", 2024, 4500, 1150000m, VehicleCondition.Used, "Azul Portimao", "Automática", "Gasolina", "AWD", "Sedán"),
                ("BMW", "X3", "xDrive30i", 2022, 28000, 890000m, VehicleCondition.Used, "Blanco Alpino", "Automática", "Gasolina", "AWD", "SUV"),
                ("BMW", "X5", "xDrive40i", 2023, 18500, 1250000m, VehicleCondition.Used, "Negro Safiro", "Automática", "Híbrido", "AWD", "SUV"),

                ("Toyota", "Corolla", "SE", 2022, 34000, 365000m, VehicleCondition.Used, "Plata", "CVT", "Gasolina", "Delantera", "Sedán"),
                ("Toyota", "Corolla", "LE", 2020, 62000, 295000m, VehicleCondition.Used, "Blanco", "Automática", "Gasolina", "Delantera", "Sedán"),
                ("Toyota", "RAV4", "Hybrid", 2023, 15000, 590000m, VehicleCondition.Used, "Gris Metálico", "CVT", "Híbrido", "AWD", "SUV"),
                ("Toyota", "RAV4", "XLE", 2021, 48000, 485000m, VehicleCondition.Used, "Azul Marino", "Automática", "Gasolina", "Delantera", "SUV"),
                ("Toyota", "Hilux", "Doble Cabina SR", 2022, 52000, 495000m, VehicleCondition.Used, "Blanco", "Manual", "Diésel", "4x4", "Pickup"),

                ("Honda", "Civic", "Touring", 2023, 16000, 465000m, VehicleCondition.Used, "Gris Sónico", "CVT", "Gasolina", "Delantera", "Sedán"),
                ("Honda", "Civic", "Sport", 2024, 0, 525000m, VehicleCondition.New, "Rojo Rallye", "CVT", "Gasolina", "Delantera", "Sedán"),
                ("Honda", "CR-V", "Touring", 2022, 31000, 560000m, VehicleCondition.Used, "Plata Lunar", "CVT", "Gasolina", "AWD", "SUV"),
                ("Honda", "HR-V", "Uniq", 2021, 41000, 385000m, VehicleCondition.Used, "Blanco Platino", "CVT", "Gasolina", "Delantera", "SUV"),

                ("Ford", "Mustang", "GT V8", 2021, 23000, 790000m, VehicleCondition.Used, "Rojo Furia", "Manual", "Gasolina", "Trasera", "Coupé"),
                ("Ford", "Explorer", "Limited", 2022, 38000, 750000m, VehicleCondition.Used, "Negro Ágata", "Automática", "Gasolina", "AWD", "SUV"),
                ("Ford", "Ranger", "XLT 4x4", 2023, 29000, 620000m, VehicleCondition.Used, "Gris Carbón", "Automática", "Diésel", "4x4", "Pickup"),

                ("Volkswagen", "Jetta", "Highline", 2022, 36000, 395000m, VehicleCondition.Used, "Blanco Puro", "Automática", "Gasolina", "Delantera", "Sedán"),
                ("Volkswagen", "Jetta", "Highline", 2020, 68000, 315000m, VehicleCondition.Used, "Negro Profundo", "Manual", "Gasolina", "Delantera", "Sedán"),
                ("Volkswagen", "Golf", "GTI", 2021, 32000, 540000m, VehicleCondition.Used, "Rojo Tornado", "Automática", "Gasolina", "Delantera", "Hatchback"),
                ("Volkswagen", "Tiguan", "R-Line", 2023, 21000, 580000m, VehicleCondition.Used, "Plata Pirita", "Automática", "Gasolina", "AWD", "SUV"),

                ("Mazda", "Mazda 3", "i Grand Touring", 2023, 19000, 410000m, VehicleCondition.Used, "Rojo Soul Crystal", "Automática", "Gasolina", "Delantera", "Sedán"),
                ("Mazda", "Mazda 3", "i Grand Touring", 2022, 42000, 360000m, VehicleCondition.Used, "Gris Titanio", "Manual", "Gasolina", "Delantera", "Hatchback"),
                ("Mazda", "CX-5", "Signature", 2023, 24000, 565000m, VehicleCondition.Used, "Blanco Perlado", "Automática", "Gasolina", "AWD", "SUV"),

                ("Mercedes-Benz", "Clase C", "C200 Exclusive", 2022, 26000, 790000m, VehicleCondition.Used, "Gris Selenita", "Automática", "Híbrido", "Trasera", "Sedán"),
                ("Mercedes-Benz", "GLC", "GLC 300", 2023, 17000, 980000m, VehicleCondition.Used, "Azul Cavansita", "Automática", "Gasolina", "AWD", "SUV"),

                ("Nissan", "Sentra", "SR Platinum", 2023, 22000, 395000m, VehicleCondition.Used, "Naranja Solar", "CVT", "Gasolina", "Delantera", "Sedán"),
                ("Nissan", "Sentra", "SR Platinum", 2021, 55000, 325000m, VehicleCondition.Used, "Blanco Perlado", "Manual", "Gasolina", "Delantera", "Sedán"),
                ("Nissan", "Frontier", "PRO-4X", 2024, 0, 745000m, VehicleCondition.New, "Gris Volcánico", "Automática", "Gasolina", "4x4", "Pickup"),

                ("Toyota", "Corolla", "SE", 2024, 0, 440000m, VehicleCondition.New, "Azul Eléctrico", "CVT", "Gasolina", "Delantera", "Sedán"),
                ("BMW", "Serie 3", "330i Sport", 2021, 48000, 680000m, VehicleCondition.Used, "Negro", "Automática", "Gasolina", "Trasera", "Sedán"),
                ("Volkswagen", "Golf", "GTI", 2019, 74000, 430000m, VehicleCondition.Used, "Blanco", "Manual", "Gasolina", "Delantera", "Hatchback"),
                ("Ford", "Mustang", "GT V8", 2024, 1200, 1100000m, VehicleCondition.Used, "Azul Grabber", "Automática", "Gasolina", "Trasera", "Coupé"),
                ("Honda", "CR-V", "Touring", 2024, 0, 715000m, VehicleCondition.New, "Plata Lunar", "CVT", "Híbrido", "AWD", "SUV"),
                ("Mercedes-Benz", "Clase C", "C200 Exclusive", 2024, 0, 1050000m, VehicleCondition.New, "Blanco Polar", "Automática", "Híbrido", "Trasera", "Sedán"),
                ("Toyota", "Hilux", "Doble Cabina SR", 2024, 0, 580000m, VehicleCondition.New, "Rojo", "Manual", "Diésel", "4x4", "Pickup"),
                ("Nissan", "Frontier", "PRO-4X", 2022, 38000, 595000m, VehicleCondition.Used, "Negro", "Automática", "Diésel", "4x4", "Pickup")
            };

            for (var i = 0; i < seedCatalogVehicles.Count; i++)
            {
                var item = seedCatalogVehicles[i];
                var make = allMakes.First(m => m.Name == item.Make);
                var model = make.Models.First(m => m.Name == item.Model);
                var variant = model.Variants.FirstOrDefault(v => v.Name == item.Variant);
                var branch = branchesList[i % branchesList.Count];

                var vehicle = new Vehicle
                {
                    Make = make,
                    MakeId = make.Id,
                    Model = model,
                    ModelId = model.Id,
                    Variant = variant,
                    VariantId = variant?.Id,
                    Branch = branch,
                    BranchId = branch.Id,
                    Year = item.Year,
                    Mileage = item.Mileage,
                    Price = item.Price,
                    Currency = "MXN",
                    Condition = item.Condition,
                    Color = item.Color,
                    Transmission = item.Transmission,
                    Fuel = item.Fuel,
                    Drivetrain = item.Drivetrain,
                    BodyStyle = item.BodyStyle,
                    CustomFields = "{\"doors\":4}",
                    CreatedById = user.Id
                };
                vehicle.SetIdentifiers($"PUB{i:D14}", $"PUB{i:D3}");

                // Transition through state machine to Published
                vehicle.TransitionTo(VehicleStatus.InReview, "Semilla catálogo", user.Id);
                vehicle.TransitionTo(VehicleStatus.Photography, "Semilla catálogo", user.Id);
                vehicle.TransitionTo(VehicleStatus.Inspection, "Semilla catálogo", user.Id, manualOverride: true);
                vehicle.TransitionTo(VehicleStatus.Approved, "Semilla catálogo", user.Id, manualOverride: true);
                vehicle.TransitionTo(VehicleStatus.Published, "Semilla catálogo", user.Id);

                db.Vehicles.Add(vehicle);
            }

            // Also seed non-published vehicles for testing isolation
            var firstMake = allMakes.First();
            var firstModel = firstMake.Models.First();
            var draftVehicle = new Vehicle { Make = firstMake, MakeId = firstMake.Id, Model = firstModel, ModelId = firstModel.Id, Branch = branchesList[0], BranchId = branchesList[0].Id, Year = 2021, Mileage = 15000, Price = 300000m, Color = "Gris", CreatedById = user.Id };
            draftVehicle.SetIdentifiers("DFT000000000001", "DFT001");
            db.Vehicles.Add(draftVehicle);

            var withdrawnVehicle = new Vehicle { Make = firstMake, MakeId = firstMake.Id, Model = firstModel, ModelId = firstModel.Id, Branch = branchesList[0], BranchId = branchesList[0].Id, Year = 2020, Mileage = 45000, Price = 270000m, Color = "Rojo", CreatedById = user.Id };
            withdrawnVehicle.SetIdentifiers("WTH000000000001", "WTH001");
            withdrawnVehicle.TransitionTo(VehicleStatus.Withdrawn, "Retirado de venta", user.Id);
            db.Vehicles.Add(withdrawnVehicle);

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureRoleUserAsync(string email, string password, string roleName, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Email == email.ToLowerInvariant(), cancellationToken);
        if (user is null) { user = new User { Email = email.ToLowerInvariant() }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password); db.Users.Add(user); }
        var role = await db.Roles.SingleAsync(x => x.Name == roleName, cancellationToken);
        if (!user.Roles.Any(x => x.RoleId == role.Id)) user.Roles.Add(new UserRole { User = user, Role = role });
    }
}
