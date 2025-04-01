using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.Entities
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public virtual ICollection<Product>? Products { get; set; }
    }
}