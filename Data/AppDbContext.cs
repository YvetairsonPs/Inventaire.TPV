using Inventory.TPV.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventory.TPV.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Departement> Departements => Set<Departement>();
    public DbSet<Fournisseur> Fournisseurs => Set<Fournisseur>();
    public DbSet<Vente> Ventes => Set<Vente>();
    public DbSet<VenteItem> VenteItems => Set<VenteItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // mappe les tables AspNet* (Identity)

        modelBuilder.Entity<Vente>()
            .HasMany(v => v.Items)
            .WithOne(i => i.Vente!)
            .HasForeignKey(i => i.VenteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
