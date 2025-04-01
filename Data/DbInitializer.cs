// Data/DbInitializer.cs
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBoutique.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var productApiService = scope.ServiceProvider.GetRequiredService<ProductApiService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                // S'assurer que la base de données est créée
                context.Database.EnsureCreated();

                // Créer les rôles s'ils n'existent pas
                string[] roles = { "Administrator", "Customer", "Vendor" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                        logger.LogInformation("Rôle {Role} créé avec succès", role);
                    }
                }

                // Créer les catégories
                if (!context.Categories.Any())
                {
                    var categories = new List<Category>
                    {
                        new Category { Name = "Électronique", Description = "Appareils électroniques, gadgets et accessoires technologiques" },
                        new Category { Name = "Vêtements", Description = "Vêtements pour hommes, femmes et enfants" },
                        new Category { Name = "Maison", Description = "Meubles, décoration et articles pour la maison" },
                        new Category { Name = "Beauté", Description = "Produits de beauté, soins personnels et cosmétiques" },
                        new Category { Name = "Sports", Description = "Équipements sportifs et vêtements de sport" },
                        new Category { Name = "Livres", Description = "Livres, e-books et publications" },
                        new Category { Name = "Jouets", Description = "Jouets et jeux pour enfants de tous âges" },
                        new Category { Name = "Bijoux", Description = "Bijoux et accessoires de mode" }
                    };

                    await context.Categories.AddRangeAsync(categories);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Catégories créées avec succès");
                }

                // Créer l'administrateur s'il n'existe pas
                if (await userManager.FindByEmailAsync("admin@example.com") == null)
                {
                    var adminUser = new User
                    {
                        UserName = "admin@example.com",
                        Email = "admin@example.com",
                        FirstName = "Admin",
                        LastName = "User",
                        EmailConfirmed = true,
                        Address = "123 Admin Street",
                        City = "AdminCity",
                        PostalCode = "12345",
                        Country = "France"
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Administrator");
                        logger.LogInformation("Utilisateur administrateur créé avec succès");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogError("Erreur lors de la création de l'administrateur: {Error}", error.Description);
                        }
                    }
                }

                // Créer un compte vendeur s'il n'existe pas
                if (await userManager.FindByEmailAsync("vendor@example.com") == null)
                {
                    var vendorUser = new User
                    {
                        UserName = "vendor@example.com",
                        Email = "vendor@example.com",
                        FirstName = "Vendor",
                        LastName = "User",
                        EmailConfirmed = true,
                        IsVendor = true,
                        Address = "456 Vendor Avenue",
                        City = "VendorCity",
                        PostalCode = "67890",
                        Country = "France"
                    };

                    var result = await userManager.CreateAsync(vendorUser, "Vendor123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(vendorUser, "Vendor");
                        logger.LogInformation("Utilisateur vendeur créé avec succès");

                        // Importer des produits pour ce vendeur
                        var products = await productApiService.GetProductsFromFakeStoreApi(vendorUser.Id);
                        if (products.Any())
                        {
                            await context.Products.AddRangeAsync(products);
                            await context.SaveChangesAsync();
                            logger.LogInformation("{Count} produits importés avec succès", products.Count);
                        }
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogError("Erreur lors de la création du vendeur: {Error}", error.Description);
                        }
                    }
                }

                // Créer un compte client s'il n'existe pas
                if (await userManager.FindByEmailAsync("client@example.com") == null)
                {
                    var clientUser = new User
                    {
                        UserName = "client@example.com",
                        Email = "client@example.com",
                        FirstName = "Client",
                        LastName = "User",
                        EmailConfirmed = true,
                        Address = "789 Client Boulevard",
                        City = "ClientCity",
                        PostalCode = "54321",
                        Country = "France"
                    };

                    var result = await userManager.CreateAsync(clientUser, "Client123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(clientUser, "Customer");
                        logger.LogInformation("Utilisateur client créé avec succès");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogError("Erreur lors de la création du client: {Error}", error.Description);
                        }
                    }
                }

                // Créer des produits manuellement si nécessaire (en plus de ceux importés par l'API)
                if (!context.Products.Any())
                {
                    // Si l'API n'a pas fonctionné, créer quelques produits manuellement
                    var vendorUser = await userManager.FindByEmailAsync("vendor@example.com");
                    if (vendorUser != null)
                    {
                        var manualProducts = new List<Product>
                        {
                            new Product
                            {
                                Title = "Smartphone XYZ",
                                Description = "Un smartphone dernier cri avec des fonctionnalités avancées",
                                Price = 699.99m,
                                ImageUrl = "https://via.placeholder.com/500x500?text=Smartphone",
                                CategoryId = context.Categories.FirstOrDefault(c => c.Name == "Électronique")?.Id ?? 1,
                                VendorId = vendorUser.Id,
                                CreatedAt = DateTime.Now
                            },
                            new Product
                            {
                                Title = "Laptop Pro",
                                Description = "Un ordinateur portable performant pour les professionnels",
                                Price = 1299.99m,
                                ImageUrl = "https://via.placeholder.com/500x500?text=Laptop",
                                CategoryId = context.Categories.FirstOrDefault(c => c.Name == "Électronique")?.Id ?? 1,
                                VendorId = vendorUser.Id,
                                CreatedAt = DateTime.Now
                            },
                            new Product
                            {
                                Title = "T-shirt coton",
                                Description = "T-shirt en coton de haute qualité, confortable et durable",
                                Price = 24.99m,
                                ImageUrl = "https://via.placeholder.com/500x500?text=T-shirt",
                                CategoryId = context.Categories.FirstOrDefault(c => c.Name == "Vêtements")?.Id ?? 2,
                                VendorId = vendorUser.Id,
                                CreatedAt = DateTime.Now
                            },
                            new Product
                            {
                                Title = "Canapé moderne",
                                Description = "Canapé élégant et confortable pour votre salon",
                                Price = 799.99m,
                                ImageUrl = "https://via.placeholder.com/500x500?text=Canape",
                                CategoryId = context.Categories.FirstOrDefault(c => c.Name == "Maison")?.Id ?? 3,
                                VendorId = vendorUser.Id,
                                CreatedAt = DateTime.Now
                            },
                            new Product
                            {
                                Title = "Crème hydratante",
                                Description = "Crème hydratante pour tous types de peau",
                                Price = 29.99m,
                                ImageUrl = "https://via.placeholder.com/500x500?text=Creme",
                                CategoryId = context.Categories.FirstOrDefault(c => c.Name == "Beauté")?.Id ?? 4,
                                VendorId = vendorUser.Id,
                                CreatedAt = DateTime.Now
                            }
                        };

                        await context.Products.AddRangeAsync(manualProducts);
                        await context.SaveChangesAsync();
                        logger.LogInformation("{Count} produits manuels créés avec succès", manualProducts.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Une erreur s'est produite lors de l'initialisation de la base de données");
            }
        }
    }
}