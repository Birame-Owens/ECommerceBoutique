// ViewComponents/CartSummaryViewComponent.cs
using ECommerceBoutique.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommerceBoutique.Models.Entities;

namespace ECommerceBoutique.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CartSummaryViewComponent(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int itemCount = 0;

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(HttpContext.User);
                if (user != null)
                {
                    var cart = await _context.Carts
                        .Include(c => c.Items)
                        .FirstOrDefaultAsync(c => c.UserId == user.Id);

                    if (cart != null && cart.Items != null)
                    {
                        itemCount = cart.Items.Sum(i => i.Quantity);
                    }
                }
            }
            else
            {
                string? cartId = HttpContext.Session.GetString("CartId");
                if (!string.IsNullOrEmpty(cartId) && int.TryParse(cartId, out int id))
                {
                    var cart = await _context.Carts
                        .Include(c => c.Items)
                        .FirstOrDefaultAsync(c => c.Id == id);

                    if (cart != null && cart.Items != null)
                    {
                        itemCount = cart.Items.Sum(i => i.Quantity);
                    }
                }
            }

            return View(itemCount);
        }
    }
}