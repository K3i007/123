using Dealership.Domain;

namespace Dealership.Application;

public sealed record LoginCommand(string Email, string Password);
public sealed record TokenResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record CurrentUser(Guid Id, string Email, IReadOnlyCollection<string> Roles);
public sealed record CustomerRegistrationCommand(string Email, string Password, string Name, string? Phone, string PrivacyPolicyVersion);
public sealed record CustomerProfile(string Email, string Name, string? Phone, string Language, bool MarketingConsent, bool EmailVerified);
public sealed record CustomerExport(Guid UserId, string Email, string Name, string? Phone, string Language, bool MarketingConsent, string PrivacyPolicyVersion, DateTimeOffset? PrivacyAcceptedAt, IReadOnlyCollection<Guid> FavoriteVehicleIds, IReadOnlyCollection<Guid> ComparisonVehicleIds);
public sealed record CustomerSession(Guid Id, DateTimeOffset ExpiresAt, DateTimeOffset? LastUsedAt, bool IsCurrent);
public sealed record PublicComparisonVehicle(Guid Id, string Make, string Model, string? Variant, int Year, int Mileage, decimal Price, string Currency, string? Transmission, string? Fuel, string? Drivetrain, string? BodyStyle, string? Power, IReadOnlyCollection<string> Equipment);

public interface IAuthenticationService
{
    Task<TokenResult?> LoginAsync(LoginCommand command, CancellationToken cancellationToken, AccountType? expectedAccountType = null);
    Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface ICustomerAccountService
{
    Task<bool> RegisterAsync(CustomerRegistrationCommand command, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken);
    Task RequestEmailVerificationAsync(string email, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken);
    Task<CustomerProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UpdateProfileAsync(Guid userId, string name, string? phone, string language, bool marketingConsent, CancellationToken cancellationToken);
    Task<CustomerExport?> ExportAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CustomerSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<int> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> MergeFavoritesAsync(Guid userId, IReadOnlyCollection<Guid> vehicleIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetFavoriteIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> SetFavoriteAsync(Guid userId, Guid vehicleId, bool favorite, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> SetComparisonAsync(Guid userId, IReadOnlyCollection<Guid> vehicleIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetComparisonIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    bool IsAvailable { get; }
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}

public interface IPasswordWorkService
{
    int IterationCount { get; }
    string Hash(User user, string password);
    bool Verify(User user, string hash, string password);
    bool VerifyUnknown(string password);
}

public interface IAccountRateLimitService
{
    /// <summary>
    /// Records an attempt. Returns false when the per-account attempt limit is reached.
    /// For the "login" purpose the return value is always true — the delay itself is the
    /// friction mechanism, not a hard block.
    /// </summary>
    Task<bool> AllowAsync(string purpose, string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the rate-limit counter for the given purpose + email.
    /// Call this after a successful login so the progressive delay resets to zero
    /// and the account is not penalised on the next honest attempt.
    /// </summary>
    Task ResetAsync(string purpose, string normalizedEmail, CancellationToken cancellationToken);
}

public interface ICurrentUser { string? Id { get; } }
public interface ICorrelationContext { string Id { get; } }
public interface IBackgroundTaskQueue { ValueTask QueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken); }

