using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    Console.WriteLine($"[DB-DEBUG] Host={npgsqlBuilder.Host}");
    Console.WriteLine($"[DB-DEBUG] Port={npgsqlBuilder.Port}");
    Console.WriteLine($"[DB-DEBUG] Database={npgsqlBuilder.Database}");
    Console.WriteLine($"[DB-DEBUG] Username={npgsqlBuilder.Username}");
    Console.WriteLine($"[DB-DEBUG] Password length={npgsqlBuilder.Password?.Length ?? 0}");
    connectionString = npgsqlBuilder.ConnectionString;
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

builder.Services.AddDbContext<PanaderiaContext>(options =>
    options.UseNpgsql(connectionString));

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
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed on startup");
        throw;
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();