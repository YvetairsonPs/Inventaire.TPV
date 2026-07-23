using System.Globalization;
using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

// Culture québécoise : dollars, dates et nombres au format fr-CA.
var cultureCA = new CultureInfo("fr-CA");
CultureInfo.DefaultThreadCurrentCulture = cultureCA;
CultureInfo.DefaultThreadCurrentUICulture = cultureCA;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Toute l'application exige une authentification, sauf [AllowAnonymous].
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Bienvenue}/{id?}")
    .WithStaticAssets();

// Création du compte administrateur par défaut au démarrage (si la base répond).
await SeedIdentite.EnsureAdminAsync(app);

// Création des boutons de département par défaut (s'ils n'existent pas déjà).
await SeedDepartements.EnsureAsync(app);

app.Run();
