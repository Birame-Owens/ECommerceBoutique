using ECommerceBoutique.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBoutique.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly UserImportService _userImportService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(UserImportService userImportService, ILogger<AdminController> logger)
        {
            _userImportService = userImportService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> ImportUsers()
        {
            try
            {
                await _userImportService.ImportUsersAsync();
                TempData["SuccessMessage"] = "Les utilisateurs ont été importés avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'importation des utilisateurs");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de l'importation des utilisateurs.";
            }

            return RedirectToAction("Index");
        }
    }
}