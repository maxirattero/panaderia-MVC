using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});

string? connectionString;
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host                   = uri.Host,
        Port                   = uri.Port,
        Database               = uri.AbsolutePath.TrimStart('/'),
        Username               = userInfo[0],
        Password               = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        SslMode                = Npgsql.SslMode.Require,
        TrustServerCertificate = true,
        // Supabase pooler (Supavisor) compatibility
        Pooling                = true,
        MaxPoolSize            = 10,
        MaxAutoPrepare         = 0,
        Timeout                = 30,
        CommandTimeout         = 60,
        Multiplexing           = false
    };
    connectionString = npgsqlBuilder.ConnectionString;
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

builder.Services.AddDbContext<PanaderiaContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount   = false;
    options.SignIn.RequireConfirmedEmail     = false;
    options.Password.RequireDigit           = true;
    options.Password.RequireLowercase       = true;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength         = 6;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<PanaderiaContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/Login";
    options.ExpireTimeSpan    = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IReporteCajaService, ReporteCajaService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IFormatoService, FormatoService>();
builder.Services.AddScoped<ITamanoService, TamanoService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IInsumoService, InsumoService>();
builder.Services.AddScoped<IRecetaService, RecetaService>();
builder.Services.AddScoped<ICompraService, CompraService>();
builder.Services.AddScoped<ISubRecetaService, SubRecetaService>();
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<PanaderiaContext>()
    .SetApplicationName("MasaViva");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PanaderiaContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed on startup");
        throw;
    }
}

using (var seedScope = app.Services.CreateScope())
{
    var roleManager = seedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    if (!await roleManager.RoleExistsAsync("Revendedor"))
        await roleManager.CreateAsync(new IdentityRole("Revendedor"));

    var adminEmail    = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                NombreCompleto = "Administrador",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

var invariantCulture = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture =
        new Microsoft.AspNetCore.Localization.RequestCulture(invariantCulture),
    SupportedCultures   = new[] { invariantCulture },
    SupportedUICultures = new[] { invariantCulture }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();