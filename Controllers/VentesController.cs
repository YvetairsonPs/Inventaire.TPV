using System.Text.Json;
using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Inventory.TPV.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Controllers;

/// <summary>Module de ventes : point de vente (POS) et historique des ventes.</summary>
public class VentesController : Controller
{
    private const decimal TauxTPS = 0.05m;   // TPS Québec
    private const decimal TauxTVQ = 0.09975m; // TVQ Québec

    private readonly AppDbContext _db;
    private readonly ScanRelay _relais;

    public VentesController(AppDbContext db, ScanRelay relais)
    {
        _db = db;
        _relais = relais;
    }

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

    // ===================== Scan par caméra =====================

    /// <summary>
    /// Variantes plausibles d'un code lu par la caméra, de la plus proche à la plus permissive.
    /// La base historique stocke les CUP sans chiffre de contrôle et sans zéros de tête
    /// (Coca-Cola : UPC-A 067000011047 → CUP1 « 6700001104 »), alors que la caméra rend le code complet.
    /// </summary>
    internal static List<string> VariantesCode(string code)
    {
        var s = (code ?? "").Trim();
        var v = new List<string>();
        void Ajouter(string? x)
        {
            if (string.IsNullOrEmpty(x) || v.Contains(x)) return;
            v.Add(x);
        }

        Ajouter(s);
        if (s.Length == 0 || !s.All(char.IsDigit)) return v;

        // Un UPC-E (8 chiffres) doit d'abord être détendu en UPC-A pour être reconnaissable.
        var upcA = EtendreUpcE(s);
        if (upcA != null) Ajouter(upcA);

        // EAN-13 commençant par 0 = UPC-A précédé d'un zéro de norme.
        var bases = new List<string> { s };
        if (upcA != null) bases.Add(upcA);
        if (s.Length == 13 && s[0] == '0') bases.Add(s[1..]);

        foreach (var b in bases.ToList())
        {
            Ajouter(b);
            Ajouter(b.TrimStart('0'));
            if (b.Length >= 8)
            {
                var sansControle = b[..^1];          // le CUP stocké omet le chiffre de contrôle
                Ajouter(sansControle);
                Ajouter(sansControle.TrimStart('0'));
            }
        }

        Ajouter("0" + s);
        return v;
    }

    /// <summary>Étend un UPC-E compressé (8 chiffres) en UPC-A (12 chiffres). Null si non applicable.</summary>
    private static string? EtendreUpcE(string s)
    {
        if (s.Length != 8 || !s.All(char.IsDigit)) return null;
        if (s[0] != '0' && s[0] != '1') return null;

        char n = s[0], c = s[7];
        string d = s[1..7];
        var corps = d[5] switch
        {
            '0' or '1' or '2' => $"{d[0]}{d[1]}{d[5]}0000{d[2]}{d[3]}{d[4]}",
            '3' => $"{d[0]}{d[1]}{d[2]}00000{d[3]}{d[4]}",
            '4' => $"{d[0]}{d[1]}{d[2]}{d[3]}00000{d[4]}",
            _ => $"{d[0]}{d[1]}{d[2]}{d[3]}{d[4]}0000{d[5]}"
        };
        return $"{n}{corps}{c}";
    }

    /// <summary>
    /// Recherche stricte par code-barres (CUP1) ou code article : un code scanné ne doit jamais
    /// ajouter un article seulement approchant. La variante la plus proche du code lu l'emporte.
    /// </summary>
    private async Task<object?> ArticleParCodeAsync(string code)
    {
        var variantes = VariantesCode(code);
        if (variantes.Count == 0) return null;

        var candidats = await _db.Articles.AsNoTracking()
            .Where(x => x.Actif && (variantes.Contains(x.CUP1!) || variantes.Contains(x.ArticleId)))
            .Take(20)
            .ToListAsync();

        if (candidats.Count == 0) return null;

        // Un CUP1 prime sur un code article, et la variante la moins retouchée prime sur les autres.
        int Rang(Article x)
        {
            var parCup = x.CUP1 == null ? -1 : variantes.IndexOf(x.CUP1.Trim());
            var parId = variantes.IndexOf(x.ArticleId.Trim());
            var r = int.MaxValue;
            if (parCup >= 0) r = parCup;
            if (parId >= 0) r = Math.Min(r, parId + 100);
            return r;
        }

        var a = candidats.OrderBy(Rang).First();

        return new
        {
            id = a.ArticleId,
            code = a.CUP1 ?? a.ArticleId,
            description = a.Description ?? a.ArticleId,
            prix = a.PrixVente ?? 0,
            taxe1 = a.Taxe1,
            taxe2 = a.Taxe2,
            stock = a.QteMain ?? 0
        };
    }

