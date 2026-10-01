using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Dealership.Infrastructure.Migrations;

[DbContext(typeof(DealershipDbContext))]
[Migration("202609300002_AddCustomerAccounts")]
public partial class AddCustomerAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("AccountType", "Users", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>("EmailVerified", "Users", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("DisplayName", "Users", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>("Phone", "Users", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>("Language", "Users", maxLength: 16, nullable: false, defaultValue: "es-MX");
        migrationBuilder.AddColumn<bool>("MarketingConsent", "Users", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("PrivacyPolicyVersion", "Users", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("PrivacyAcceptedAt", "Users", nullable: true);
        migrationBuilder.AddColumn<int>("FailedLoginCount", "Users", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTimeOffset>("LockoutEnd", "Users", nullable: true);
        migrationBuilder.CreateTable("OneTimeTokens", table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), Purpose = table.Column<int>(nullable: false), TokenHash = table.Column<string>(nullable: false), ExpiresAt = table.Column<DateTimeOffset>(nullable: false), UsedAt = table.Column<DateTimeOffset>(nullable: true), CreatedAt = table.Column<DateTimeOffset>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_OneTimeTokens", x => x.Id); table.ForeignKey("FK_OneTimeTokens_Users_UserId", x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable("FavoriteVehicles", table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), VehicleId = table.Column<Guid>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_FavoriteVehicles", x => x.Id); table.ForeignKey("FK_FavoriteVehicles_Users_UserId", x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_FavoriteVehicles_Vehicles_VehicleId", x => x.VehicleId, principalTable: "Vehicles", principalColumn: "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable("SavedComparisonVehicles", table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), VehicleId = table.Column<Guid>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_SavedComparisonVehicles", x => x.Id); table.ForeignKey("FK_SavedComparisonVehicles_Users_UserId", x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_SavedComparisonVehicles_Vehicles_VehicleId", x => x.VehicleId, principalTable: "Vehicles", principalColumn: "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable("SecurityEvents", table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: true), EventType = table.Column<string>(maxLength: 80, nullable: false), IpHash = table.Column<string>(maxLength: 128, nullable: true), OccurredAt = table.Column<DateTimeOffset>(nullable: false) }, constraints: table => table.PrimaryKey("PK_SecurityEvents", x => x.Id));
        migrationBuilder.CreateIndex("IX_OneTimeTokens_TokenHash", "OneTimeTokens", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_OneTimeTokens_UserId_Purpose_UsedAt", "OneTimeTokens", new[] { "UserId", "Purpose", "UsedAt" });
        migrationBuilder.CreateIndex("IX_FavoriteVehicles_UserId_VehicleId", "FavoriteVehicles", new[] { "UserId", "VehicleId" }, unique: true);
        migrationBuilder.CreateIndex("IX_SavedComparisonVehicles_UserId_VehicleId", "SavedComparisonVehicles", new[] { "UserId", "VehicleId" }, unique: true);
        migrationBuilder.CreateIndex("IX_SecurityEvents_UserId_OccurredAt", "SecurityEvents", new[] { "UserId", "OccurredAt" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SecurityEvents"); migrationBuilder.DropTable("SavedComparisonVehicles"); migrationBuilder.DropTable("FavoriteVehicles"); migrationBuilder.DropTable("OneTimeTokens");
        foreach (var name in new[] { "AccountType", "EmailVerified", "DisplayName", "Phone", "Language", "MarketingConsent", "PrivacyPolicyVersion", "PrivacyAcceptedAt", "FailedLoginCount", "LockoutEnd" }) migrationBuilder.DropColumn(name, "Users");
    }
}
