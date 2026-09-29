using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Dealership.Application;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.AddAuthorization(options => options.AddPolicy("Administration", policy => policy.RequireRole("Administrator", "Manager")));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; }));
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks().AddDbContextCheck<DealershipDbContext>();
var app = builder.Build();
app.Use(async (context, next) => { var correlation = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N"); context.Items["CorrelationId"] = correlation; context.Response.Headers["X-Correlation-ID"] = correlation; await next(); });
app.UseExceptionHandler(errorApp => errorApp.Run(async context => { var correlation = context.Items["CorrelationId"]?.ToString(); var problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Ocurrió un error inesperado.", Detail = "Intenta nuevamente o contacta a soporte con el identificador de seguimiento.", Extensions = { ["correlationId"] = correlation } }; await Results.Problem(problem.Detail, statusCode: problem.Status, title: problem.Title, extensions: problem.Extensions).ExecuteAsync(context); }));
app.UseHsts();
app.Use(async (context, next) => { context.Response.Headers.Append("X-Content-Type-Options", "nosniff"); context.Response.Headers.Append("X-Frame-Options", "DENY"); context.Response.Headers.Append("Referrer-Policy", "no-referrer"); await next(); });
app.UseHttpsRedirection(); app.UseCors("frontend"); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.UseSwagger(); app.UseSwaggerUI(); app.MapHealthChecks("/health"); app.MapControllers();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DealershipDbContext>(); await db.Database.MigrateAsync(); await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(CancellationToken.None); }
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
