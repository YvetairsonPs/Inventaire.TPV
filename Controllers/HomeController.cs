using System.Diagnostics;
using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Page d'accueil publique (vitrine) : présentation du logiciel, sans connexion requise.
    // C'est la page par défaut au lancement du site ; mène vers la connexion.
    [AllowAnonymous]
    public IActionResult Bienvenue()
    {
        // Un utilisateur déjà connecté est redirigé directement vers son lanceur de modules.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Accueil));

        return View();
    }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel();

        try
        {
            var aujourdhui = DateTime.Today;
            var debutMois = new DateTime(aujourdhui.Year, aujourdhui.Month, 1);

            vm.NombreArticles = await _db.Articles.CountAsync();
            vm.ArticlesActifs = await _db.Articles.CountAsync(a => a.Actif);

            vm.ValeurStock = await _db.Articles
                .Where(a => a.QteMain > 0 && a.Coutant > 0)
                .SumAsync(a => (decimal?)(a.QteMain!.Value * a.Coutant!.Value)) ?? 0;

            vm.StockFaible = await _db.Articles
                .Where(a => a.Actif && a.QteMin > 0 && a.QteMain <= a.QteMin)
                .OrderBy(a => a.QteMain)
                .Take(10)
                .ToListAsync();
            vm.ArticlesStockFaible = await _db.Articles
                .CountAsync(a => a.Actif && a.QteMin > 0 && a.QteMain <= a.QteMin);

            vm.VentesAujourdhui = await _db.Ventes
                .Where(v => v.DateVente >= aujourdhui)
                .SumAsync(v => (decimal?)v.Total) ?? 0;
            vm.NbVentesAujourdhui = await _db.Ventes.CountAsync(v => v.DateVente >= aujourdhui);
            vm.VentesMois = await _db.Ventes
                .Where(v => v.DateVente >= debutMois)
                .SumAsync(v => (decimal?)v.Total) ?? 0;

            vm.MeilleursVendeurs = await _db.Articles
                .Where(a => a.QteVendu > 0 && a.Description != null && a.Description != "")
                .OrderByDescending(a => a.QteVendu)
                .Take(8)
                .Select(a => new MeilleurVendeur
                {
                    Description = a.Description!,
                    Departement = a.Departement ?? "",
                    QteVendu = a.QteVendu ?? 0,
                    PrixVente = a.PrixVente ?? 0
                })
                .ToListAsync();

            var depuis = aujourdhui.AddDays(-13);
            vm.VentesParJour = (await _db.Ventes
                    .Where(v => v.DateVente >= depuis)
                    .ToListAsync())
                .GroupBy(v => v.DateVente.Date)
                .Select(g => new VenteParJour { Jour = g.Key, Total = g.Sum(x => x.Total) })
                .OrderBy(x => x.Jour)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur de chargement du tableau de bord (base de données indisponible ?)");
            ViewBag.DbError = ex.Message;
        }

        return View(vm);
    }

    // Page d'accueil / lanceur : écran de démarrage avec grandes tuiles vers les modules.
    public async Task<IActionResult> Accueil()
    {
        var vm = new DashboardViewModel();

        try
        {
            var aujourdhui = DateTime.Today;

            vm.VentesAujourdhui = await _db.Ventes
                .Where(v => v.DateVente >= aujourdhui)
                .SumAsync(v => (decimal?)v.Total) ?? 0;
            vm.NbVentesAujourdhui = await _db.Ventes.CountAsync(v => v.DateVente >= aujourdhui);
            vm.NombreArticles = await _db.Articles.CountAsync();
            vm.ArticlesStockFaible = await _db.Articles
                .CountAsync(a => a.Actif && a.QteMin > 0 && a.QteMain <= a.QteMin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur de chargement de la page d'accueil (base de données indisponible ?)");
            ViewBag.DbError = ex.Message;
        }

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
