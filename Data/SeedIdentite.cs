using Inventory.TPV.Models;
using Microsoft.AspNetCore.Identity;

namespace Inventory.TPV.Data;

public static class SeedIdentite
{
    public const string AdminEmail = "admin@inventaire.local";
    public const string AdminMotDePasse = "Admin123!";
    public const string RoleAdmin = "Admin";
    public const string RoleCaissier = "Caissier";

    /// <summary>Crée les rôles et un compte administrateur par défaut s'il n'existe pas encore.</summary>
    public static async Task EnsureAdminAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();

        try
        {
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var role in new[] { RoleAdmin, RoleCaissier })
            {
                if (!await roleMgr.RoleExistsAsync(role))
                    await roleMgr.CreateAsync(new IdentityRole(role));
            }

            if (await userMgr.FindByEmailAsync(AdminEmail) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true,
                    FullName = "Administrateur",
                    Address = "",
                    City = "",
                    State = "QC",
                    Country = "Canada",
                    PostalCode = ""
                };
                var res = await userMgr.CreateAsync(admin, AdminMotDePasse);
                if (res.Succeeded)
                {
                    await userMgr.AddToRoleAsync(admin, RoleAdmin);
                    logger.LogInformation("Compte administrateur créé : {Email}", AdminEmail);
                }
                else
                {
                    logger.LogWarning("Échec création admin : {Erreurs}",
                        string.Join("; ", res.Errors.Select(e => e.Description)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Initialisation Identity ignorée (base indisponible ?)");
        }
    }
}
