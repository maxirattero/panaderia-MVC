using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Panaderia.Models.Entities;
using Panaderia.MVC.Controllers;
using Panaderia.Services.Interfaces;

// Vista previa aislada: productos ficticios, reloj controlado y ningún acceso a la DB.
static class PreviewHost
{
    public static async Task RunAsync(TestClock clock, IProductoService products, IClienteService clients,
        IPedidoService orders, IPushNotificationService push, IConfiguracionTiendaService settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(TiendaController).Assembly.GetName().Name,
            ContentRootPath = Path.GetFullPath("Panaderia.MVC"), EnvironmentName = "Production"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5079");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(TiendaController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton(products);
        builder.Services.AddSingleton(clients);
        builder.Services.AddSingleton(orders);
        builder.Services.AddSingleton(push);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(Stub.Make<IAccesoTiendaService>((_, _) => Task.FromResult<Cliente?>(null)));
        await using var app = builder.Build();
        app.UseStaticFiles();
        app.MapGet("/preview", () => Results.Content("""
            <h1>Prueba local de disponibilidad semanal</h1>
            <a href="/preview/jueves">Jueves 23:59</a>
            <a href="/preview/viernes">Viernes 00:00</a>
            <a href="/preview/sabado">Sábado 12:00</a>
            <a href="/Tienda/Index">Tienda</a>
            """, "text/html; charset=utf-8"));
        app.MapGet("/preview/{dia}", (string dia) => {
            clock.Now = DateTimeOffset.Parse(dia switch {
                "jueves" => "2026-09-24T23:59:00-03:00",
                "sabado" => "2026-09-26T12:00:00-03:00",
                _ => "2026-09-25T00:00:00-03:00"
            });
            return Results.Redirect("/Tienda/Index");
        });
        app.MapControllerRoute("default", "{controller=Tienda}/{action=Index}/{id?}");
        Console.WriteLine("Local fixture preview: http://127.0.0.1:5079/preview");
        await app.RunAsync();
    }
}
