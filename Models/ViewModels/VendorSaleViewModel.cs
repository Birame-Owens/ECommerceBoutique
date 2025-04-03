// Models/ViewModels/VendorSaleViewModel.cs
using ECommerceBoutique.Models.Entities;
using System;
using System.Collections.Generic;

namespace ECommerceBoutique.Models.ViewModels
{
    public class VendorSaleViewModel
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public decimal VendorTotal { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }
}