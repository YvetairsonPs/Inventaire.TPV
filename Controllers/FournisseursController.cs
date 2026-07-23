using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Gestion des fournisseurs : liste, recherche et CRUD.</summary>
public class FournisseursController : Controller
{
    private readonly AppDbContext _db;
    public FournisseursController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? recherche)
    {
        var q = _db.Fournisseurs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(recherche))
            q = q.Where(f => f.Nom!.Contains(recherche) || f.Code.Contains(recherche)
                          || f.Ville!.Contains(recherche) || f.Contact!.Contains(recherche));

        var liste = await q.OrderBy(f => f.Nom).ToListAsync();
        ViewBag.Recherche = recherche;
        return View(liste);
    }

    public async Task<IActionResult> Details(int id)
    {
        var f = await _db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id);
        if (f == null) return NotFound();

        // Articles rattachés à ce fournisseur (par code).
        ViewBag.NbArticles = await _db.Articles.CountAsync(a => a.Fournisseur == f.Code);
        return View(f);
    }

    public IActionResult Create() => View(new Fournisseur());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Fournisseur f)
    {
        if (await _db.Fournisseurs.AnyAsync(x => x.Code == f.Code))
            ModelState.AddModelError(nameof(Fournisseur.Code), "Ce code fournisseur existe déjà.");

        if (ModelState.IsValid)
        {
            // La colonne ID n'est pas IDENTITY : on calcule le prochain identifiant.
            var maxId = await _db.Fournisseurs.MaxAsync(x => (int?)x.ID) ?? 0;
            f.ID = maxId + 1;
            _db.Add(f);
            await _db.SaveChangesAsync();
            TempData["Succes"] = $"Fournisseur « {f.Nom} » créé.";
            return RedirectToAction(nameof(Index));
        }
        return View(f);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await _db.Fournisseurs.FindAsync(id);
        if (f == null) return NotFound();
        return View(f);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Fournisseur f)
    {
        if (id != f.ID) return NotFound();
        if (ModelState.IsValid)
        {
            _db.Update(f);
            await _db.SaveChangesAsync();
            TempData["Succes"] = $"Fournisseur « {f.Nom} » modifié.";
            return RedirectToAction(nameof(Index));
        }
        return View(f);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id);
        if (f == null) return NotFound();
        return View(f);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var f = await _db.Fournisseurs.FindAsync(id);
        if (f != null)
        {
            _db.Fournisseurs.Remove(f);
            await _db.SaveChangesAsync();
            TempData["Succes"] = "Fournisseur supprimé.";
        }
        return RedirectToAction(nameof(Index));
    }
}
