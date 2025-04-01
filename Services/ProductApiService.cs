// Services/ProductApiService.cs
using ECommerceBoutique.Models.Entities;
using System.Text.Json;

namespace ECommerceBoutique.Services
{
    public class ProductApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductApiService> _logger;

        public ProductApiService(HttpClient httpClient, ILogger<ProductApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<Product>> GetProductsFromFakeStoreApi(string vendorId)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://fakestoreapi.com/products");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var apiProducts = JsonSerializer.Deserialize<List<FakeStoreProduct>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiProducts == null)
                    return new List<Product>();

                // Mapper les produits API vers nos modèles
                return apiProducts.Select(p => new Product
                {
                    Title = p.Title,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.Image,
                    CategoryId = GetCategoryId(p.Category),
                    VendorId = vendorId,
                    CreatedAt = DateTime.Now
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des produits depuis FakeStoreAPI");
                return new List<Product>();
            }
        }

        private int GetCategoryId(string categoryName)
        {
            // Simplification: assigner des ID en fonction du nom de catégorie
            return categoryName.ToLower() switch
            {
                "electronics" => 1,
                "jewelery" => 2,
                "men's clothing" => 3,
                "women's clothing" => 4,
                _ => 1
            };
        }

        // Classe pour désérialiser les produits de l'API
        private class FakeStoreProduct
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Image { get; set; } = string.Empty;
        }
    }
}