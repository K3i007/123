using System.Security.Cryptography;
using System.Text;
using Dealership.Application;
using Dealership.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dealership.Infrastructure;

public sealed class DevelopmentEmailSender(IHostEnvironment environment) : IEmailSender
{
    public bool IsAvailable => environment.IsDevelopment();

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("No email provider is configured.");
        // Deliberately do not log recipient or token-bearing body. Local delivery is an explicit development aid.
        var directory = Path.Combine(AppContext.BaseDirectory, "development-emails");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{Guid.NewGuid():N}.txt");
        return File.WriteAllTextAsync(file, $"To: {recipient}\nSubject: {subject}\n\n{body}", cancellationToken);
    }
}

public sealed class CustomerAccountService(DealershipDbContext db, IConfiguration configuration, IEmailSender emailSender, IPasswordWorkService passwords, IBackgroundTaskQueue? backgroundTasks = null) : ICustomerAccountService
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "123456789012", "qwertyuiopasdf", "contrasena123", "contrasena1234", "letmeinletmein"
    };
    private readonly byte[] hashKey = Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is missing."));

    public async Task<bool> RegisterAsync(CustomerRegistrationCommand command, CancellationToken cancellationToken)
    {
        ValidatePassword(command.Password);
        if (!emailSender.IsAvailable) return false;
        var email = NormalizeEmail(command.Email);
        var existing = await db.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (existing)
        {
            // New registrations hash a password. Do the same expensive work when the account
            // already exists, while delivery itself is always outside the request path.
            _ = passwords.Hash(new User(), command.Password);
            await QueueEmailAsync("delivery@invalid.local", "Verifica tu correo", "", cancellationToken);
            return true;
        }

        var customerRole = await db.Roles.SingleAsync(x => x.Name == SystemRoles.Customer, cancellationToken);
        var user = new User
        {
            Email = email,
            PasswordHash = passwords.Hash(new User(), command.Password),
            AccountType = AccountType.Customer,
            DisplayName = command.Name.Trim(),
            Phone = NormalizePhone(command.Phone),
            PrivacyPolicyVersion = command.PrivacyPolicyVersion.Trim(),
            PrivacyAcceptedAt = DateTimeOffset.UtcNow
        };
        user.Roles.Add(new UserRole { User = user, Role = customerRole });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        var token = await IssueTokenAsync(user.Id, OneTimeTokenPurpose.EmailVerification, TimeSpan.FromHours(24), cancellationToken);
        await QueueEmailAsync(user.Email, "Verifica tu correo", $"Código de verificación: {token}", cancellationToken);
        return true;
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken) =>
        await ConsumeTokenAsync(token, OneTimeTokenPurpose.EmailVerification, async user =>
        {
            user.EmailVerified = true;
            await Task.CompletedTask;
        }, cancellationToken);

    public async Task RequestEmailVerificationAsync(string email, CancellationToken cancellationToken)
    {
        if (!emailSender.IsAvailable) return;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == NormalizeEmail(email) && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null || user.EmailVerified)
        {
            await QueueEmailAsync("delivery@invalid.local", "Verifica tu correo", "", cancellationToken);
            return;
        }
        var token = await IssueTokenAsync(user.Id, OneTimeTokenPurpose.EmailVerification, TimeSpan.FromHours(24), cancellationToken);
        await QueueEmailAsync(user.Email, "Verifica tu correo", $"Código de verificación: {token}", cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        if (!emailSender.IsAvailable) return;
        var normalized = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == normalized && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null)
        {
            await QueueEmailAsync("delivery@invalid.local", "Restablece tu contraseña", "", cancellationToken);
            return;
        }
        var token = await IssueTokenAsync(user.Id, OneTimeTokenPurpose.PasswordReset, TimeSpan.FromMinutes(20), cancellationToken);
        await QueueEmailAsync(user.Email, "Restablece tu contraseña", $"Código de restablecimiento: {token}", cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken)
    {
        ValidatePassword(password);
        return await ConsumeTokenAsync(token, OneTimeTokenPurpose.PasswordReset, async user =>
        {
            user.PasswordHash = passwords.Hash(user, password);
            await db.OneTimeTokens.Where(x => x.UserId == user.Id && x.Purpose == OneTimeTokenPurpose.PasswordReset && x.UsedAt == null)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsedAt, DateTimeOffset.UtcNow), cancellationToken);
            await RevokeRefreshTokensAsync(x => x.UserId == user.Id && x.RevokedAt == null, cancellationToken);
        }, cancellationToken);
    }

    public async Task<CustomerProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.AccountType == AccountType.Customer, cancellationToken);
        return user is null ? null : ToProfile(user);
    }

    public async Task<bool> UpdateProfileAsync(Guid userId, string name, string? phone, string language, bool marketingConsent, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null) return false;
        user.DisplayName = name.Trim();
        user.Phone = NormalizePhone(phone);
        user.Language = string.IsNullOrWhiteSpace(language) ? "es-MX" : language.Trim();
        user.MarketingConsent = marketingConsent;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CustomerExport?> ExportAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, password)) return null;
        var favorites = await db.FavoriteVehicles.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.VehicleId).ToListAsync(cancellationToken);
        var comparisons = await db.SavedComparisonVehicles.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.VehicleId).ToListAsync(cancellationToken);
        return new CustomerExport(user.Id, user.Email, user.DisplayName ?? string.Empty, user.Phone, user.Language, user.MarketingConsent, user.PrivacyPolicyVersion ?? string.Empty, user.PrivacyAcceptedAt, favorites, comparisons);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, currentPassword)) return false;
        user.PasswordHash = passwords.Hash(user, newPassword);
        await RevokeRefreshTokensAsync(x => x.UserId == userId && x.RevokedAt == null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.AccountType == AccountType.Customer, cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, password)) return false;
        db.FavoriteVehicles.RemoveRange(db.FavoriteVehicles.Where(x => x.UserId == userId));
        db.SavedComparisonVehicles.RemoveRange(db.SavedComparisonVehicles.Where(x => x.UserId == userId));
        db.OneTimeTokens.RemoveRange(db.OneTimeTokens.Where(x => x.UserId == userId));
        await RevokeRefreshTokensAsync(x => x.UserId == userId && x.RevokedAt == null, cancellationToken);
        user.Email = $"deleted-{user.Id:N}@deleted.invalid";
        user.DisplayName = null; user.Phone = null; user.PasswordHash = passwords.Hash(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))); user.EmailVerified = false; user.IsActive = false; user.MarketingConsent = false; user.PrivacyPolicyVersion = null; user.PrivacyAcceptedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<CustomerSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.AsNoTracking().Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.UsedAt ?? x.ExpiresAt).Select(x => new CustomerSession(x.Id, x.ExpiresAt, x.UsedAt, false)).ToListAsync(cancellationToken);

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var count = await RevokeRefreshTokensAsync(x => x.Id == sessionId && x.UserId == userId && x.RevokedAt == null, cancellationToken);
        return count == 1;
    }

    public Task<int> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        RevokeRefreshTokensAsync(x => x.UserId == userId && x.RevokedAt == null, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> MergeFavoritesAsync(Guid userId, IReadOnlyCollection<Guid> vehicleIds, CancellationToken cancellationToken)
    {
        if (!await IsVerifiedCustomerAsync(userId, cancellationToken)) return [];
        var requested = vehicleIds.Where(x => x != Guid.Empty).Distinct().Take(100).ToArray();
        var published = await db.Vehicles.Where(x => requested.Contains(x.Id) && x.Status == VehicleStatus.Published && !x.IsDeleted).Select(x => x.Id).ToListAsync(cancellationToken);
        var existing = await db.FavoriteVehicles.Where(x => x.UserId == userId).Select(x => x.VehicleId).ToListAsync(cancellationToken);
        foreach (var id in published.Where(x => !existing.Contains(x)).Take(100 - existing.Count)) db.FavoriteVehicles.Add(new FavoriteVehicle { UserId = userId, VehicleId = id });
        await db.SaveChangesAsync(cancellationToken);
        return await GetFavoriteIdsAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetFavoriteIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.FavoriteVehicles.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Select(x => x.VehicleId).ToListAsync(cancellationToken);

    public async Task<bool> SetFavoriteAsync(Guid userId, Guid vehicleId, bool favorite, CancellationToken cancellationToken)
    {
        if (!await IsVerifiedCustomerAsync(userId, cancellationToken)) return false;
        var existing = await db.FavoriteVehicles.SingleOrDefaultAsync(x => x.UserId == userId && x.VehicleId == vehicleId, cancellationToken);
        if (!favorite)
        {
            if (existing is not null) db.FavoriteVehicles.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        if (existing is not null) return true;
        if (await db.FavoriteVehicles.CountAsync(x => x.UserId == userId, cancellationToken) >= 100) return false;
        if (!await db.Vehicles.AnyAsync(x => x.Id == vehicleId && x.Status == VehicleStatus.Published && !x.IsDeleted, cancellationToken)) return false;
        db.FavoriteVehicles.Add(new FavoriteVehicle { UserId = userId, VehicleId = vehicleId });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<Guid>> SetComparisonAsync(Guid userId, IReadOnlyCollection<Guid> vehicleIds, CancellationToken cancellationToken)
    {
        if (!await IsVerifiedCustomerAsync(userId, cancellationToken)) return [];
        var requested = vehicleIds.Where(x => x != Guid.Empty).Distinct().Take(4).ToArray();
        var published = await db.Vehicles.Where(x => requested.Contains(x.Id) && x.Status == VehicleStatus.Published && !x.IsDeleted).Select(x => x.Id).ToListAsync(cancellationToken);
        var current = await db.SavedComparisonVehicles.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        db.SavedComparisonVehicles.RemoveRange(current);
        db.SavedComparisonVehicles.AddRange(published.Select(x => new SavedComparisonVehicle { UserId = userId, VehicleId = x }));
        await db.SaveChangesAsync(cancellationToken);
        return published;
    }

    public async Task<IReadOnlyCollection<Guid>> GetComparisonIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.SavedComparisonVehicles.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).Select(x => x.VehicleId).ToListAsync(cancellationToken);

    private async Task<string> IssueTokenAsync(Guid userId, OneTimeTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var pending = await db.OneTimeTokens.Where(x => x.UserId == userId && x.Purpose == purpose && x.UsedAt == null).ToListAsync(cancellationToken);
        foreach (var previous in pending) previous.UsedAt = DateTimeOffset.UtcNow;
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.OneTimeTokens.Add(new OneTimeToken { UserId = userId, Purpose = purpose, TokenHash = Hash(raw), ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime) });
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    private async Task<bool> ConsumeTokenAsync(string raw, OneTimeTokenPurpose purpose, Func<User, Task> action, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var token = await db.OneTimeTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == Hash(raw) && x.Purpose == purpose, cancellationToken);
        if (token is null) return false;
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var used = await db.OneTimeTokens.Where(x => x.Id == token.Id && x.UsedAt == null && x.ExpiresAt > now)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsedAt, now), cancellationToken);
            if (used != 1) return false;
            var relationalUser = await db.Users.SingleAsync(x => x.Id == token.UserId && x.AccountType == AccountType.Customer, cancellationToken);
            await action(relationalUser);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        var local = await db.OneTimeTokens.SingleOrDefaultAsync(x => x.Id == token.Id && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);
        if (local is null) return false;
        local.UsedAt = now;
        var user = await db.Users.SingleAsync(x => x.Id == token.UserId && x.AccountType == AccountType.Customer, cancellationToken);
        await action(user);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CustomerProfile ToProfile(User user) => new(user.Email, user.DisplayName ?? string.Empty, user.Phone, user.Language, user.MarketingConsent, user.EmailVerified);
    public Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken cancellationToken) => IsVerifiedCustomerAsync(userId, cancellationToken);
    private Task<bool> IsVerifiedCustomerAsync(Guid userId, CancellationToken cancellationToken) => db.Users.AnyAsync(x => x.Id == userId && x.AccountType == AccountType.Customer && x.EmailVerified && x.IsActive, cancellationToken);
    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());
    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(value)));
    private async Task<int> RevokeRefreshTokensAsync(System.Linq.Expressions.Expression<Func<RefreshToken, bool>> predicate, CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
            return await db.RefreshTokens.Where(predicate).ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        var tokens = await db.RefreshTokens.Where(predicate).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = DateTimeOffset.UtcNow;
        if (tokens.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }
    private Task QueueEmailAsync(string recipient, string subject, string body, CancellationToken cancellationToken) =>
        backgroundTasks is null
            ? emailSender.SendAsync(recipient, subject, body, cancellationToken)
            : backgroundTasks.QueueAsync(workerCancellationToken => emailSender.SendAsync(recipient, subject, body, workerCancellationToken), cancellationToken).AsTask();
    private static void ValidatePassword(string password)
    {
        if (password.Length < 12 || CommonPasswords.Contains(password)) throw new DomainRuleException("La contraseña no cumple los requisitos de seguridad.");
    }
}
