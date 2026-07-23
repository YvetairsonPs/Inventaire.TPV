using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Gestion des départements du point de vente : taxes (TPS/TVQ), déductions et visibilité des boutons.</summary>
[Authorize(Roles = SeedIdentite.RoleAdmin)]
public class DepartementsController : Controller
{
    private readonly AppDbContext _db;
    public DepartementsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var liste = await _db.Departements.AsNoTracking()
            .OrderBy(d => d.Negatif).ThenBy(d => d.DepartementCode)
            .ToListAsync();
        return View(liste);
    }

    public class DepDto
    {
        public int ID { get; set; }
        public string? Description { get; set; }
        public bool Taxe1 { get; set; }
        public bool Taxe2 { get; set; }
        public bool Negatif { get; set; }
        public bool Visible { get; set; }
    }

    // Enregistrement groupé des modifications (taxes / déduction / visibilité / description).
    [HttpPost]
    public async Task<IActionResult> Enregistrer([FromBody] List<DepDto> deps)
    {
        if (deps == null || deps.Count == 0)
            return Json(new { success = false, message = "Aucune donnée reçue." });

        var ids = deps.Select(d => d.ID).ToList();
        var rows = await _db.Departements.Where(d => ids.Contains(d.ID)).ToListAsync();
        foreach (var r in rows)
        {
            var d = deps.First(x => x.ID == r.ID);
            r.Description = string.IsNullOrWhiteSpace(d.Description) ? r.DepartementCode : d.Description!.Trim();
            r.Taxe1 = d.Taxe1;
            r.Taxe2 = d.Taxe2;
            r.Negatif = d.Negatif;
            r.Visible = d.Visible;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true, count = rows.Count });
    }

    public class NouveauDepDto
    {
        public string Code { get; set; } = "";
        public string? Description { get; set; }
        public bool Taxe1 { get; set; }
        public bool Taxe2 { get; set; }
        public bool Negatif { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Ajouter([FromBody] NouveauDepDto dto)
    {
        var code = (dto.Code ?? "").Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Le code du département est requis." });
        if (code.Length > 16)
            return Json(new { success = false, message = "Le code ne peut dépasser 16 caractères." });
        if (await _db.Departements.AnyAsync(d => d.DepartementCode == code))
            return Json(new { success = false, message = "Ce département existe déjà." });

        // La colonne ID n'est pas IDENTITY : on calcule le prochain identifiant.
        var maxId = await _db.Departements.MaxAsync(d => (int?)d.ID) ?? 0;
        _db.Departements.Add(new Departement
        {
            ID = maxId + 1,
            DepartementCode = code,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? code : dto.Description!.Trim(),
            Taxe1 = dto.Taxe1,
            Taxe2 = dto.Taxe2,
            Negatif = dto.Negatif,
            Visible = true
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    public class IdDto { public int Id { get; set; } }

    [HttpPost]
    public async Task<IActionResult> Supprimer([FromBody] IdDto dto)
    {
        var d = await _db.Departements.FindAsync(dto.Id);
        if (d != null)
        {
            _db.Departements.Remove(d);
            await _db.SaveChangesAsync();
        }
        return Json(new { success = true });
    }
}
