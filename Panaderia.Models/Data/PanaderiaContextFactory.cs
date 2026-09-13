using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Panaderia.Models.Data;

// Crear migraciones no debe arrancar la aplicación ni conectarse a producción.
public class PanaderiaContextFactory : IDesignTimeDbContextFactory<PanaderiaContext>
{
    public PanaderiaContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PanaderiaContext>()
        .UseNpgsql("Host=localhost;Database=masaviva_design;Username=postgres").Options);
}
