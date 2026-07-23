using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.TPV.Models;

/// <summary>
/// Article / produit. Mappé sur la table existante dbo.Article (PK = ArticleId).
/// </summary>
[Table("Article")]
public class Article
{
    public int ID { get; set; }

    [Display(Name = "No article")]
    [StringLength(16)]
    public string NoArticle { get; set; } = string.Empty;

    [Key]
    [Display(Name = "Code article")]
    [StringLength(16)]
    public string ArticleId { get; set; } = string.Empty;

    [StringLength(16)]
    public string? SKU { get; set; }

    [Display(Name = "Code-barres")]
    [StringLength(16)]
    public string? CUP1 { get; set; }

    [Display(Name = "No fournisseur")]
    [StringLength(16)]
    public string? NoFournisseur { get; set; }

    [StringLength(60)]
    public string? Description { get; set; }

    [Display(Name = "Description (Ang)")]
    [StringLength(60)]
    public string? DescriptionAng { get; set; }

    [Display(Name = "Département")]
    [StringLength(16)]
    public string? Departement { get; set; }

    [StringLength(16)]
    public string? Fournisseur { get; set; }

    public int? FabricantId { get; set; }

    [Display(Name = "Article lié")]
    [StringLength(16)]
    public string? ArticleLie { get; set; }

    [Display(Name = "Coûtant")]
    [Column(TypeName = "money")]
    public decimal? Coutant { get; set; }

    [Display(Name = "Coût moyen")]
    [Column(TypeName = "money")]
    public decimal? CoutMoyen { get; set; }

    [Display(Name = "Prix de vente")]
    [Column(TypeName = "money")]
    public decimal? PrixVente { get; set; }

    [Display(Name = "Prix solde")]
    [Column(TypeName = "money")]
    public decimal? PrixSolde { get; set; }

    [Column(TypeName = "money")]
    public decimal? PrixDivers { get; set; }

    [Column(TypeName = "money")]
    public decimal? PrixAutre { get; set; }

    [Column(TypeName = "money")]
    public decimal? PrixBoite { get; set; }

    [Display(Name = "Profit moyen")]
    [Column(TypeName = "money")]
    public decimal? ProfitMoyen { get; set; }

    [Display(Name = "Qté en main")]
    [Column(TypeName = "money")]
    public decimal? QteMain { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteReserve { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteEntrepot { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteCommande { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteAchete { get; set; }

    [Display(Name = "Qté min")]
    [Column(TypeName = "money")]
    public decimal? QteMin { get; set; }

    [Display(Name = "Qté max")]
    [Column(TypeName = "money")]
    public decimal? QteMax { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteLot { get; set; }

    [Column(TypeName = "money")]
    public decimal? QteEmbalage { get; set; }

    [Display(Name = "Qté prise inv.")]
    [Column(TypeName = "money")]
    public decimal? QtePriseInv { get; set; }

    [Display(Name = "Prise inventaire")]
    public bool PriseInv { get; set; }

    [Display(Name = "Qté vendue")]
    [Column(TypeName = "money")]
    public decimal? QteVendu { get; set; }

    [StringLength(40)]
    public string? Localisation { get; set; }

    [Display(Name = "Actif")]
    public bool Actif { get; set; }

    [Display(Name = "TPS")]
    public bool Taxe1 { get; set; }

    [Display(Name = "TVQ")]
    public bool Taxe2 { get; set; }

    [Display(Name = "Date créé")]
    public DateTime? DateCree { get; set; }

    [Display(Name = "Date vendu")]
    public DateTime? DateVendu { get; set; }

    [Display(Name = "Date reçu")]
    public DateTime? DateRecu { get; set; }

    [Display(Name = "Date modifié")]
    public DateTime? DateModifie { get; set; }

    [StringLength(50)]
    public string? Photo { get; set; }

    // Champs calculés (non mappés)
    [NotMapped]
    public decimal MargeBeneficiaire =>
        (PrixVente ?? 0) > 0 && (Coutant ?? 0) > 0
            ? Math.Round(((PrixVente!.Value - Coutant!.Value) / PrixVente.Value) * 100, 1)
            : 0;

    [NotMapped]
    public bool StockFaible => (QteMain ?? 0) <= (QteMin ?? 0) && (QteMin ?? 0) > 0;
}
