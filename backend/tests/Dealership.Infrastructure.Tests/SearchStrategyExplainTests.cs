using System.Data;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Dealership.Infrastructure.Tests;

public sealed class SearchStrategyExplainTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Published_catalog_query_has_the_documented_partial_index_and_explain_plan()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            output.WriteLine("Skipped: ConnectionStrings__Default is not configured.");
            return;
        }

        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new DealershipDbContext(options);
        Assert.True(await db.Database.CanConnectAsync());
        var modelId = await db.Vehicles.Where(v => v.Status == VehicleStatus.Published && !v.IsDeleted).Select(v => (Guid?)v.ModelId).FirstOrDefaultAsync();
        if (modelId is null)
        {
            output.WriteLine("Skipped: there is no published catalog seed to inspect.");
            return;
        }

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "SELECT indexdef FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'IX_Vehicles_Published_Catalog';";
        var indexDefinition = (string?)await indexCommand.ExecuteScalarAsync();
        Assert.NotNull(indexDefinition);
        Assert.Contains("WHERE", indexDefinition!, StringComparison.OrdinalIgnoreCase);

        await using var explain = connection.CreateCommand();
        explain.CommandText = $"""
            SET enable_seqscan = off;
            EXPLAIN (FORMAT JSON)
            SELECT "Id", "Price", "Year", "Mileage"
            FROM "Vehicles"
            WHERE "Status" = {(int)VehicleStatus.Published} AND "IsDeleted" = false AND "ModelId" = @modelId
            ORDER BY "Year" DESC
            LIMIT 12;
            """;
        explain.Parameters.AddWithValue("modelId", modelId.Value);
        var plan = (string?)await explain.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(plan));
        Assert.Contains("Index", plan!, StringComparison.OrdinalIgnoreCase);
        output.WriteLine(plan);
    }
}
