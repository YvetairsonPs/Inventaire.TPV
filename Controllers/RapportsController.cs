using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Rapports : ventes par période, meilleurs vendeurs, valeur du stock, stock faible.</summary>
public class RapportsController : Controller
{
    private readonly AppDbContext _db;
    public RapportsController(AppDbContext db) => _db = db;

    public IActionResult Index() => View();

    // Rapport de ventes par période
    public async Task<IActionResult> Ventes(DateTime? du, DateTime? au)
    {
        du ??= DateTime.Today.AddDays(-30);
        au ??= DateTime.Today;

        var ventes = await _db.Ventes.AsNoTracking()
            .Where(v => v.DateVente >= du.Value.Date && v.DateVente < au.Value.Date.AddDays(1))
            .ToListAsync();

        var parJour = ventes
            .GroupBy(v => v.DateVente.Date)
            .Select(g => new VenteParJour { Jour = g.Key, Total = g.Sum(x => x.Total) })
            .OrderBy(x => x.Jour)
            .ToList();

        ViewBag.Du = du.Value.ToString("yyyy-MM-dd");
        ViewBag.Au = au.Value.ToString("yyyy-MM-dd");
        ViewBag.NbVentes = ventes.Count;
        ViewBag.SousTotal = ventes.Sum(v => v.SousTotal);
        ViewBag.Taxe1 = ventes.Sum(v => v.MontantTaxe1);
        ViewBag.Taxe2 = ventes.Sum(v => v.MontantTaxe2);
        ViewBag.Total = ventes.Sum(v => v.Total);
        ViewBag.PanierMoyen = ventes.Count > 0 ? ventes.Average(v => v.Total) : 0;
        ViewBag.ParJour = parJour;

        return View(ventes.OrderByDescending(v => v.DateVente).ToList());
    }

    // Meilleurs vendeurs
    public async Task<IActionResult> MeilleursVendeurs(string? departement)
    {
        var q = _db.Articles.AsNoTracking().Where(a => a.QteVendu > 0);
        if (!string.IsNullOrWhiteSpace(departement))
            q = q.Where(a => a.Departement == departement);

        var liste = await q
            .OrderByDescending(a => a.QteVendu)
            .Take(100)
            .Select(a => new MeilleurVendeur
            {
                Description = a.Description ?? a.ArticleId,
                Departement = a.Departement ?? "",
                QteVendu = a.QteVendu ?? 0,
                PrixVente = a.PrixVente ?? 0
            })
            .ToListAsync();

        ViewBag.Departements = await _db.Articles
            .Where(a => a.Departement != null && a.Departement != "")
            .Select(a => a.Departement!).Distinct().OrderBy(d => d).ToListAsync();
        ViewBag.Departement = departement;
        return View(liste);
    }

    // Valeur du stock par département
    public async Task<IActionResult> ValeurStock()
    {
        var data = await _db.Articles.AsNoTracking()
            .Where(a => a.QteMain > 0)
            .GroupBy(a => a.Departement)
            .Select(g => new
            {
                Departement = g.Key ?? "(Aucun)",
                NbArticles = g.Count(),
                QteTotale = g.Sum(a => a.QteMain ?? 0),
                ValeurCout = g.Sum(a => (a.QteMain ?? 0) * (a.Coutant ?? 0)),
                ValeurVente = g.Sum(a => (a.QteMain ?? 0) * (a.PrixVente ?? 0))
            })
            .OrderByDescending(x => x.ValeurCout)
            .ToListAsync();

        ViewBag.TotalCout = data.Sum(x => x.ValeurCout);
        ViewBag.TotalVente = data.Sum(x => x.ValeurVente);
        return View(data.Select(x => new ValeurStockLigne
        {
            Departement = x.Departement,
            NbArticles = x.NbArticles,
            QteTotale = x.QteTotale,
            ValeurCout = x.ValeurCout,
            ValeurVente = x.ValeurVente
        }).ToList());
    }

    // Stock faible / a commander
    public async Task<IActionResult> StockFaible()
    {
        var liste = await _db.Articles.AsNoTracking()
            .Where(a => a.Actif && a.QteMin > 0 && a.QteMain <= a.QteMin)
            .OrderBy(a => a.QteMain)
            .ToListAsync();
        return View(liste);
    }
}
