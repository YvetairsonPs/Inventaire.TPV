using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.TPV.Models;

[Table("Ventes")]
public class Vente
{
    [Key]
    public int Id { get; set; }

    [Display(Name = "No facture")]
    public string NumeroFacture { get; set; } = string.Empty;

    [Display(Name = "Date")]
    public DateTime DateVente { get; set; }

    [Display(Name = "Sous-total")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal SousTotal { get; set; }

    [Display(Name = "TPS")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantTaxe1 { get; set; }

    [Display(Name = "TVQ")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantTaxe2 { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [Display(Name = "Paiement")]
    public string? MethodePaiement { get; set; }

    public string? Commentaire { get; set; }

    [Display(Name = "Caissier")]
    public string? CaissierNom { get; set; }

    public List<VenteItem> Items { get; set; } = new();
}

[Table("VenteItems")]
public class VenteItem
{
    [Key]
    public int Id { get; set; }

    public int VenteId { get; set; }

    [StringLength(16)]
    public string ArticleId { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Display(Name = "Prix unitaire")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrixUnitaire { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantite { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public bool Taxe1Appliquee { get; set; }
    public bool Taxe2Appliquee { get; set; }

    [ForeignKey(nameof(VenteId))]
    public Vente? Vente { get; set; }
}
