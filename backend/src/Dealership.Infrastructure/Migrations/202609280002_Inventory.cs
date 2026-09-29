using Dealership.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Dealership.Infrastructure.Migrations;
[DbContext(typeof(DealershipDbContext))]
[Migration("202609280002_Inventory")]
public partial class Inventory : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE "Branches" ("Id" uuid PRIMARY KEY, "Name" varchar(160) NOT NULL, "Address" text NOT NULL, "Phones" text NOT NULL, "Hours" text NOT NULL, "ManagerName" text NULL, "IsActive" boolean NOT NULL);
CREATE UNIQUE INDEX "IX_Branches_Name" ON "Branches" ("Name");
CREATE TABLE "Makes" ("Id" uuid PRIMARY KEY, "Name" varchar(100) NOT NULL, "IsDeleted" boolean NOT NULL);
CREATE UNIQUE INDEX "IX_Makes_Name_IsDeleted" ON "Makes" ("Name", "IsDeleted");
CREATE TABLE "Models" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "IsDeleted" boolean NOT NULL, "MakeId" uuid NOT NULL REFERENCES "Makes"("Id"));
CREATE UNIQUE INDEX "IX_Models_MakeId_Name_IsDeleted" ON "Models" ("MakeId", "Name", "IsDeleted");
CREATE TABLE "Variants" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "IsDeleted" boolean NOT NULL, "ModelId" uuid NOT NULL REFERENCES "Models"("Id"));
CREATE UNIQUE INDEX "IX_Variants_ModelId_Name_IsDeleted" ON "Variants" ("ModelId", "Name", "IsDeleted");
CREATE TABLE "TechnicalCatalogs" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "IsDeleted" boolean NOT NULL, "Category" text NOT NULL);
CREATE UNIQUE INDEX "IX_TechnicalCatalogs_Category_Name_IsDeleted" ON "TechnicalCatalogs" ("Category", "Name", "IsDeleted");
CREATE TABLE "Equipment" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "IsDeleted" boolean NOT NULL);
CREATE UNIQUE INDEX "IX_Equipment_Name_IsDeleted" ON "Equipment" ("Name", "IsDeleted");
CREATE TABLE "CustomFieldDefinitions" ("Id" uuid PRIMARY KEY, "Key" text NOT NULL, "Label" text NOT NULL, "Type" integer NOT NULL, "IsRequired" boolean NOT NULL, "VehicleType" text NULL, "Options" jsonb NOT NULL, "IsActive" boolean NOT NULL);
CREATE UNIQUE INDEX "IX_CustomFieldDefinitions_Key" ON "CustomFieldDefinitions" ("Key");
CREATE TABLE "Vehicles" ("Id" uuid PRIMARY KEY, "MakeId" uuid NOT NULL REFERENCES "Makes"("Id"), "ModelId" uuid NOT NULL REFERENCES "Models"("Id"), "VariantId" uuid NULL REFERENCES "Variants"("Id"), "BranchId" uuid NOT NULL REFERENCES "Branches"("Id"), "Year" integer NOT NULL, "Mileage" integer NOT NULL, "Price" numeric(18,2) NOT NULL, "Currency" text NOT NULL, "Condition" integer NOT NULL, "Vin" text NULL, "Plate" text NULL, "Color" text NULL, "Transmission" text NULL, "Fuel" text NULL, "Drivetrain" text NULL, "BodyStyle" text NULL, "CustomFields" jsonb NOT NULL, "Status" integer NOT NULL, "CreatedAt" timestamptz NOT NULL, "CreatedById" uuid NOT NULL, "IsDeleted" boolean NOT NULL);
CREATE UNIQUE INDEX "IX_Vehicles_Vin" ON "Vehicles" ("Vin") WHERE "Vin" IS NOT NULL AND "IsDeleted" = false;
CREATE UNIQUE INDEX "IX_Vehicles_Plate" ON "Vehicles" ("Plate") WHERE "Plate" IS NOT NULL AND "IsDeleted" = false;
CREATE INDEX "IX_Vehicles_CustomFields" ON "Vehicles" USING gin ("CustomFields"); CREATE INDEX "IX_Vehicles_Status_BranchId" ON "Vehicles" ("Status", "BranchId"); CREATE INDEX "IX_Vehicles_MakeId_ModelId_Year" ON "Vehicles" ("MakeId", "ModelId", "Year"); CREATE INDEX "IX_Vehicles_Price" ON "Vehicles" ("Price");
CREATE TABLE "VehicleEquipment" ("VehicleId" uuid NOT NULL REFERENCES "Vehicles"("Id"), "EquipmentId" uuid NOT NULL REFERENCES "Equipment"("Id"), PRIMARY KEY ("VehicleId", "EquipmentId"));
CREATE TABLE "VehicleStatusHistories" ("Id" uuid PRIMARY KEY, "VehicleId" uuid NOT NULL REFERENCES "Vehicles"("Id"), "FromStatus" integer NOT NULL, "ToStatus" integer NOT NULL, "ChangedById" uuid NOT NULL, "ChangedAt" timestamptz NOT NULL, "Reason" text NOT NULL, "ManualOverride" boolean NOT NULL);
CREATE INDEX "IX_VehicleStatusHistories_VehicleId_ChangedAt" ON "VehicleStatusHistories" ("VehicleId", "ChangedAt");
CREATE TABLE "VehiclePriceHistories" ("Id" uuid PRIMARY KEY, "VehicleId" uuid NOT NULL REFERENCES "Vehicles"("Id"), "PreviousPrice" numeric(18,2) NOT NULL, "NewPrice" numeric(18,2) NOT NULL, "Currency" text NOT NULL, "ChangedById" uuid NOT NULL, "ChangedAt" timestamptz NOT NULL, "Reason" text NOT NULL);
CREATE INDEX "IX_VehiclePriceHistories_VehicleId_ChangedAt" ON "VehiclePriceHistories" ("VehicleId", "ChangedAt");
""");
    }
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE IF EXISTS \"VehiclePriceHistories\", \"VehicleStatusHistories\", \"VehicleEquipment\", \"Vehicles\", \"CustomFieldDefinitions\", \"Equipment\", \"TechnicalCatalogs\", \"Variants\", \"Models\", \"Makes\", \"Branches\" CASCADE;");
}
