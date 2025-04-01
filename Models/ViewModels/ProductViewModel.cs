// Models/ViewModels/ProductViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le titre est requis")]
        [Display(Name = "Titre")]
        [StringLength(100, ErrorMessage = "Le titre ne peut pas dépasser 100 caractères")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "La description est requise")]
        [Display(Name = "Description")]
        [StringLength(2000, ErrorMessage = "La description ne peut pas dépasser 2000 caractères")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le prix est requis")]
        [Display(Name = "Prix")]
        [Range(0.01, 10000, ErrorMessage = "Le prix doit être compris entre 0.01 et 10000")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "URL de l'image")]
        [Url(ErrorMessage = "Veuillez entrer une URL valide")]
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "La catégorie est requise")]
        [Display(Name = "Catégorie")]
        public int CategoryId { get; set; }
    }
}