using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Migrations;

[DbContext(typeof(DealershipDbContext))]
[Migration("202609290001_PublicCatalog")]
public partial class PublicCatalog : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        var publishedStatus = (int)VehicleStatus.Published;
        m.Sql($"""
CREATE INDEX IF NOT EXISTS "IX_Vehicles_Published_Catalog" 
ON "Vehicles" ("ModelId", "MakeId", "Year", "Price", "Mileage") 
WHERE "Status" = {publishedStatus} AND "IsDeleted" = false;
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP INDEX IF EXISTS \"IX_Vehicles_Published_Catalog\";");
    }
}
