using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.TPV.Models;

[Table("Fournisseur")]
public class Fournisseur
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(16)]
    [Column("Fournisseur")]
    [Display(Name = "Code fournisseur")]
    public string Code { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Nom { get; set; }

    [StringLength(30)]
    public string? Adresse { get; set; }

    [StringLength(30)]
    public string? Ville { get; set; }

    [Display(Name = "Code postal")]
    [StringLength(6)]
    public string? CodePostal { get; set; }

    [Display(Name = "Téléphone")]
    [StringLength(16)]
    public string? Telephone { get; set; }

    [StringLength(16)]
    public string? Telecopieur { get; set; }

    [StringLength(50)]
    public string? EMail { get; set; }

    [StringLength(30)]
    public string? Contact { get; set; }

    [Display(Name = "Dernier achat")]
    public DateTime? DernierAchat { get; set; }

    [Display(Name = "Total achats")]
    [Column(TypeName = "money")]
    public decimal? TotalAchat { get; set; }
}
