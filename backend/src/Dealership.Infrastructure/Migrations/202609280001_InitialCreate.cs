using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Dealership.Infrastructure.Migrations;
[DbContext(typeof(DealershipDbContext))]
[Migration("202609280001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Roles", columns: table => new { Id = table.Column<Guid>(nullable: false), Name = table.Column<string>(maxLength: 100, nullable: false) }, constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));
        migrationBuilder.CreateTable(name: "Users", columns: table => new { Id = table.Column<Guid>(nullable: false), Email = table.Column<string>(maxLength: 256, nullable: false), PasswordHash = table.Column<string>(nullable: false), IsActive = table.Column<bool>(nullable: false) }, constraints: table => table.PrimaryKey("PK_Users", x => x.Id));
        migrationBuilder.CreateTable(name: "AuditLogs", columns: table => new { Id = table.Column<Guid>(nullable: false), OccurredAt = table.Column<DateTimeOffset>(nullable: false), ActorId = table.Column<string>(nullable: true), Action = table.Column<string>(nullable: false), EntityName = table.Column<string>(nullable: false), EntityId = table.Column<string>(nullable: false), OldValues = table.Column<string>(nullable: true), NewValues = table.Column<string>(nullable: true), CorrelationId = table.Column<string>(nullable: false) }, constraints: table => table.PrimaryKey("PK_AuditLogs", x => x.Id));
        migrationBuilder.CreateTable(name: "RefreshTokens", columns: table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), FamilyId = table.Column<Guid>(nullable: false), TokenHash = table.Column<string>(nullable: false), ExpiresAt = table.Column<DateTimeOffset>(nullable: false), UsedAt = table.Column<DateTimeOffset>(nullable: true), RevokedAt = table.Column<DateTimeOffset>(nullable: true), ReplacedById = table.Column<Guid>(nullable: true) }, constraints: table => { table.PrimaryKey("PK_RefreshTokens", x => x.Id); table.ForeignKey("FK_RefreshTokens_Users_UserId", x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable(name: "UserRole", columns: table => new { UserId = table.Column<Guid>(nullable: false), RoleId = table.Column<Guid>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId }); table.ForeignKey("FK_UserRole_Users_UserId", x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_UserRole_Roles_RoleId", x => x.RoleId, principalTable: "Roles", principalColumn: "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex(name: "IX_AuditLogs_OccurredAt", table: "AuditLogs", column: "OccurredAt");
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_FamilyId_RevokedAt", table: "RefreshTokens", columns: new[] { "FamilyId", "RevokedAt" });
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_TokenHash", table: "RefreshTokens", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_UserId", table: "RefreshTokens", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_Roles_Name", table: "Roles", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_UserRole_RoleId", table: "UserRole", column: "RoleId");
        migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", column: "Email", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable(name: "AuditLogs"); migrationBuilder.DropTable(name: "RefreshTokens"); migrationBuilder.DropTable(name: "UserRole"); migrationBuilder.DropTable(name: "Users"); migrationBuilder.DropTable(name: "Roles"); }
}
