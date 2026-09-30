using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Dealership.Infrastructure.Migrations;

[DbContext(typeof(DealershipDbContext))]
[Migration("202609290002_PublicCustomFieldFlags")]
public partial class PublicCustomFieldFlags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsFilterable", table: "CustomFieldDefinitions", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "IsPublic", table: "CustomFieldDefinitions", type: "boolean", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsFilterable", table: "CustomFieldDefinitions");
        migrationBuilder.DropColumn(name: "IsPublic", table: "CustomFieldDefinitions");
    }
}
