namespace ECommerceBoutique.Models.ViewModels
{
    public class UserRoleViewModel
    {
        // Toutes les propriétés string sont rendues nullables
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsVendor { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsCustomer { get; set; }
        public bool IsApiUser { get; set; }
        public string? OriginalApiPassword { get; set; }

        // Constructeur qui initialise les propriétés
        public UserRoleViewModel()
        {
            // Initialisation avec des valeurs par défaut
            UserId = string.Empty;
            UserName = string.Empty;
            Email = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            OriginalApiPassword = string.Empty;
        }
    }
}