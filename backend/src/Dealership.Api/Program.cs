using System.Security.Claims;
using System.Net;
using System.Threading.RateLimiting;
using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Dealership.Application;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

EnvironmentFile.LoadFromAncestors(Directory.GetCurrentDirectory());
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Host.UseSerilog((context, services, logger) => logger.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<DealershipDbContext>((services, options) => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")).AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IPasswordWorkService, PasswordWorkService>();
builder.Services.AddScoped<IAccountRateLimitService, AccountRateLimitService>();
builder.Services.AddScoped<ICustomerAccountService, CustomerAccountService>();
builder.Services.AddSingleton<IEmailSender, DevelopmentEmailSender>();
builder.Services.AddScoped<DevelopmentDataSeeder>();
builder.Services.AddSingleton<BackgroundTaskQueue>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(services => services.GetRequiredService<BackgroundTaskQueue>());
builder.Services.AddHostedService<QueuedWorker>();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<Dealership.Domain.VehicleStatus>(() => new OpenApiSchema { Type = "string", Enum = Enum.GetNames<Dealership.Domain.VehicleStatus>().Select(x => new Microsoft.OpenApi.Any.OpenApiString(x)).Cast<Microsoft.OpenApi.Any.IOpenApiAny>().ToList() });
    options.MapType<Dealership.Domain.VehicleCondition>(() => new OpenApiSchema { Type = "string", Enum = Enum.GetNames<Dealership.Domain.VehicleCondition>().Select(x => new Microsoft.OpenApi.Any.OpenApiString(x)).Cast<Microsoft.OpenApi.Any.IOpenApiAny>().ToList() });
});
builder.Services.AddApiVersioning(options => { options.DefaultApiVersion = new ApiVersion(1); options.AssumeDefaultVersionWhenUnspecified = true; options.ReportApiVersions = true; }).AddApiExplorer(options => options.GroupNameFormat = "'v'V");
var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"], ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ValidateLifetime = true, RoleClaimType = ClaimTypes.Role });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Staff", policy => policy.RequireClaim("account_type", "Staff"));
    options.AddPolicy("Customer", policy => policy.RequireClaim("account_type", "Customer"));
    options.AddPolicy("Administration", policy => policy.RequireClaim("account_type", "Staff").RequireRole("Administrator", "Manager"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Demasiadas solicitudes.",
            detail: "Espera un minuto antes de intentar nuevamente.").ExecuteAsync(context.HttpContext);
    };
    options.AddFixedWindowLimiter("login", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; });
    var customerLoginLimit = builder.Configuration.GetValue("RateLimiting:CustomerLoginPermitLimit", 20);
    var customerAccountLimit = builder.Configuration.GetValue("RateLimiting:CustomerAccountPermitLimit", 3);
    var customerIpLimit = builder.Configuration.GetValue("RateLimiting:CustomerIpPermitLimit", 20);
    options.AddPolicy("customer-login", context => RateLimitPartition.GetFixedWindowLimiter($"customer-login:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions { PermitLimit = customerLoginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("customer-register", context => RateLimitPartition.GetFixedWindowLimiter($"customer-register:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions { PermitLimit = customerIpLimit, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("customer-reset", context => RateLimitPartition.GetFixedWindowLimiter($"customer-reset:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions { PermitLimit = customerIpLimit, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("customer-resend", context => RateLimitPartition.GetFixedWindowLimiter($"customer-resend:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions { PermitLimit = customerIpLimit, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true }));
    var publicLimit = builder.Configuration.GetValue<int>("RateLimiting:PublicCatalogPermitLimit", builder.Environment.IsDevelopment() ? 1_000 : 100);
    options.AddPolicy("public-catalog", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = publicLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var trustedProxyAddresses = (builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
    .Where(address => address is not null)
    .Cast<IPAddress>()
    .ToHashSet();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Only infrastructure declared by configuration can alter a client identity or scheme.
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    foreach (var address in trustedProxyAddresses) options.KnownProxies.Add(address);
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.KnownProxies.Clear();
    }
});
var allowedOrigins = builder.Configuration.GetSection("Frontend:Origins").Get<string[]>() ?? [];
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Security requires Frontend:Origins outside Development.");
}
if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0) allowedOrigins = ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks().AddDbContextCheck<DealershipDbContext>();
var app = builder.Build();

app.Use(async (context, next) => { var correlation = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N"); context.Items["CorrelationId"] = correlation; context.Response.Headers["X-Correlation-ID"] = correlation; await next(); });
if (!app.Environment.IsEnvironment("Testing")) { app.UseExceptionHandler(errorApp => errorApp.Run(async context => { var correlation = context.Items["CorrelationId"]?.ToString(); var problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "OcurriÃ³ un error inesperado.", Detail = "Intenta nuevamente o contacta a soporte con el identificador de seguimiento.", Extensions = { ["correlationId"] = correlation } }; await Results.Problem(problem.Detail, statusCode: problem.Status, title: problem.Title, extensions: problem.Extensions).ExecuteAsync(context); })); }
app.UseHsts();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is null || !trustedProxyAddresses.Contains(context.Connection.RemoteIpAddress))
    {
        context.Request.Headers.Remove("X-Forwarded-For");
        context.Request.Headers.Remove("X-Forwarded-Proto");
    }
    await next();
});
app.UseForwardedHeaders();
app.Use(async (context, next) => { 
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff"); 
    context.Response.Headers.Append("X-Frame-Options", "DENY"); 
    context.Response.Headers.Append("Referrer-Policy", "no-referrer"); 
    
    // CSRF protection for mutations
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)) {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrEmpty(origin) && !allowedOrigins.Contains(origin)) {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Cross-site mutation rejected.");
            return;
        }
    }
    
    await next(); 
});
app.UseHttpsRedirection(); app.UseCors("frontend"); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.UseSwagger(); app.UseSwaggerUI(); app.MapHealthChecks("/health"); app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
    if (db.Database.IsRelational()) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(CancellationToken.None);
}
app.Run();

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser { public string? Id => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? accessor.HttpContext?.User.FindFirstValue("sub"); }
public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext { public string Id => accessor.HttpContext?.Items["CorrelationId"]?.ToString() ?? "system"; }
public static class EnvironmentFile
{
    public static void LoadFromAncestors(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                Load(path);
                return;
            }
            directory = directory.Parent;
        }
    }

    private static void Load(string path)
    {
        foreach (var line in File.ReadLines(path).Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith('#')))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key))) Environment.SetEnvironmentVariable(key, line[(separator + 1)..].Trim());
        }
    }
}
public partial class Program;