    /// <summary>Résolution d'un code scanné, pour la caisse comme pour le téléphone.</summary>
    [HttpGet]
    public async Task<IActionResult> ParCodeBarres(string code)
    {
        var a = await ArticleParCodeAsync(code);
        return a == null
            ? Json(new { trouve = false, code })
            : Json(new { trouve = true, article = a });
    }

    /// <summary>Page plein écran transformant le téléphone en douchette pour une caisse appairée.</summary>
    public IActionResult Douchette(string? code) { ViewBag.CodeAppairage = code; return View(); }

    /// <summary>
    /// Sert l'autorité de certification locale (partie publique uniquement) pour que le téléphone
    /// puisse faire confiance au site en HTTPS — préalable obligatoire à l'usage de la caméra.
    /// Anonyme : le téléphone doit pouvoir la récupérer avant même de pouvoir se connecter.
    /// Le fichier est produit par deploy\Setup-Https.ps1.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult CertificatCA()
    {
        var chemin = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Inventaire.TPV", "ca-inventaire-tpv.crt");

        if (!System.IO.File.Exists(chemin))
            return NotFound("Autorité locale absente. Lancez deploy\\Setup-Https.ps1 en administrateur.");

        return PhysicalFile(chemin, "application/x-x509-ca-cert", "ca-inventaire-tpv.crt");
    }

    public class ScanDto
    {
        public string Code { get; set; } = "";
        public string CodeBarres { get; set; } = "";
    }

    /// <summary>La caisse ouvre une session et affiche le code à saisir sur le téléphone.</summary>
    [HttpPost]
    public IActionResult OuvrirSessionScan()
    {
        var session = _relais.Ouvrir(User.Identity?.Name ?? "");
        var url = Url.Action("Douchette", "Ventes", new { code = session.Code }, Request.Scheme) ?? "";

        // Le caissier travaille souvent sur « localhost » : cette adresse ne mène nulle part
        // depuis le téléphone. On lui substitue l'adresse du poste sur le réseau local.
        var hote = Request.Host.Host;
        if (hote is "localhost" or "127.0.0.1" or "::1")
        {
            var ip = AdresseReseauLocal();
            if (ip != null) url = url.Replace($"//{hote}", $"//{ip}");
        }

        return Json(new { code = session.Code, url });
    }

    /// <summary>
    /// Adresse IPv4 du poste sur le réseau local, prise sur l'interface qui porte la route par défaut.
    /// La connexion UDP ne transmet rien : elle sert seulement à faire choisir l'interface par l'OS.
    /// </summary>
    private static string? AdresseReseauLocal()
    {
        try
        {
            using var s = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
            s.Connect("8.8.8.8", 65530);
            return (s.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    /// <summary>Le téléphone vérifie que le code d'appairage est valide avant de démarrer la caméra.</summary>
    [HttpPost]
    public IActionResult VerifierSessionScan([FromBody] ScanDto dto)
    {
        var session = _relais.Trouver(dto.Code, User.Identity?.Name ?? "");
        if (session == null) return Json(new { ok = false, message = "Code d'appairage inconnu ou expiré." });
        session.TelephoneConnecte = true;
        session.DerniereActiviteUtc = DateTime.UtcNow;
        return Json(new { ok = true });
    }

    /// <summary>Le téléphone envoie un code-barres à la caisse et reçoit l'article reconnu en retour.</summary>
    [HttpPost]
    public async Task<IActionResult> PousserScan([FromBody] ScanDto dto)
    {
        var session = _relais.Trouver(dto.Code, User.Identity?.Name ?? "");
        if (session == null) return Json(new { ok = false, message = "Session expirée — réappairez le téléphone." });
        if (!_relais.Pousser(session, dto.CodeBarres)) return Json(new { ok = false, message = "Code vide." });

        var article = await ArticleParCodeAsync(dto.CodeBarres);
        return Json(new { ok = true, trouve = article != null, article });
    }

    /// <summary>Attente longue : la caisse récupère les codes scannés dès qu'ils arrivent.</summary>
    [HttpGet]
    public async Task<IActionResult> AttendreScans(string code, CancellationToken ct)
    {
        var session = _relais.Trouver(code, User.Identity?.Name ?? "");
        if (session == null) return Json(new { ok = false, message = "Session expirée." });

        var lot = await _relais.AttendreAsync(session, TimeSpan.FromSeconds(25), ct);
        return Json(new { ok = true, scans = lot, telephone = session.TelephoneConnecte });
    }

    [HttpPost]
    public IActionResult FermerSessionScan([FromBody] ScanDto dto)
    {
        var session = _relais.Trouver(dto.Code, User.Identity?.Name ?? "");
        if (session != null) _relais.Fermer(session);
        return Json(new { ok = true });
    }

    // ===================== Enregistrement d'une vente =====================

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
