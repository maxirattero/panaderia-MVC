using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Entities;

namespace Panaderia.Models.Data
{
    public class PanaderiaContext : DbContext
    {
        public PanaderiaContext(DbContextOptions<PanaderiaContext> options) : base(options)
        {
        }

        // DbSets - una por cada entidad
        public DbSet<CategoriaProducto> CategoriasProducto { get; set; }
        public DbSet<Tamano> Tamanos { get; set; }
        public DbSet<Formato> Formatos { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Producto> Productos { get; set; }        
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallePedido> DetallesPedido { get; set; }
        public DbSet<ReporteCaja> ReportesCaja { get; set; }    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configuración de relaciones y restricciones            
            // Producto -> CategoriaProducto
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => p.IdCategoria);
            // Producto -> Tamano (opcional)
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Tamano)
                .WithMany()
                .HasForeignKey(p => p.IdTamano);
            // Producto -> Formato (opcional)
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Formato)
                .WithMany()
                .HasForeignKey(p => p.IdFormato);
            // Pedido -> Cliente
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.IdCliente);
            // DetallePedido -> Pedido
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Pedido)
                .WithMany(p => p.Detalles)
                .HasForeignKey(d => d.IdPedido);
            // DetallePedido -> Producto
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.IdProducto);
            // ReporteCaja -> Pedido (opcional)
            modelBuilder.Entity<ReporteCaja>()
                .HasOne(r => r.Pedido)
                .WithMany(p => p.Reportes)
                .HasForeignKey(r => r.IdPedido);
            // reporteCaja -> Proveedor (opcional)
            modelBuilder.Entity<ReporteCaja>()
                .HasOne(r => r.Proveedor)
                .WithMany()
                .HasForeignKey(r => r.IdProveedor);
            //filtro global consulta - soft delete
            modelBuilder.Entity<Pedido>().HasQueryFilter(p => !p.Anulado);            
            modelBuilder.Entity<DetallePedido>().HasQueryFilter(d => !d.Pedido.Anulado);
        }
    }
}