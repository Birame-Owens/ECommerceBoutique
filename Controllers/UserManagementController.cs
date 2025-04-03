using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using ECommerceBoutique.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBoutique.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserImportService _userImportService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            UserImportService userImportService,
            ILogger<UserManagementController> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userImportService = userImportService ?? throw new ArgumentNullException(nameof(userImportService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: UserManagement
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                if (user == null) continue;

                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    IsVendor = roles.Contains("Vendor"),
                    IsAdmin = roles.Contains("Administrator"),
                    IsCustomer = roles.Contains("Customer"),
                    IsApiUser = user.Email?.Contains("dummyjson.com") ?? false,
                    OriginalApiPassword = user.OriginalApiPassword ?? string.Empty
                });
            }

            return View(userViewModels);
        }

        // POST: UserManagement/ToggleVendorRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVendorRole(string userId, bool isVendor)
        {
            // Debug - Loggez les valeurs pour voir ce qui arrive
            _logger.LogInformation($"ToggleVendorRole appelé avec userId={userId}, isVendor={isVendor}");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "ID d'utilisateur non valide.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Utilisateur non trouvé.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Vérifier le rôle actuel
                var isCurrentlyVendor = await _userManager.IsInRoleAsync(user, "Vendor");
                _logger.LogInformation($"L'utilisateur {user.UserName} est actuellement vendeur: {isCurrentlyVendor}");

                // Si on veut faire l'utilisateur vendeur et qu'il ne l'est pas déjà
                if (isVendor && !isCurrentlyVendor)
                {
                    // Vérifier si le rôle existe
                    if (!await _roleManager.RoleExistsAsync("Vendor"))
                    {
                        await _roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Vendor"));
                        _logger.LogInformation("Rôle Vendor créé car il n'existait pas");
                    }

                    // Ajouter l'utilisateur au rôle Vendor
                    var result = await _userManager.AddToRoleAsync(user, "Vendor");

                    if (result.Succeeded)
                    {
                        // Mettre à jour également la propriété IsVendor de l'utilisateur
                        user.IsVendor = true;
                        await _userManager.UpdateAsync(user);

                        TempData["SuccessMessage"] = $"Le rôle Vendeur a été attribué à {user.UserName}.";
                        _logger.LogInformation($"L'utilisateur {user.UserName} est maintenant vendeur");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError($"Erreur lors de l'ajout du rôle: {errors}");
                        TempData["ErrorMessage"] = $"Erreur lors de l'attribution du rôle: {errors}";
                    }
                }
                // Si on veut retirer le rôle vendeur et qu'il l'est déjà
                else if (!isVendor && isCurrentlyVendor)
                {
                    // Retirer le rôle Vendor
                    var result = await _userManager.RemoveFromRoleAsync(user, "Vendor");

                    if (result.Succeeded)
                    {
                        // Mettre à jour également la propriété IsVendor de l'utilisateur
                        user.IsVendor = false;
                        await _userManager.UpdateAsync(user);

                        TempData["SuccessMessage"] = $"Le rôle Vendeur a été retiré de {user.UserName}.";
                        _logger.LogInformation($"Le rôle vendeur a été retiré de l'utilisateur {user.UserName}");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError($"Erreur lors du retrait du rôle: {errors}");
                        TempData["ErrorMessage"] = $"Erreur lors du retrait du rôle: {errors}";
                    }
                }
                else
                {
                    _logger.LogInformation($"Aucune action nécessaire: isVendor={isVendor}, isCurrentlyVendor={isCurrentlyVendor}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la modification du rôle pour l'utilisateur {user.UserName}");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la modification du rôle: " + ex.Message;
            }

            // Rediriger vers la page d'où venait la requête (Details ou Index)
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("Details"))
            {
                return RedirectToAction("Details", new { id = userId });
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "ID d'utilisateur non valide.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Utilisateur non trouvé.";
                return RedirectToAction(nameof(Index));
            }

            // Ne pas permettre la suppression du compte administrateur principal
            if (user.Email == "admin@example.com" || await _userManager.IsInRoleAsync(user, "Administrator"))
            {
                TempData["ErrorMessage"] = "Les comptes administrateurs ne peuvent pas être supprimés.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Supprimer l'utilisateur
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = $"L'utilisateur {user.UserName} a été supprimé avec succès.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Erreur lors de la suppression de l'utilisateur : " +
                        string.Join(", ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la suppression de l'utilisateur {user.UserName}");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la suppression de l'utilisateur.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/DeleteApiUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteApiUsers()
        {
            try
            {
                int deletedCount = await _userImportService.DeleteApiUsersAsync();
                TempData["SuccessMessage"] = $"{deletedCount} utilisateurs API ont été supprimés avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des utilisateurs API");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la suppression des utilisateurs API.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/ImportApiUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportApiUsers()
        {
            try
            {
                var (importedCount, errorCount) = await _userImportService.ImportUsersAsync();

                if (errorCount > 0)
                {
                    TempData["WarningMessage"] = $"{importedCount} utilisateurs importés, mais {errorCount} erreurs rencontrées. Consultez les logs pour plus de détails.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"{importedCount} utilisateurs ont été importés avec succès.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'importation des utilisateurs API");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de l'importation des utilisateurs API.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: UserManagement/Details/id
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var userDetailViewModel = new UserDetailViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? "Inconnu",
                Email = user.Email ?? "Inconnu",
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? string.Empty,
                City = user.City ?? string.Empty,
                PostalCode = user.PostalCode ?? string.Empty,
                Country = user.Country ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles.ToList(),
                IsApiUser = user.Email?.Contains("dummyjson.com") ?? false,
                OriginalApiPassword = user.OriginalApiPassword ?? string.Empty
            };

            return View(userDetailViewModel);
        }

        // GET: UserManagement/ApiUserCredentials
        public async Task<IActionResult> ApiUserCredentials()
        {
            var apiUsers = await _userImportService.GetApiUsersWithPasswordsAsync();
            return View(apiUsers);
        }
    }
}