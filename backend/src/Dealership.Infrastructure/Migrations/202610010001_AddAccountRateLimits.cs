using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Dealership.Infrastructure.Migrations;

[DbContext(typeof(DealershipDbContext))]
[Migration("202610010001_AddAccountRateLimits")]
public partial class AddAccountRateLimits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("AccountRateLimits", table => new { Id = table.Column<Guid>(nullable: false), Purpose = table.Column<string>(maxLength: 40, nullable: false), AccountHash = table.Column<string>(maxLength: 128, nullable: false), AttemptCount = table.Column<int>(nullable: false), ExpiresAt = table.Column<DateTimeOffset>(nullable: false) }, constraints: table => table.PrimaryKey("PK_AccountRateLimits", x => x.Id));
        migrationBuilder.CreateIndex("IX_AccountRateLimits_Purpose_AccountHash", "AccountRateLimits", new[] { "Purpose", "AccountHash" }, unique: true);
        migrationBuilder.CreateIndex("IX_AccountRateLimits_ExpiresAt", "AccountRateLimits", "ExpiresAt");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AccountRateLimits");
}
