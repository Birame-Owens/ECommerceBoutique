// Controllers/CheckoutController.cs
using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using StripeSession = Stripe.Checkout.Session;
using StripeSessionService = Stripe.Checkout.SessionService;
using StripeSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using StripeSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using StripeSessionLineItemPriceDataOptions = Stripe.Checkout.SessionLineItemPriceDataOptions;
using StripeSessionLineItemPriceDataProductDataOptions = Stripe.Checkout.SessionLineItemPriceDataProductDataOptions;

namespace ECommerceBoutique.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IConfiguration configuration,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: Checkout
        public IActionResult Index()
        {
            // Rediriger vers le panier si on accède directement
            return RedirectToAction("Checkout", "Cart");
        }

        // POST: Checkout/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(CheckoutViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Rediriger vers le panier avec message d'erreur
                TempData["ErrorMessage"] = "Veuillez remplir tous les champs requis.";
                return RedirectToAction("Checkout", "Cart");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                // Mettre à jour les informations de l'utilisateur si elles ont changé
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Address = model.Address;
                user.City = model.City;
                user.PostalCode = model.PostalCode;
                user.Country = model.Country;

                await _userManager.UpdateAsync(user);

                // Récupérer le panier de l'utilisateur
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart == null || cart.Items == null || !cart.Items.Any())
                {
                    TempData["ErrorMessage"] = "Votre panier est vide.";
                    return RedirectToAction("Index", "Cart");
                }

                // Calculer le montant total
                decimal total = 0;
                foreach (var item in cart.Items)
                {
                    if (item.Product != null)
                    {
                        total += item.Product.Price * item.Quantity;
                    }
                }

                // Créer une session de paiement Stripe
                var options = new StripeSessionCreateOptions
                {
                    PaymentMethodTypes = new List<string>
                    {
                        "card",
                    },
                    LineItems = new List<StripeSessionLineItemOptions>(),
                    Mode = "payment",
                    SuccessUrl = Url.Action("PaymentSuccess", "Checkout", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = Url.Action("PaymentCancel", "Checkout", null, Request.Scheme),
                    CustomerEmail = user.Email,
                    ClientReferenceId = user.Id,
                };

                // Ajouter chaque produit comme ligne de commande
                foreach (var item in cart.Items)
                {
                    if (item.Product != null)
                    {
                        var description = item.Product.Description;
                        if (description != null && description.Length > 100)
                        {
                            description = description.Substring(0, 97) + "...";
                        }

                        options.LineItems.Add(new StripeSessionLineItemOptions
                        {
                            PriceData = new StripeSessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(item.Product.Price * 100), // Stripe utilise les centimes
                                Currency = "eur",
                                ProductData = new StripeSessionLineItemPriceDataProductDataOptions
                                {
                                    Name = item.Product.Title,
                                    Description = description,
                                    Images = !string.IsNullOrEmpty(item.Product.ImageUrl)
                                        ? new List<string> { item.Product.ImageUrl }
                                        : null,
                                },
                            },
                            Quantity = item.Quantity,
                        });
                    }
                }

                var service = new StripeSessionService();
                StripeSession session = await service.CreateAsync(options);

                // Créer une commande en attente
                var order = new Order
                {
                    UserId = user.Id,
                    Total = total,
                    Status = "Pending",
                    OrderDate = DateTime.Now,
                    PaymentIntentId = session.PaymentIntentId,
                    Items = new List<OrderItem>()
                };

                // Ajouter les éléments de commande
                // Ajouter les éléments de commande
                if (cart.Items != null)
                {
                    foreach (var item in cart.Items.ToList())
                    {
                        if (item.Product != null)
                        {
                            order.Items.Add(new OrderItem
                            {
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                Price = item.Product.Price,
                                VendorId = item.Product.VendorId
                            });
                        }
                    }
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Stocker l'ID de commande en session pour le récupérer après le paiement
                HttpContext.Session.SetString("PendingOrderId", order.Id.ToString());

                // Rediriger vers Stripe Checkout
                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement du paiement");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors du traitement du paiement.";
                return RedirectToAction("Checkout", "Cart");
            }
        }

        // GET: Checkout/PaymentSuccess
        public async Task<IActionResult> PaymentSuccess(string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                // Récupérer les détails de la session Stripe
                var sessionService = new StripeSessionService();
                var session = await sessionService.GetAsync(session_id);

                // Récupérer l'ID de commande depuis la session
                string? orderIdString = HttpContext.Session.GetString("PendingOrderId");
                if (string.IsNullOrEmpty(orderIdString) || !int.TryParse(orderIdString, out int orderId))
                {
                    TempData["ErrorMessage"] = "Impossible de retrouver votre commande.";
                    return RedirectToAction("Index", "Home");
                }

                // Mettre à jour la commande
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Commande introuvable.";
                    return RedirectToAction("Index", "Home");
                }

                order.Status = "Paid";

                // Générer la facture
                string invoiceNumber = $"INV-{DateTime.Now.Year}{DateTime.Now.Month:D2}{DateTime.Now.Day:D2}-{order.Id:D5}";
                var invoice = new ECommerceBoutique.Models.Entities.Invoice
                {
                    OrderId = order.Id,
                    InvoiceNumber = invoiceNumber,
                    IssueDate = DateTime.Now,
                    Total = order.Total,
                    IsPaid = true
                };

                _context.Invoices.Add(invoice);

                // Vider le panier
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart != null && cart.Items != null && cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                }

                // Supprimer l'ID de commande de la session
                HttpContext.Session.Remove("PendingOrderId");

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Paiement réussi! Votre commande a été traitée avec succès.";
                return RedirectToAction("Details", "Invoice", new { id = invoice.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement du succès du paiement");
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la finalisation de votre commande.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Checkout/PaymentCancel
        public IActionResult PaymentCancel()
        {
            TempData["WarningMessage"] = "Le paiement a été annulé. Votre commande n'a pas été traitée.";
            return RedirectToAction("Checkout", "Cart");
        }
    }
}