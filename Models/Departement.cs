using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.TPV.Models;

[Table("Departement")]
public class Departement
{
    [Key]
    public int ID { get; set; }

    [Required]
    [StringLength(16)]
    [Column("Departement")]
    [Display(Name = "Département")]
    public string DepartementCode { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Description { get; set; }

    public bool Negatif { get; set; }

    [Display(Name = "Vente à zéro")]
    public bool VenteAzero { get; set; }

    [Display(Name = "TPS")]
    public bool Taxe1 { get; set; }

    [Display(Name = "TVQ")]
    public bool Taxe2 { get; set; }

    public bool Visible { get; set; }
}
