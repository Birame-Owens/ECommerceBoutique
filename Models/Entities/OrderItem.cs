namespace ECommerceBoutique.Models.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order? Order { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }

        public decimal Price { get; set; }

        public string? VendorId { get; set; }
        public virtual User? Vendor { get; set; }
    }
}