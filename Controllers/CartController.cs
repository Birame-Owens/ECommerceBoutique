// Controllers/CartController.cs
using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBoutique.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<CartController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var cartViewModel = await GetCartViewModelAsync();
            return View(cartViewModel);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                // Vérifier si le produit existe
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound();
                }

                // Récupérer ou créer le panier
                var cart = await GetCartAsync();

                // Vérifier si le produit est déjà dans le panier
                var cartItem = cart.Items?.FirstOrDefault(i => i.ProductId == productId);

                if (cartItem != null)
                {
                    // Mettre à jour la quantité
                    cartItem.Quantity += quantity;
                }
                else
                {
                    // Ajouter le produit au panier
                    cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = productId,
                        Quantity = quantity,
                        Price = product.Price
                    };

                    // S'assurer que Items est initialisé
                    if (cart.Items == null)
                    {
                        cart.Items = new List<CartItem>();
                    }

                    cart.Items.Add(cartItem);
                }

                cart.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Produit ajouté au panier avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'ajout au panier");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de l'ajout au panier.";
            }

            // Rediriger vers la page précédente ou la liste des produits
            string? returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Products");
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            try
            {
                // Vérifier si l'élément du panier existe
                var cartItem = await _context.CartItems.FindAsync(cartItemId);
                if (cartItem == null)
                {
                    return NotFound();
                }

                // Vérifier que l'utilisateur est bien le propriétaire du panier
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.Id == cartItem.CartId);

                if (cart == null)
                {
                    return NotFound();
                }

                // Si l'utilisateur est connecté, vérifier qu'il est bien le propriétaire du panier
                if (User.Identity?.IsAuthenticated == true)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null || (cart.UserId != null && cart.UserId != user.Id))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    // Pour les utilisateurs non connectés, vérifier avec le cartId de session
                    var sessionCartId = HttpContext.Session.GetString("CartId");
                    if (string.IsNullOrEmpty(sessionCartId) || sessionCartId != cart.Id.ToString())
                    {
                        return Forbid();
                    }
                }

                if (quantity <= 0)
                {
                    // Supprimer l'élément du panier
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    // Mettre à jour la quantité
                    cartItem.Quantity = quantity;
                }

                cart.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Panier mis à jour avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour du panier");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la mise à jour du panier.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            try
            {
                // Vérifier si l'élément du panier existe
                var cartItem = await _context.CartItems.FindAsync(cartItemId);
                if (cartItem == null)
                {
                    return NotFound();
                }

                // Vérifier que l'utilisateur est bien le propriétaire du panier
                var cart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.Id == cartItem.CartId);

                if (cart == null)
                {
                    return NotFound();
                }

                // Si l'utilisateur est connecté, vérifier qu'il est bien le propriétaire du panier
                if (User.Identity?.IsAuthenticated == true)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null || (cart.UserId != null && cart.UserId != user.Id))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    // Pour les utilisateurs non connectés, vérifier avec le cartId de session
                    var sessionCartId = HttpContext.Session.GetString("CartId");
                    if (string.IsNullOrEmpty(sessionCartId) || sessionCartId != cart.Id.ToString())
                    {
                        return Forbid();
                    }
                }

                // Supprimer l'élément du panier
                _context.CartItems.Remove(cartItem);
                cart.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Produit retiré du panier avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression d'un élément du panier");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la suppression du produit du panier.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            try
            {
                var cart = await GetCartAsync();

                if (cart.Items != null && cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                    cart.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Panier vidé avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du vidage du panier");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors du vidage du panier.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cartViewModel = await GetCartViewModelAsync();

            if (cartViewModel.CartItems == null || !cartViewModel.CartItems.Any())
            {
                TempData["ErrorMessage"] = "Votre panier est vide.";
                return RedirectToAction(nameof(Index));
            }

            // Fusionner le panier anonyme avec le panier de l'utilisateur connecté si nécessaire
            await MergeCartsIfNeededAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var checkoutViewModel = new CheckoutViewModel
            {
                CartViewModel = cartViewModel,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Address = user.Address ?? string.Empty,
                City = user.City ?? string.Empty,
                PostalCode = user.PostalCode ?? string.Empty,
                Country = user.Country ?? string.Empty
            };

            return View(checkoutViewModel);
        }

        // Méthodes privées d'aide
        private async Task<Cart> GetCartAsync()
        {
            Cart? cart = null;

            // Si l'utilisateur est connecté, récupérer son panier
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    cart = await _context.Carts
                        .Include(c => c.Items)
                        .FirstOrDefaultAsync(c => c.UserId == user.Id);

                    if (cart == null)
                    {
                        // Créer un nouveau panier pour l'utilisateur
                        cart = new Cart
                        {
                            UserId = user.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            Items = new List<CartItem>()
                        };
                        _context.Carts.Add(cart);
                        await _context.SaveChangesAsync();
                    }

                    // Vérifier s'il y a un panier en session à fusionner
                    await MergeCartsIfNeededAsync();

                    return cart;
                }
            }

            // Pour les utilisateurs non connectés, utiliser un panier basé sur la session
            string? cartId = HttpContext.Session.GetString("CartId");
            if (!string.IsNullOrEmpty(cartId) && int.TryParse(cartId, out int sessionCartId))
            {
                cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.Id == sessionCartId);

                if (cart != null)
                {
                    return cart;
                }
            }

            // Créer un nouveau panier pour la session
            cart = new Cart
            {
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Items = new List<CartItem>()
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Stocker l'ID du panier dans la session
            HttpContext.Session.SetString("CartId", cart.Id.ToString());

            return cart;
        }

        private async Task<CartViewModel> GetCartViewModelAsync()
        {
            var cart = await GetCartAsync();
            var cartItems = new List<CartItemViewModel>();

            if (cart.Items != null)
            {
                foreach (var item in cart.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.Category)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product != null)
                    {
                        cartItems.Add(new CartItemViewModel
                        {
                            CartItemId = item.Id,
                            ProductId = product.Id,
                            ProductName = product.Title,
                            Description = product.Description,
                            CategoryName = product.Category?.Name ?? "Non catégorisé",
                            Price = product.Price,
                            Quantity = item.Quantity,
                            ImageUrl = product.ImageUrl,
                            Total = product.Price * item.Quantity
                        });
                    }
                }
            }

            return new CartViewModel
            {
                CartId = cart.Id,
                CartItems = cartItems,
                TotalItems = cartItems.Sum(i => i.Quantity),
                SubTotal = cartItems.Sum(i => i.Total)
            };
        }

        private async Task MergeCartsIfNeededAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            string? sessionCartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(sessionCartId) || !int.TryParse(sessionCartId, out int cartId))
            {
                return;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
            }

            // Récupérer le panier de session et le panier utilisateur
            var sessionCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            var userCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (sessionCart == null || sessionCart.Items == null || !sessionCart.Items.Any())
            {
                return;
            }

            if (userCart == null)
            {
                // Si l'utilisateur n'a pas de panier, associer simplement le panier de session à l'utilisateur
                sessionCart.UserId = user.Id;
                await _context.SaveChangesAsync();
                return;
            }

            // Fusionner les éléments du panier de session vers le panier utilisateur
            if (sessionCart.Items != null)
            {
                // S'assurer que le panier utilisateur a une collection Items initialisée
                if (userCart.Items == null)
                {
                    userCart.Items = new List<CartItem>();
                }

                foreach (var sessionItem in sessionCart.Items)
                {
                    var userItem = userCart.Items.FirstOrDefault(i => i.ProductId == sessionItem.ProductId);

                    if (userItem != null)
                    {
                        // Mettre à jour la quantité si le produit existe déjà
                        userItem.Quantity += sessionItem.Quantity;
                    }
                    else
                    {
                        // Ajouter le produit au panier utilisateur
                        var newItem = new CartItem
                        {
                            CartId = userCart.Id,
                            ProductId = sessionItem.ProductId,
                            Quantity = sessionItem.Quantity,
                            Price = sessionItem.Price
                        };

                        userCart.Items.Add(newItem);
                    }
                }
            }

            userCart.UpdatedAt = DateTime.Now;

            // Supprimer le panier de session
            _context.Carts.Remove(sessionCart);

            await _context.SaveChangesAsync();

            // Effacer l'ID du panier de session
            HttpContext.Session.Remove("CartId");
        }
    }
}