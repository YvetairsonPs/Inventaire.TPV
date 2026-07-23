using System.Text.Json;
using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Module de ventes : point de vente (POS) et historique des ventes.</summary>
public class VentesController : Controller
{
    private const decimal TauxTPS = 0.05m;   // TPS Québec
    private const decimal TauxTVQ = 0.09975m; // TVQ Québec

    private readonly AppDbContext _db;
    public VentesController(AppDbContext db) => _db = db;

    // Liste / historique
    public async Task<IActionResult> Index(DateTime? du, DateTime? au)
    {
        var q = _db.Ventes.AsNoTracking().AsQueryable();
        if (du.HasValue) q = q.Where(v => v.DateVente >= du.Value.Date);
        if (au.HasValue) q = q.Where(v => v.DateVente < au.Value.Date.AddDays(1));

        var ventes = await q.OrderByDescending(v => v.DateVente).Take(200).ToListAsync();
        ViewBag.Du = du?.ToString("yyyy-MM-dd");
        ViewBag.Au = au?.ToString("yyyy-MM-dd");
        ViewBag.TotalPeriode = ventes.Sum(v => v.Total);
        return View(ventes);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vente = await _db.Ventes.Include(v => v.Items).FirstOrDefaultAsync(v => v.Id == id);
        if (vente == null) return NotFound();
        return View(vente);
    }

    // Reçu imprimable d'une vente (format ticket).
    public async Task<IActionResult> Recu(int id)
    {
        var vente = await _db.Ventes.Include(v => v.Items).FirstOrDefaultAsync(v => v.Id == id);
        if (vente == null) return NotFound();
        return View(vente);
    }

    // Rapport / reçu journalier : consultation et réimpression.
    public async Task<IActionResult> Journalier(DateTime? date)
    {
        var jour = (date ?? DateTime.Today).Date;
        var ventes = await _db.Ventes.AsNoTracking()
            .Where(v => v.DateVente >= jour && v.DateVente < jour.AddDays(1))
            .OrderBy(v => v.DateVente)
            .ToListAsync();

        ViewBag.Jour = jour;
        ViewBag.NbVentes = ventes.Count;
        ViewBag.SousTotal = ventes.Sum(v => v.SousTotal);
        ViewBag.Taxe1 = ventes.Sum(v => v.MontantTaxe1);
        ViewBag.Taxe2 = ventes.Sum(v => v.MontantTaxe2);
        ViewBag.Total = ventes.Sum(v => v.Total);
        ViewBag.NbArticles = ventes.SelectMany(v => v.Items).Count();

        // Répartition approximative par moyen de paiement (présence dans le libellé).
        ViewBag.NbComptant = ventes.Count(v => (v.MethodePaiement ?? "").Contains("Comptant"));
        ViewBag.NbInterac = ventes.Count(v => (v.MethodePaiement ?? "").Contains("Interac"));
        ViewBag.NbAutre = ventes.Count(v => (v.MethodePaiement ?? "").Contains("Autre"));

        return View(ventes);
    }

    // Point de vente
    public IActionResult Pos() => View();

    // Départements pour les touches de vente rapide (avec indicateurs de taxe).
    [HttpGet]
    public async Task<IActionResult> Departements()
    {
        var deps = await _db.Departements.AsNoTracking()
            .Where(d => d.Visible)
            .OrderBy(d => d.Negatif).ThenBy(d => d.DepartementCode)
            .Select(d => new { code = d.DepartementCode, desc = d.Description ?? d.DepartementCode, taxe1 = d.Taxe1, taxe2 = d.Taxe2, negatif = d.Negatif })
            .ToListAsync();

        // Repli : si la table Departement est vide, on déduit depuis les articles.
        if (deps.Count == 0)
        {
            var distinct = await _db.Articles.AsNoTracking()
                .Where(a => a.Departement != null && a.Departement != "")
                .Select(a => a.Departement!)
                .Distinct().OrderBy(x => x).Take(24).ToListAsync();
            return Json(distinct.Select(c => new { code = c, desc = c, taxe1 = true, taxe2 = true, negatif = false }));
        }
        return Json(deps);
    }

