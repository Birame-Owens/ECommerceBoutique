// Controllers/HomeController.cs
using ECommerceBoutique.Data;
using ECommerceBoutique.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ECommerceBoutique.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchTerm = null, int? categoryId = null)
        {
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .AsQueryable();

            // Filtrer par terme de recherche
            if (!string.IsNullOrEmpty(searchTerm))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Title.Contains(searchTerm) ||
                    p.Description.Contains(searchTerm));
                ViewData["CurrentFilter"] = searchTerm;
            }

            // Filtrer par catégorie
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = await productsQuery.ToListAsync();

            // Récupérer les catégories pour le filtre
            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}