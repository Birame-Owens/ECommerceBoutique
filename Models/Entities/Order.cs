using Stripe;

namespace ECommerceBoutique.Models.Entities
{
    public class Order
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public virtual User? User { get; set; }

        public decimal Total { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string? PaymentIntentId { get; set; }

        public virtual ICollection<OrderItem>? Items { get; set; }

        public virtual Invoice? Invoice { get; set; }
    }
}