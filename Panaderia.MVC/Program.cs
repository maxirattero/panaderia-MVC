using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<PanaderiaContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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