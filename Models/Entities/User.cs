using Microsoft.AspNetCore.Identity;
using Stripe.Climate;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.Entities
{
    public class User : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool IsVendor { get; set; }
        public virtual ICollection<Order>? Orders { get; set; }
        public virtual ICollection<Product>? Products { get; set; }

        // Nouvelle propriété pour stocker le mot de passe original des utilisateurs API
        public string? OriginalApiPassword { get; set; }
    }
}