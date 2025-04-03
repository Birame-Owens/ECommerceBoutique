using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ECommerceBoutique.Services
{
    public class UserImportService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserImportService> _logger;

        public UserImportService(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            HttpClient httpClient,
            ILogger<UserImportService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient.BaseAddress = new Uri("https://dummyjson.com/");
        }

        public async Task<(int ImportedCount, int ErrorCount)> ImportUsersAsync()
        {
            int importedCount = 0;
            int errorCount = 0;

            try
            {
                // S'assurer que les rôles requis existent
                string[] requiredRoles = { "Administrator", "Vendor", "Customer" };
                foreach (var roleName in requiredRoles)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(roleName));
                        _logger.LogInformation($"Rôle {roleName} créé");
                    }
                }

                // Récupérer les utilisateurs de l'API
                _logger.LogInformation("Récupération des utilisateurs depuis l'API DummyJSON");
                var response = await _httpClient.GetAsync("users?limit=30");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                // Désérialiser la réponse JSON
                var jsonDocument = JsonDocument.Parse(content);
                var users = jsonDocument.RootElement.GetProperty("users");

                _logger.LogInformation($"Traitement de {users.GetArrayLength()} utilisateurs");

                // Mapper les rôles de l'API vers les rôles de l'application
                var roleMapping = new Dictionary<string, string>
                {
                    { "admin", "Administrator" },
                    { "moderator", "Vendor" },
                    { "user", "Customer" }
                };

                foreach (var userElement in users.EnumerateArray())
                {
                    try
                    {
                        var username = userElement.GetProperty("username").GetString() ?? "user";
                        var email = userElement.GetProperty("email").GetString() ?? $"{username}@dummyjson.com";

                        // Vérifier si l'utilisateur existe déjà
                        var existingUser = await _userManager.FindByEmailAsync(email);
                        if (existingUser != null)
                        {
                            _logger.LogInformation($"L'utilisateur {username} avec l'email {email} existe déjà");
                            continue;
                        }

                        // Extraire les données utilisateur
                        var originalPassword = userElement.GetProperty("password").GetString() ?? "password123";
                        var firstName = userElement.GetProperty("firstName").GetString() ?? "";
                        var lastName = userElement.GetProperty("lastName").GetString() ?? "";
                        var phone = userElement.TryGetProperty("phone", out var phoneProperty) ? phoneProperty.GetString() : "";

                        // Extraire l'adresse
                        var addressProperty = userElement.GetProperty("address");
                        var address = addressProperty.TryGetProperty("address", out var addrProperty) ? addrProperty.GetString() : "";
                        var city = addressProperty.TryGetProperty("city", out var cityProperty) ? cityProperty.GetString() : "";
                        var postalCode = addressProperty.TryGetProperty("postalCode", out var pcProperty) ? pcProperty.GetString() : "";
                        var country = addressProperty.TryGetProperty("country", out var countryProperty) ? countryProperty.GetString() : "";

                        // Déterminer le rôle
                        var apiRole = userElement.TryGetProperty("role", out var roleProperty) ? roleProperty.GetString()?.ToLower() : "user";
                        var applicationRole = (apiRole != null && roleMapping.ContainsKey(apiRole)) ? roleMapping[apiRole] : "Customer";

                        // Marquer l'utilisateur comme vendeur si nécessaire
                        bool isVendor = applicationRole == "Vendor";

                        // Créer le nouvel utilisateur
                        var user = new User
                        {
                            UserName = username,
                            Email = email,
                            FirstName = firstName,
                            LastName = lastName,
                            PhoneNumber = phone,
                            Address = address,
                            City = city,
                            PostalCode = postalCode,
                            Country = country,
                            EmailConfirmed = true,
                            IsVendor = isVendor,
                            // Stocker le mot de passe original pour simplifier les tests
                            OriginalApiPassword = originalPassword
                        };

                        // Créer l'utilisateur avec son mot de passe d'origine
                        _logger.LogInformation($"Tentative de création de l'utilisateur {username} avec email {email} et rôle {applicationRole}");

                        var result = await _userManager.CreateAsync(user, originalPassword);

                        if (result.Succeeded)
                        {
                            // Attribuer le rôle
                            await _userManager.AddToRoleAsync(user, applicationRole);
                            _logger.LogInformation($"Utilisateur {username} créé avec succès et assigné au rôle {applicationRole}");
                            importedCount++;
                        }
                        else
                        {
                            // Si échec avec le mot de passe d'origine, essayer avec un mot de passe renforcé
                            _logger.LogWarning($"Échec avec le mot de passe d'origine pour {username}. Erreurs: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                            // Utiliser un mot de passe prévisible basé sur le nom d'utilisateur
                            var securePassword = $"{username}@123";

                            // Mettre à jour le mot de passe stocké
                            user.OriginalApiPassword = securePassword;

                            var secureResult = await _userManager.CreateAsync(user, securePassword);

                            if (secureResult.Succeeded)
                            {
                                await _userManager.AddToRoleAsync(user, applicationRole);
                                _logger.LogInformation($"Utilisateur {username} créé avec un mot de passe renforcé et assigné au rôle {applicationRole}");
                                importedCount++;
                            }
                            else
                            {
                                _logger.LogError($"Échec définitif pour l'utilisateur {username}. Erreurs: {string.Join(", ", secureResult.Errors.Select(e => e.Description))}");
                                errorCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors du traitement d'un utilisateur");
                        errorCount++;
                    }
                }

                _logger.LogInformation($"Importation terminée: {importedCount} utilisateurs importés, {errorCount} erreurs");
                return (importedCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'importation des utilisateurs");
                throw;
            }
        }

        public async Task<int> DeleteApiUsersAsync()
        {
            try
            {
                _logger.LogInformation("Suppression des utilisateurs de l'API DummyJSON");

                var apiUsers = await _userManager.Users
                    .Where(u => u.Email != null && u.Email.Contains("dummyjson.com"))
                    .ToListAsync();

                _logger.LogInformation($"Trouvé {apiUsers.Count} utilisateurs API à supprimer");

                int deletedCount = 0;
                foreach (var user in apiUsers)
                {
                    try
                    {
                        var result = await _userManager.DeleteAsync(user);
                        if (result.Succeeded)
                        {
                            _logger.LogInformation($"Utilisateur {user.UserName} supprimé avec succès");
                            deletedCount++;
                        }
                        else
                        {
                            _logger.LogWarning($"Échec de la suppression de l'utilisateur {user.UserName}. Erreurs: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erreur lors de la suppression de l'utilisateur {user.UserName}");
                    }
                }

                _logger.LogInformation($"Suppression terminée: {deletedCount} utilisateurs supprimés sur {apiUsers.Count}");
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des utilisateurs API");
                throw;
            }
        }

        // Méthode pour récupérer la liste des utilisateurs API
        public async Task<List<User>> GetApiUsersWithPasswordsAsync()
        {
            try
            {
                var apiUsers = await _userManager.Users
                    .Where(u => u.Email != null && u.Email.Contains("dummyjson.com"))
                    .ToListAsync();

                return apiUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des utilisateurs API");
                return new List<User>();
            }
        }
    }
}