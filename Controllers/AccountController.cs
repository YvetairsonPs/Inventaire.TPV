using System.ComponentModel.DataAnnotations;
using Inventory.TPV.Data;
using Inventory.TPV.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.TPV.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.Souvenir, lockoutOnFailure: false);
        if (result.Succeeded)
            return RedirectToLocal(returnUrl);

        ModelState.AddModelError(string.Empty, "Courriel ou mot de passe invalide.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // Auto-inscription publique : un visiteur crée son compte (rôle Caissier) et est connecté.
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Inscription() => View(new InscriptionModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscription(InscriptionModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName ?? "",
            Address = "", City = "", State = "QC", Country = "Canada", PostalCode = ""
        };
        var res = await _users.CreateAsync(user, model.Password);
        if (res.Succeeded)
        {
            await _users.AddToRoleAsync(user, SeedIdentite.RoleCaissier);
            await _signIn.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }
        foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
        return View(model);
    }

    // Réservé aux administrateurs : créer un utilisateur (caissier).
    [Authorize(Roles = SeedIdentite.RoleAdmin)]
    [HttpGet]
    public IActionResult Register() => View(new RegisterModel());

    [Authorize(Roles = SeedIdentite.RoleAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName ?? "",
            Address = "", City = "", State = "QC", Country = "Canada", PostalCode = ""
        };
        var res = await _users.CreateAsync(user, model.Password);
        if (res.Succeeded)
        {
            await _users.AddToRoleAsync(user, model.Role ?? SeedIdentite.RoleCaissier);
            TempData["Succes"] = $"Utilisateur « {model.Email} » créé.";
            return RedirectToAction(nameof(Utilisateurs));
        }
        foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
        return View(model);
    }

    [Authorize(Roles = SeedIdentite.RoleAdmin)]
    [HttpGet]
    public IActionResult Utilisateurs()
    {
        var liste = _users.Users
            .OrderBy(u => u.Email)
            .Select(u => new { u.Email, u.FullName })
            .ToList();
        return View(liste.Select(u => (u.Email ?? "", u.FullName)).ToList());
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Accueil", "Home");
}

public class LoginModel
{
    [Required(ErrorMessage = "Le courriel est requis.")]
    [Display(Name = "Courriel")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Se souvenir de moi")]
    public bool Souvenir { get; set; }
}

public class RegisterModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Courriel")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Nom complet")]
    public string? FullName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Le mot de passe doit comporter au moins 6 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Rôle")]
    public string? Role { get; set; }
}

public class InscriptionModel
{
    [Required(ErrorMessage = "Le courriel est requis.")]
    [EmailAddress(ErrorMessage = "Courriel invalide.")]
    [Display(Name = "Courriel")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Nom complet")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Le mot de passe doit comporter au moins 6 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
