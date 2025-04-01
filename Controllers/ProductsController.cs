// Controllers/ProductsController.cs
using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using ECommerceBoutique.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ECommerceBoutique.Helpers;

namespace ECommerceBoutique.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ProductApiService _productApiService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ProductApiService productApiService,
            ILogger<ProductsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _productApiService = productApiService;
            _logger = logger;
        }

        // GET: Products
        public async Task<IActionResult> Index(string? sortOrder = null, string? searchString = null, int? categoryId = null, int pageNumber = 1)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParam"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParam"] = sortOrder == "Price" ? "price_desc" : "Price";
            ViewData["DateSortParam"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = categoryId;

            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .AsQueryable();

            // Filtrage par recherche
            if (!string.IsNullOrEmpty(searchString))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Title.Contains(searchString) ||
                    p.Description.Contains(searchString));
            }

            // Filtrage par catégorie
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            // Tri
            productsQuery = sortOrder switch
            {
                "name_desc" => productsQuery.OrderByDescending(p => p.Title),
                "Price" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                "Date" => productsQuery.OrderBy(p => p.CreatedAt),
                "date_desc" => productsQuery.OrderByDescending(p => p.CreatedAt),
                _ => productsQuery.OrderBy(p => p.Title),
            };

            // Pagination (10 produits par page)
            int pageSize = 10;
            var products = await PaginatedList<Product>.CreateAsync(productsQuery, pageNumber, pageSize);

            // Récupérer les catégories pour le filtre
            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> Create(ProductViewModel productViewModel)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                var product = new Product
                {
                    Title = productViewModel.Title,
                    Description = productViewModel.Description,
                    Price = productViewModel.Price,
                    ImageUrl = productViewModel.ImageUrl,
                    CategoryId = productViewModel.CategoryId,
                    VendorId = user.Id,
                    CreatedAt = DateTime.Now
                };

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", productViewModel.CategoryId);
            return View(productViewModel);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Vérifier que l'utilisateur est le vendeur du produit ou un admin
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (product.VendorId != user.Id && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            var productViewModel = new ProductViewModel
            {
                Id = product.Id,
                Title = product.Title,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CategoryId = product.CategoryId
            };

            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            return View(productViewModel);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> Edit(int id, ProductViewModel productViewModel)
        {
            if (id != productViewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var product = await _context.Products.FindAsync(id);
                    if (product == null)
                    {
                        return NotFound();
                    }

                    // Vérifier que l'utilisateur est le vendeur du produit ou un admin
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                    {
                        return NotFound();
                    }

                    if (product.VendorId != user.Id && !User.IsInRole("Administrator"))
                    {
                        return Forbid();
                    }

                    product.Title = productViewModel.Title;
                    product.Description = productViewModel.Description;
                    product.Price = productViewModel.Price;
                    product.ImageUrl = productViewModel.ImageUrl;
                    product.CategoryId = productViewModel.CategoryId;

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(productViewModel.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", productViewModel.CategoryId);
            return View(productViewModel);
        }

        // GET: Products/Delete/5
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Vérifier que l'utilisateur est le vendeur du produit ou un admin
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (product.VendorId != user.Id && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Vérifier que l'utilisateur est le vendeur du produit ou un admin
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (product.VendorId != user.Id && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Products/ImportFromApi
        [HttpGet]
        [Authorize(Roles = "Vendor,Administrator")]
        public IActionResult ImportFromApi()
        {
            return View();
        }

        // POST: Products/ImportFromApi
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Vendor,Administrator")]
        public async Task<IActionResult> ImportFromApi(string apiUrl)
        {
            if (!string.IsNullOrEmpty(apiUrl))
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                    {
                        return NotFound();
                    }

                    // Importer les produits depuis l'API
                    var products = await _productApiService.GetProductsFromFakeStoreApi(user.Id);
                    if (products.Any())
                    {
                        await _context.Products.AddRangeAsync(products);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = $"{products.Count} produits ont été importés avec succès.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Aucun produit n'a été trouvé ou importé.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de l'importation des produits depuis l'API");
                    TempData["ErrorMessage"] = "Une erreur s'est produite lors de l'importation des produits.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }

    // Classe pour la pagination
   
}