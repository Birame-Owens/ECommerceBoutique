namespace ECommerceBoutique.Models.Entities
{
    public class Cart
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public virtual User? User { get; set; }

        public virtual ICollection<CartItem>? Items { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}