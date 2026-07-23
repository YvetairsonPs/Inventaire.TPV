using Inventory.TPV.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Data;

public static class SeedDepartements
{
    // Boutons de département par défaut du point de vente : (code, description, TPS, TVQ, déduction).
    private static readonly (string code, string desc, bool t1, bool t2, bool neg)[] Defauts =
    {
        ("TABAC",            "Tabac",            true,  true,  false),
        ("LOTO",             "Loto",             false, false, false),
        ("GRATTEUX",         "Gratteux",         false, false, false),
        ("DEPOT BOUT.",      "Depot Bout.",      false, false, false),
        ("AUTOBUS",          "Autobus",          false, false, false),
        ("FRAIS LIVRAISON",  "Frais Livraison",  true,  true,  false),
        ("RETOUR BOUT.",     "Retour Bout.",     false, false, true),
        ("LOTO GAGNANT",     "Loto Gagnant",     false, false, true),
        ("GRATTEUX GAGNANT", "Gratteux Gagnant", false, false, true),
    };

    /// <summary>Crée les boutons de département par défaut s'ils n'existent pas encore (n'écrase jamais ceux déjà présents).</summary>
    public static async Task EnsureAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();

        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var codesExistants = await db.Departements.Select(d => d.DepartementCode).ToListAsync();
            var maxId = await db.Departements.MaxAsync(d => (int?)d.ID) ?? 0;

            var aAjouter = Defauts.Where(x => !codesExistants.Contains(x.code)).ToList();
            foreach (var x in aAjouter)
            {
                db.Departements.Add(new Departement
                {
                    ID = ++maxId,
                    DepartementCode = x.code,
                    Description = x.desc,
                    Taxe1 = x.t1,
                    Taxe2 = x.t2,
                    Negatif = x.neg,
                    Visible = true
                });
            }

            if (aAjouter.Count > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Départements par défaut ajoutés : {Codes}", string.Join(", ", aAjouter.Select(a => a.code)));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Initialisation des départements ignorée (base indisponible ?)");
        }
    }
}
