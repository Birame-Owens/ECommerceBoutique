
// Models/ViewModels/CheckoutViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ECommerceBoutique.Models.ViewModels
{

    public class CheckoutViewModel
{
    public CartViewModel CartViewModel { get; set; } = new CartViewModel();

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
}
