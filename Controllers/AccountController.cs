// Controllers/AccountController.cs
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBoutique.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    IsVendor = model.IsVendor
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Vérifier si les rôles existent, sinon les créer
                    string roleName = model.IsVendor ? "Vendor" : "Customer";

                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(roleName));
                    }

                    // Ajouter l'utilisateur au rôle approprié
                    await _userManager.AddToRoleAsync(user, roleName);

                    // Connecter l'utilisateur
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // D'abord, essayons de trouver l'utilisateur par son email
                var user = await _userManager.FindByEmailAsync(model.Email);

                // Vérifier si c'est un utilisateur API (email contient "dummyjson.com")
                if (user != null && user.Email != null && user.Email.Contains("dummyjson.com"))
                {
                    _logger.LogInformation($"Tentative de connexion pour un utilisateur API: {model.Email}");

                    // Méthode 1: Vérifier si le mot de passe correspond au OriginalApiPassword stocké
                    if (!string.IsNullOrEmpty(user.OriginalApiPassword) && model.Password == user.OriginalApiPassword)
                    {
                        _logger.LogInformation($"Mot de passe original API valide pour {model.Email}");
                        await _signInManager.SignInAsync(user, model.RememberMe);
                        return RedirectToLocal(returnUrl);
                    }

                    // Méthode 2: Essayer la méthode standard
                    var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"Connexion standard réussie pour {model.Email}");
                        return RedirectToLocal(returnUrl);
                    }

                    // Méthode 3: Essayer avec le nom d'utilisateur plutôt que l'email
                    if (user.UserName != null && user.UserName != user.Email)
                    {
                        result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                        if (result.Succeeded)
                        {
                            _logger.LogInformation($"Connexion avec nom d'utilisateur réussie pour {user.UserName}");
                            return RedirectToLocal(returnUrl);
                        }
                    }

                    // Méthode 4: Forcer la connexion en dernier recours (pour démonstration)
                    // Attention: Ne faites ceci que pour les utilisateurs de démonstration en phase de test
                    if (User.IsInRole("Administrator"))
                    {
                        _logger.LogWarning($"Forçage de connexion pour l'utilisateur API {model.Email} par un administrateur");
                        await _signInManager.SignInAsync(user, model.RememberMe);
                        return RedirectToLocal(returnUrl);
                    }

                    // Toutes les méthodes ont échoué pour cet utilisateur API
                    _logger.LogWarning($"Échec d'authentification pour l'utilisateur API {model.Email}");
                    ModelState.AddModelError(string.Empty, "Les informations d'identification pour cet utilisateur API ne sont pas valides.");
                }
                else
                {
                    // Authentification standard pour les utilisateurs normaux
                    var result = await _signInManager.PasswordSignInAsync(
                        model.Email,
                        model.Password,
                        model.RememberMe,
                        lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Tentative de connexion non valide.");
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email,
                Address = user.Address,
                City = user.City,
                PostalCode = user.PostalCode,
                Country = user.Country
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Address = model.Address;
                user.City = model.City;
                user.PostalCode = model.PostalCode;
                user.Country = model.Country;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["StatusMessage"] = "Votre profil a été mis à jour avec succès.";
                    return RedirectToAction(nameof(Profile));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Votre mot de passe a été changé avec succès.";

            return RedirectToAction(nameof(Profile));
        }

        // Nouvelle méthode - Connexion instantanée pour les administrateurs
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> LoginAsUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "ID d'utilisateur non spécifié.";
                return RedirectToAction("Index", "UserManagement");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Utilisateur non trouvé.";
                return RedirectToAction("Index", "UserManagement");
            }

            try
            {
                // Déconnecter l'utilisateur actuel
                await _signInManager.SignOutAsync();

                // Connecter en tant que l'utilisateur sélectionné
                await _signInManager.SignInAsync(user, isPersistent: false);

                TempData["SuccessMessage"] = $"Vous êtes maintenant connecté en tant que {user.UserName}.";

                // Rediriger vers la page d'accueil
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la connexion en tant que {user.UserName}");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la tentative de connexion.";
                return RedirectToAction("Index", "UserManagement");
            }
        }

        // Nouvelle méthode pour afficher et tester les utilisateurs API
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ApiUsersList()
        {
            var apiUsers = await _userManager.Users
                .Where(u => u.Email != null && u.Email.Contains("dummyjson.com"))
                .ToListAsync();

            return View(apiUsers);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}