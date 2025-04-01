using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.Entities
{
    public class Invoice
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order? Order { get; set; }

        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; } = DateTime.Now;

        public decimal Total { get; set; }

        public bool IsPaid { get; set; }
    }
}