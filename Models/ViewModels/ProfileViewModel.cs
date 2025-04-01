using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Le prénom est requis")]
        [Display(Name = "Prénom")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom est requis")]
        [Display(Name = "Nom")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Adresse email non valide")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Adresse")]
        public string? Address { get; set; }

        [Display(Name = "Ville")]
        public string? City { get; set; }

        [Display(Name = "Code postal")]
        public string? PostalCode { get; set; }

        [Display(Name = "Pays")]
        public string? Country { get; set; }
    }
}
