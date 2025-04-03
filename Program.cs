using ECommerceBoutique.Data;
using ECommerceBoutique.Models.Entities;
using ECommerceBoutique.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
}).CreateLogger("Program");

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configuration de la base de données
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configuration d'Identity avec User personnalisé
// Dans Program.cs - Assouplir complètement les règles de mot de passe
// Configuration d'Identity avec User personnalisé et règles de mot de passe assouplies
builder.Services.AddIdentity<User, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;         // Ne pas exiger de chiffres
    options.Password.RequiredLength = 3;           // Longueur minimale très courte pour accepter les mots de passe de l'API
    options.Password.RequireNonAlphanumeric = false; // Ne pas exiger de caractères spéciaux
    options.Password.RequireUppercase = false;     // Ne pas exiger de majuscules
    options.Password.RequireLowercase = false;     // Ne pas exiger de minuscules
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
// Configuration des cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Configuration de la session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configuration de Stripe
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// HttpClient pour l'API
builder.Services.AddHttpClient<ProductApiService>();

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Ajout du service HTTP Client
builder.Services.AddHttpClient<UserImportService>();

// Enregistrement du service d'importation d'utilisateurs
builder.Services.AddScoped<UserImportService>();


// Ajout des contrôleurs, des vues et des pages Razor
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Initialisation des données de test
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await DbInitializer.InitializeAsync(services);
        logger.LogInformation("Base de données initialisée avec succès");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Une erreur s'est produite lors de l'initialisation de la base de données");
}

app.Run();