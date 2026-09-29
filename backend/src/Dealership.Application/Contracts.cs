using Dealership.Domain;

namespace Dealership.Application;

public sealed record LoginCommand(string Email, string Password);
public sealed record TokenResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record CurrentUser(Guid Id, string Email, IReadOnlyCollection<string> Roles);

public interface IAuthenticationService
{
    Task<TokenResult?> LoginAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface ICurrentUser { string? Id { get; } }
public interface ICorrelationContext { string Id { get; } }
public interface IBackgroundTaskQueue { ValueTask QueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken); }

