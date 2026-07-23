using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Inventory.TPV.Models;

/// <summary>
/// Utilisateur applicatif. Étend IdentityUser avec les colonnes personnalisées
/// déjà présentes (NOT NULL) dans la table dbo.AspNetUsers.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Display(Name = "Nom complet")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Ville")]
    [StringLength(50)]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Province")]
    [StringLength(50)]
    public string State { get; set; } = string.Empty;

    [Display(Name = "Pays")]
    [StringLength(50)]
    public string Country { get; set; } = string.Empty;

    [Display(Name = "Code postal")]
    [StringLength(10)]
    public string PostalCode { get; set; } = string.Empty;
}
