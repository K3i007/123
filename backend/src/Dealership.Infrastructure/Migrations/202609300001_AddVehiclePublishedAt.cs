using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Dealership.Infrastructure.Migrations;

[DbContext(typeof(DealershipDbContext))]
[Migration("202609300001_AddVehiclePublishedAt")]
public partial class AddVehiclePublishedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PublishedAt",
            table: "Vehicles",
            type: "timestamp with time zone",
            nullable: true);

        // Existing published inventory predates this field; its creation time is the best available date.
        migrationBuilder.Sql("UPDATE \"Vehicles\" SET \"PublishedAt\" = \"CreatedAt\" WHERE \"Status\" = 5 AND \"PublishedAt\" IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "PublishedAt", table: "Vehicles");
}
