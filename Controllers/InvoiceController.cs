// Controllers/InvoiceController.cs
using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ECommerceBoutique.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<InvoiceController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Invoice
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            List<InvoiceViewModel> invoices = new List<InvoiceViewModel>();

            // Vérifier si l'utilisateur est un vendeur
            if (user.IsVendor)
            {
                // Récupérer toutes les commandes contenant des produits du vendeur
                var orderIds = await _context.OrderItems
                    .Where(oi => oi.VendorId == user.Id)
                    .Select(oi => oi.OrderId)
                    .Distinct()
                    .ToListAsync();

                // Récupérer les factures associées à ces commandes
                var vendorInvoices = await _context.Invoices
                    .Where(i => orderIds.Contains(i.OrderId))
                    .OrderByDescending(i => i.IssueDate)
                    .ToListAsync();

                foreach (var invoice in vendorInvoices)
                {
                    var order = await _context.Orders.FindAsync(invoice.OrderId);
                    if (order != null)
                    {
                        // Calculer le montant total pour ce vendeur seulement
                        var vendorTotal = await _context.OrderItems
                            .Where(oi => oi.OrderId == order.Id && oi.VendorId == user.Id)
                            .SumAsync(oi => oi.Price * oi.Quantity);

                        var customer = order.UserId != null
                            ? await _userManager.FindByIdAsync(order.UserId)
                            : null;

                        invoices.Add(new InvoiceViewModel
                        {
                            Id = invoice.Id,
                            InvoiceNumber = invoice.InvoiceNumber,
                            IssueDate = invoice.IssueDate,
                            OrderDate = order.OrderDate,
                            Total = vendorTotal,
                            Status = invoice.IsPaid ? "Payée" : "En attente",
                            CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}" : "Client inconnu"
                        });
                    }
                }
            }
            else
            {
                // Récupérer toutes les factures de l'utilisateur client
                var customerInvoices = await _context.Invoices
                    .Join(_context.Orders,
                          i => i.OrderId,
                          o => o.Id,
                          (i, o) => new { Invoice = i, Order = o })
                    .Where(io => io.Order.UserId == user.Id)
                    .OrderByDescending(io => io.Invoice.IssueDate)
                    .Select(io => io.Invoice)
                    .ToListAsync();

                foreach (var invoice in customerInvoices)
                {
                    var order = await _context.Orders.FindAsync(invoice.OrderId);
                    if (order != null)
                    {
                        invoices.Add(new InvoiceViewModel
                        {
                            Id = invoice.Id,
                            InvoiceNumber = invoice.InvoiceNumber,
                            IssueDate = invoice.IssueDate,
                            OrderDate = order.OrderDate,
                            Total = invoice.Total,
                            Status = invoice.IsPaid ? "Payée" : "En attente"
                        });
                    }
                }
            }

            ViewBag.IsVendor = user.IsVendor;

            // Calculer le total des ventes pour les vendeurs ou des achats pour les clients
            decimal totalAmount = invoices.Sum(i => i.Total);
            ViewBag.TotalAmount = totalAmount;

            return View(invoices);
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == invoice.OrderId);

            if (order == null)
            {
                return NotFound();
            }

            // Vérifier que l'utilisateur a le droit de voir cette facture
            bool hasAccess = false;

            // Si l'utilisateur est le client qui a passé la commande
            if (order.UserId == user.Id)
            {
                hasAccess = true;
            }
            // Si l'utilisateur est un vendeur qui a vendu des produits dans cette commande
            else if (user.IsVendor && order.Items != null)
            {
                bool isVendorOfAnyItem = order.Items.Any(i => i.VendorId == user.Id);
                hasAccess = isVendorOfAnyItem;
            }

            if (!hasAccess)
            {
                return Forbid();
            }

            User? customer = null;
            if (order.UserId != null)
            {
                customer = await _userManager.FindByIdAsync(order.UserId);
            }

            var detailViewModel = new InvoiceDetailViewModel
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                IssueDate = invoice.IssueDate,
                OrderDate = order.OrderDate,
                Total = invoice.Total,
                Status = invoice.IsPaid ? "Payée" : "En attente",
                CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}" : "Client inconnu",
                CustomerEmail = customer?.Email ?? "Email inconnu",
                CustomerAddress = $"{customer?.Address}, {customer?.City}, {customer?.PostalCode}, {customer?.Country}",
                Items = new List<InvoiceItemViewModel>()
            };

            // Filtrer les articles pour un vendeur, montrer tous pour un client
            // Filtrer les articles pour un vendeur, montrer tous pour un client
            if (order.Items != null)
            {
                var orderItems = order.Items.ToList();
                foreach (var item in orderItems)
                {
                    // Pour un vendeur, ne montrer que ses produits
                    if (!user.IsVendor || item.VendorId == user.Id)
                    {
                        detailViewModel.Items.Add(new InvoiceItemViewModel
                        {
                            ProductName = item.Product?.Title ?? "Produit inconnu",
                            Quantity = item.Quantity,
                            UnitPrice = item.Price,
                            Total = item.Price * item.Quantity
                        });
                    }
                }
            }

            // Pour un vendeur, recalculer le total basé sur ses produits seulement
            if (user.IsVendor)
            {
                detailViewModel.Total = detailViewModel.Items.Sum(i => i.Total);
            }

            return View(detailViewModel);
        }

        // GET: Invoice/Download/5
        public async Task<IActionResult> Download(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == invoice.OrderId);

            if (order == null)
            {
                return NotFound();
            }

            // Vérifier que l'utilisateur a le droit de voir cette facture
            bool hasAccess = false;

            // Si l'utilisateur est le client qui a passé la commande
            if (order.UserId == user.Id)
            {
                hasAccess = true;
            }
            // Si l'utilisateur est un vendeur qui a vendu des produits dans cette commande
            // Si l'utilisateur est un vendeur qui a vendu des produits dans cette commande
            else if (user.IsVendor && order.Items != null)
            {
                var orderItems = order.Items.ToList();
                bool isVendorOfAnyItem = orderItems.Any(i => i.VendorId == user.Id);
                hasAccess = isVendorOfAnyItem;
            }

            if (!hasAccess)
            {
                return Forbid();
            }

            User? customer = null;
            if (order.UserId != null)
            {
                customer = await _userManager.FindByIdAsync(order.UserId);
            }

            // Générer le contenu PDF (ici simulé avec du texte)
            StringBuilder invoiceContent = new StringBuilder();
            invoiceContent.AppendLine($"FACTURE #{invoice.InvoiceNumber}");
            invoiceContent.AppendLine("---------------------------------");
            invoiceContent.AppendLine($"Date d'émission: {invoice.IssueDate:dd/MM/yyyy}");
            invoiceContent.AppendLine($"Date de commande: {order.OrderDate:dd/MM/yyyy}");
            invoiceContent.AppendLine();

            if (customer != null)
            {
                invoiceContent.AppendLine("CLIENT:");
                invoiceContent.AppendLine($"{customer.FirstName} {customer.LastName}");
                invoiceContent.AppendLine(customer.Email ?? "");
                invoiceContent.AppendLine(customer.Address ?? "");
                invoiceContent.AppendLine($"{customer.City ?? ""}, {customer.PostalCode ?? ""}");
                invoiceContent.AppendLine(customer.Country ?? "");
            }

            invoiceContent.AppendLine();
            invoiceContent.AppendLine("ARTICLES:");
            invoiceContent.AppendLine("---------------------------------");

            decimal total = 0;

            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    // Pour un vendeur, ne montrer que ses produits
                    if (!user.IsVendor || item.VendorId == user.Id)
                    {
                        string productName = item.Product?.Title ?? "Produit inconnu";
                        decimal lineTotal = item.Price * item.Quantity;
                        invoiceContent.AppendLine($"{item.Quantity} x {productName} - {item.Price:C} = {lineTotal:C}");
                        total += lineTotal;
                    }
                }
            }

            invoiceContent.AppendLine("---------------------------------");
            invoiceContent.AppendLine($"TOTAL: {total:C}");
            invoiceContent.AppendLine();
            invoiceContent.AppendLine($"STATUT: {(invoice.IsPaid ? "Payée" : "En attente")}");

            // Dans un environnement réel, vous utiliseriez une bibliothèque comme iTextSharp ou DinkToPdf
            // pour générer un vrai PDF. Ici nous simulons avec un fichier texte.
            byte[] fileBytes = Encoding.UTF8.GetBytes(invoiceContent.ToString());

            string fileName = $"Facture_{invoice.InvoiceNumber}.txt";

            return File(fileBytes, "text/plain", fileName);
        }
    }
}