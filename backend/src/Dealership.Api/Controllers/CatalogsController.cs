using Asp.Versioning;
using Dealership.Domain;
using Dealership.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Api.Controllers;

[ApiController, ApiVersion(1), Route("api/v{version:apiVersion}/inventory"), Authorize(Roles = "InventoryManager,Manager,Administrator")]
public sealed class CatalogsController(DealershipDbContext db) : ControllerBase
{
    [HttpGet("branches")] public async Task<ActionResult> Branches(CancellationToken ct) => Ok(await db.Branches.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("branches")] public async Task<ActionResult<Branch>> CreateBranch(Branch branch, CancellationToken ct) { db.Branches.Add(branch); await db.SaveChangesAsync(ct); return Created($"branches/{branch.Id}", branch); }
    [HttpGet("makes")] public async Task<ActionResult> Makes(CancellationToken ct) => Ok(await db.Makes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("makes")] public async Task<ActionResult<Make>> CreateMake(Make make, CancellationToken ct) { db.Makes.Add(make); await db.SaveChangesAsync(ct); return Created($"makes/{make.Id}", make); }
    [HttpPut("makes/{id:guid}")] public async Task<IActionResult> UpdateMake(Guid id, Make input, CancellationToken ct) { var item = await db.Makes.FindAsync([id], ct); if (item is null) return NotFound(); item.Name = input.Name; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("makes/{id:guid}")] public async Task<IActionResult> DeleteMake(Guid id, CancellationToken ct) { var item = await db.Makes.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("models")] public async Task<ActionResult> Models([FromQuery] Guid? makeId, CancellationToken ct) => Ok(await db.Models.AsNoTracking().Where(x => !x.IsDeleted && (makeId == null || x.MakeId == makeId)).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("models")] public async Task<ActionResult<Model>> CreateModel(Model model, CancellationToken ct) { db.Models.Add(model); await db.SaveChangesAsync(ct); return Created($"models/{model.Id}", model); }
    [HttpPut("models/{id:guid}")] public async Task<IActionResult> UpdateModel(Guid id, Model input, CancellationToken ct) { var item = await db.Models.FindAsync([id], ct); if (item is null) return NotFound(); item.Name = input.Name; item.MakeId = input.MakeId; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("models/{id:guid}")] public async Task<IActionResult> DeleteModel(Guid id, CancellationToken ct) { var item = await db.Models.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("variants")] public async Task<ActionResult> Variants([FromQuery] Guid? modelId, CancellationToken ct) => Ok(await db.Variants.AsNoTracking().Where(x => !x.IsDeleted && (modelId == null || x.ModelId == modelId)).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("variants")] public async Task<ActionResult<Variant>> CreateVariant(Variant variant, CancellationToken ct) { db.Variants.Add(variant); await db.SaveChangesAsync(ct); return Created($"variants/{variant.Id}", variant); }
    [HttpPut("variants/{id:guid}")] public async Task<IActionResult> UpdateVariant(Guid id, Variant input, CancellationToken ct) { var item = await db.Variants.FindAsync([id], ct); if (item is null) return NotFound(); item.Name = input.Name; item.ModelId = input.ModelId; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("variants/{id:guid}")] public async Task<IActionResult> DeleteVariant(Guid id, CancellationToken ct) { var item = await db.Variants.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("equipment")] public async Task<ActionResult> Equipment(CancellationToken ct) => Ok(await db.Equipment.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(ct));
    [HttpPost("equipment")] public async Task<ActionResult<Equipment>> CreateEquipment(Equipment equipment, CancellationToken ct) { db.Equipment.Add(equipment); await db.SaveChangesAsync(ct); return Created($"equipment/{equipment.Id}", equipment); }
    [HttpPut("equipment/{id:guid}")] public async Task<IActionResult> UpdateEquipment(Guid id, Equipment input, CancellationToken ct) { var item = await db.Equipment.FindAsync([id], ct); if (item is null) return NotFound(); item.Name = input.Name; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("equipment/{id:guid}")] public async Task<IActionResult> DeleteEquipment(Guid id, CancellationToken ct) { var item = await db.Equipment.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("technical-catalogs")] public async Task<ActionResult> TechnicalCatalogs([FromQuery] string? category, CancellationToken ct) => Ok(await db.TechnicalCatalogs.AsNoTracking().Where(x => !x.IsDeleted && (category == null || x.Category == category)).OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync(ct));
    [HttpPost("technical-catalogs")] public async Task<ActionResult<TechnicalCatalog>> CreateTechnicalCatalog(TechnicalCatalog item, CancellationToken ct) { db.TechnicalCatalogs.Add(item); await db.SaveChangesAsync(ct); return Created($"technical-catalogs/{item.Id}", item); }
    [HttpPut("technical-catalogs/{id:guid}")] public async Task<IActionResult> UpdateTechnicalCatalog(Guid id, TechnicalCatalog input, CancellationToken ct) { var item = await db.TechnicalCatalogs.FindAsync([id], ct); if (item is null) return NotFound(); item.Name = input.Name; item.Category = input.Category; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("technical-catalogs/{id:guid}")] public async Task<IActionResult> DeleteTechnicalCatalog(Guid id, CancellationToken ct) { var item = await db.TechnicalCatalogs.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpGet("fields")] public async Task<ActionResult> Fields(CancellationToken ct) => Ok(await db.CustomFieldDefinitions.AsNoTracking().OrderBy(x => x.Label).ToListAsync(ct));
    [HttpPost("fields")] public async Task<ActionResult<CustomFieldDefinition>> CreateField(CustomFieldDefinition definition, CancellationToken ct) { db.CustomFieldDefinitions.Add(definition); await db.SaveChangesAsync(ct); return Created($"fields/{definition.Id}", definition); }
    [HttpDelete("fields/{id:guid}")] public async Task<IActionResult> DisableField(Guid id, CancellationToken ct) { var definition = await db.CustomFieldDefinitions.SingleOrDefaultAsync(x => x.Id == id, ct); if (definition is null) return NotFound(); definition.IsActive = false; await db.SaveChangesAsync(ct); return NoContent(); }
}
