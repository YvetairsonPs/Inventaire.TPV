namespace Inventory.TPV.Models;

public class DashboardViewModel
{
    public int NombreArticles { get; set; }
    public int ArticlesActifs { get; set; }
    public int ArticlesStockFaible { get; set; }
    public decimal ValeurStock { get; set; }

    public decimal VentesAujourdhui { get; set; }
    public int NbVentesAujourdhui { get; set; }
    public decimal VentesMois { get; set; }

    public List<Article> StockFaible { get; set; } = new();
    public List<MeilleurVendeur> MeilleursVendeurs { get; set; } = new();
    public List<VenteParJour> VentesParJour { get; set; } = new();
}

public class MeilleurVendeur
{
    public string Description { get; set; } = string.Empty;
    public string Departement { get; set; } = string.Empty;
    public decimal QteVendu { get; set; }
    public decimal PrixVente { get; set; }
}

public class VenteParJour
{
    public DateTime Jour { get; set; }
    public decimal Total { get; set; }
}

public class ValeurStockLigne
{
    public string Departement { get; set; } = "";
    public int NbArticles { get; set; }
    public decimal QteTotale { get; set; }
    public decimal ValeurCout { get; set; }
    public decimal ValeurVente { get; set; }
}
