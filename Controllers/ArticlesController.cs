using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Gestion de stock : liste, recherche, création, modification des articles.</summary>
public class ArticlesController : Controller
{
    private readonly AppDbContext _db;
    public ArticlesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? recherche, string? departement, string? filtre, int page = 1)
    {
        const int taille = 25;
        var q = _db.Articles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            q = q.Where(a => a.Description!.Contains(recherche)
                          || a.ArticleId.Contains(recherche)
                          || a.CUP1!.Contains(recherche)
                          || a.SKU!.Contains(recherche));
        }
        if (!string.IsNullOrWhiteSpace(departement))
            q = q.Where(a => a.Departement == departement);

        if (filtre == "actifs") q = q.Where(a => a.Actif);
        else if (filtre == "inactifs") q = q.Where(a => !a.Actif);
        else if (filtre == "faible") q = q.Where(a => a.QteMin > 0 && a.QteMain <= a.QteMin);

        var total = await q.CountAsync();
        var articles = await q
            .OrderBy(a => a.Description)
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        ViewBag.Departements = await DepartementsSelectList(departement);
        ViewBag.Recherche = recherche;
        ViewBag.Departement = departement;
        ViewBag.Filtre = filtre;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)taille);
        ViewBag.Total = total;

        return View(articles);
    }

    public async Task<IActionResult> Details(string id)
    {
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.ArticleId == id);
        if (article == null) return NotFound();
        return View(article);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Departements = await DepartementsSelectList(null);
        return View(new Article { Actif = true, DateCree = DateTime.Now });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Article article)
    {
        if (await _db.Articles.AnyAsync(a => a.ArticleId == article.ArticleId))
            ModelState.AddModelError(nameof(Article.ArticleId), "Ce code article existe déjà.");

        if (ModelState.IsValid)
        {
            article.DateCree ??= DateTime.Now;
            article.DateModifie = DateTime.Now;
            if (string.IsNullOrEmpty(article.NoArticle)) article.NoArticle = article.ArticleId;
            _db.Add(article);
            await _db.SaveChangesAsync();
            TempData["Succes"] = $"Article « {article.Description} » créé.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Departements = await DepartementsSelectList(article.Departement);
        return View(article);
    }

    public async Task<IActionResult> Edit(string id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();
        ViewBag.Departements = await DepartementsSelectList(article.Departement);
        return View(article);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Article article)
    {
        if (id != article.ArticleId) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                article.DateModifie = DateTime.Now;
                _db.Update(article);
                await _db.SaveChangesAsync();
                TempData["Succes"] = $"Article « {article.Description} » modifié.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.Articles.AnyAsync(a => a.ArticleId == id)) return NotFound();
                throw;
            }
        }
        ViewBag.Departements = await DepartementsSelectList(article.Departement);
        return View(article);
    }

    public async Task<IActionResult> Delete(string id)
    {
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.ArticleId == id);
        if (article == null) return NotFound();
        return View(article);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article != null)
        {
            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            TempData["Succes"] = "Article supprimé.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<SelectList> DepartementsSelectList(string? selected)
    {
        var deps = await _db.Articles
            .Where(a => a.Departement != null && a.Departement != "")
            .Select(a => a.Departement!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
        return new SelectList(deps, selected);
    }
}