    // Recherche d'article pour le POS (autocomplétion)
    [HttpGet]
    public async Task<IActionResult> Rechercher(string terme)
    {
        if (string.IsNullOrWhiteSpace(terme))
            return Json(Array.Empty<object>());

        var resultats = await _db.Articles.AsNoTracking()
            .Where(a => a.Actif && (a.CUP1 == terme || a.ArticleId == terme
                        || a.Description!.Contains(terme) || a.CUP1!.Contains(terme)))
            .OrderBy(a => a.Description)
            .Take(15)
            .Select(a => new
            {
                id = a.ArticleId,
                code = a.CUP1 ?? a.ArticleId,
                description = a.Description ?? a.ArticleId,
                prix = a.PrixVente ?? 0,
                taxe1 = a.Taxe1,
                taxe2 = a.Taxe2,
                stock = a.QteMain ?? 0
            })
            .ToListAsync();

        return Json(resultats);
    }

    public class PanierLigne
    {
        public string ArticleId { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal PrixUnitaire { get; set; }
        public decimal Quantite { get; set; }
        public bool Taxe1 { get; set; }
        public bool Taxe2 { get; set; }
    }

    public class EnregistrerVenteDto
    {
        public List<PanierLigne> Lignes { get; set; } = new();
        public string MethodePaiement { get; set; } = "Comptant";
        public string? Caissier { get; set; }
        public string? Commentaire { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Enregistrer([FromBody] EnregistrerVenteDto dto)
    {
        if (dto.Lignes == null || dto.Lignes.Count == 0)
            return BadRequest(new { message = "Le panier est vide." });

        decimal sousTotal = 0, taxe1 = 0, taxe2 = 0;
        var items = new List<VenteItem>();

        foreach (var l in dto.Lignes)
        {
            var ligneTotal = Math.Round(l.PrixUnitaire * l.Quantite, 2);
            sousTotal += ligneTotal;
            if (l.Taxe1) taxe1 += ligneTotal * TauxTPS;
            if (l.Taxe2) taxe2 += ligneTotal * TauxTVQ;

            items.Add(new VenteItem
            {
                ArticleId = l.ArticleId,
                Description = l.Description,
                PrixUnitaire = l.PrixUnitaire,
                Quantite = l.Quantite,
                Total = ligneTotal,
                Taxe1Appliquee = l.Taxe1,
                Taxe2Appliquee = l.Taxe2
            });
        }

        taxe1 = Math.Round(taxe1, 2);
        taxe2 = Math.Round(taxe2, 2);

        var vente = new Vente
        {
            NumeroFacture = $"F{DateTime.Now:yyyyMMddHHmmss}",
            DateVente = DateTime.Now,
            SousTotal = Math.Round(sousTotal, 2),
            MontantTaxe1 = taxe1,
            MontantTaxe2 = taxe2,
            Total = Math.Round(sousTotal + taxe1 + taxe2, 2),
            MethodePaiement = dto.MethodePaiement,
            CaissierNom = string.IsNullOrWhiteSpace(dto.Caissier) ? "POS" : dto.Caissier,
            Commentaire = dto.Commentaire,
            Items = items
        };

        using var tx = await _db.Database.BeginTransactionAsync();
        _db.Ventes.Add(vente);

        // Décrémente le stock et incrémente la quantité vendue.
        foreach (var l in dto.Lignes)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.ArticleId == l.ArticleId);
            if (article != null)
            {
                article.QteMain = (article.QteMain ?? 0) - l.Quantite;
                article.QteVendu = (article.QteVendu ?? 0) + l.Quantite;
                article.DateVendu = DateTime.Now;
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Json(new { success = true, id = vente.Id, facture = vente.NumeroFacture, total = vente.Total });
    }
}
