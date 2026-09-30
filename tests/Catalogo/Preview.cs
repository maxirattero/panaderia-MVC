using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Panaderia.Models.Data;
using Panaderia.MVC.Controllers;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

static class Preview
{
    public static async Task RunAsync(string connection)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(PedidoController).Assembly.GetName().Name,
            ContentRootPath = Path.GetFullPath("Panaderia.MVC"), EnvironmentName = "Production"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5087");
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PedidoController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization();
        builder.Services.AddDbContext<PanaderiaContext>(o => o.UseNpgsql(connection));
        builder.Services.AddScoped<IPedidoService, PedidoService>();
        builder.Services.AddScoped<IProductoService, ProductoService>();
        builder.Services.AddScoped<IClienteService, ClienteService>();
        builder.Services.AddScoped<IInsumoService, InsumoService>();
        builder.Services.AddScoped<IRecetaService, RecetaService>();
        builder.Services.AddScoped<ISubRecetaService, SubRecetaService>();
        builder.Services.AddScoped<ICategoriaService, CategoriaService>();
        builder.Services.AddScoped<IFormatoService, FormatoService>();
        builder.Services.AddScoped<ITamanoService, TamanoService>();
        builder.Services.AddScoped<IEtiquetaService, EtiquetaService>();
        await using var app = builder.Build();
        app.UseStaticFiles();
        // Identidad exclusiva del servidor de pruebas local, nunca del host real.
        app.Use(async (ctx, next) => {
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "local-admin"), new Claim(ClaimTypes.Role, "Admin")], "Test"));
            await next();
        });
        app.UseRouting();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Pedido}/{action=Create}/{id?}");
        Console.WriteLine("Vista previa local: http://127.0.0.1:5087/Pedido/Create");
        await app.RunAsync();
    }
}
