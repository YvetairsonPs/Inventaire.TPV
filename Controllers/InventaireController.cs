using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Prise d'inventaire : comptage physique et ajustement des quantités en main.</summary>
public class InventaireController : Controller
{
    private readonly AppDbContext _db;
    public InventaireController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? recherche, string? departement)
    {
        var q = _db.Articles.AsNoTracking().Where(a => a.Actif);

        if (!string.IsNullOrWhiteSpace(recherche))
            q = q.Where(a => a.Description!.Contains(recherche) || a.ArticleId.Contains(recherche) || a.CUP1!.Contains(recherche));
        if (!string.IsNullOrWhiteSpace(departement))
            q = q.Where(a => a.Departement == departement);

        var articles = await q.OrderBy(a => a.Departement).ThenBy(a => a.Description).Take(200).ToListAsync();

        ViewBag.Departements = await _db.Articles
            .Where(a => a.Departement != null && a.Departement != "")
            .Select(a => a.Departement!).Distinct().OrderBy(d => d).ToListAsync();
        ViewBag.Recherche = recherche;
        ViewBag.Departement = departement;
        ViewBag.EnCours = await _db.Articles.CountAsync(a => a.PriseInv);
        return View(articles);
    }

    public class ComptageDto
    {
        public string ArticleId { get; set; } = "";
        public decimal QteComptee { get; set; }
    }

    /// <summary>Enregistre le comptage d'un article (ajuste QteMain et marque PriseInv).</summary>
    [HttpPost]
    public async Task<IActionResult> Compter([FromBody] ComptageDto dto)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.ArticleId == dto.ArticleId);
        if (article == null) return NotFound(new { message = "Article introuvable." });

        var ancienne = article.QteMain ?? 0;
        article.QtePriseInv = dto.QteComptee;
        article.QteMain = dto.QteComptee;
        article.PriseInv = true;
        article.DateModifie = DateTime.Now;
        await _db.SaveChangesAsync();

        var ecart = dto.QteComptee - ancienne;
        return Json(new { success = true, ancienne, nouvelle = dto.QteComptee, ecart });
    }

    /// <summary>Réinitialise les marqueurs de prise d'inventaire (nouveau cycle).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reinitialiser()
    {
        await _db.Articles.Where(a => a.PriseInv)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PriseInv, false));
        TempData["Succes"] = "Cycle d'inventaire réinitialisé.";
        return RedirectToAction(nameof(Index));
    }
}
