using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Entities;

namespace Panaderia.Models.Data
{
    public class PanaderiaContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public PanaderiaContext(DbContextOptions<PanaderiaContext> options) : base(options)
        {
        }

        // DbSets - una por cada entidad
        public DbSet<ConfiguracionTienda> ConfiguracionTienda { get; set; }
        public DbSet<CategoriaProducto> CategoriasProducto { get; set; }
        public DbSet<Tamano> Tamanos { get; set; }
        public DbSet<Formato> Formatos { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Producto> Productos { get; set; }        
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallePedido> DetallesPedido { get; set; }
        public DbSet<ReporteCaja> ReportesCaja { get; set; }
        public DbSet<ConfiguracionCaja> ConfiguracionCaja { get; set; }
        public DbSet<CierreCaja> CierresCaja { get; set; }
        public DbSet<CierreCajaCosto> CierresCajaCostos { get; set; }
        public DbSet<CierreCajaSaldo> CierresCajaSaldos { get; set; }
        public DbSet<Insumo> Insumos { get; set; }
        public DbSet<UnidadCompra> UnidadesCompra { get; set; }
        public DbSet<CompraProveedor> ComprasProveedor { get; set; }
        public DbSet<CompraDetalle> ComprasDetalle { get; set; }
        public DbSet<Receta> Recetas { get; set; }
        public DbSet<RecetaDetalle> RecetaDetalles { get; set; }
        public DbSet<SubReceta> SubRecetas { get; set; }
        public DbSet<SubRecetaDetalle> SubRecetaDetalles { get; set; }
        public DbSet<ProduccionStock> ProduccionStock { get; set; }
        public DbSet<ProductoImagen> ProductoImagenes { get; set; }
        public DbSet<Etiqueta> Etiquetas { get; set; }
        public DbSet<ProductoEtiqueta> ProductoEtiquetas { get; set; }
        public DbSet<SuscripcionPush> SuscripcionesPush { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Mantener las longitudes de Identity tanto en el host MVC como al generar migraciones.
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().Property(x => x.ProviderKey).HasMaxLength(128);
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().Property(x => x.Name).HasMaxLength(128);
            modelBuilder.Entity<ConfiguracionCaja>(e =>
            {
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.PorcentajeReserva).HasPrecision(5, 2);
                e.ToTable("ConfiguracionCaja", t => {
                    t.HasCheckConstraint("CK_ConfiguracionCaja_Id", "\"Id\" = 1");
                    t.HasCheckConstraint("CK_ConfiguracionCaja_Porcentaje", "\"PorcentajeReserva\" BETWEEN 0 AND 100");
                });
                e.HasData(new ConfiguracionCaja { Id = 1, PorcentajeReserva = 30m });
            });
            modelBuilder.Entity<CierreCaja>(e =>
            {
                e.HasIndex(x => x.InicioSemana).IsUnique().HasFilter("NOT \"EsHistorico\"");
                e.HasIndex(x => x.IdMovimientoHistorico).IsUnique();
                e.HasMany(x => x.Costos).WithOne(x => x.Cierre).HasForeignKey(x => x.IdCierre).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Saldos).WithOne(x => x.Cierre).HasForeignKey(x => x.IdCierre).OnDelete(DeleteBehavior.Restrict);
                e.Property(x => x.PorcentajeReserva).HasPrecision(5, 2);
            });
            modelBuilder.Entity<ReporteCaja>(e =>
            {
                e.HasOne(x => x.Compra).WithMany().HasForeignKey(x => x.IdCompra).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Cierre).WithMany().HasForeignKey(x => x.IdCierre).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CierreDestino).WithMany().HasForeignKey(x => x.IdCierreDestino).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.Cuenta, x.Fecha });
                e.HasIndex(x => x.Fecha);
                e.HasIndex(x => x.ClaveOperacion).IsUnique();
                e.HasIndex(x => new { x.IdTransferencia, x.Tipo }).IsUnique();
            });
            modelBuilder.Entity<DetallePedido>().Property(x => x.CostoIngredientes).HasPrecision(18, 4);
            modelBuilder.Entity<Pedido>().HasIndex(x => new { x.FechaEntregaReal, x.Estado });
            modelBuilder.Entity<CierreCajaCosto>().Property(x => x.Ingredientes).HasPrecision(18, 4);
            modelBuilder.Entity<CierreCajaCosto>().Property(x => x.Empaque).HasPrecision(18, 4);
            modelBuilder.Entity<ApplicationUser>()
                .HasOne<Cliente>().WithMany().HasForeignKey(u => u.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ApplicationUser>().HasIndex(u => u.IdCliente).IsUnique();
            modelBuilder.Entity<ConfiguracionTienda>(entity =>
            {
                entity.Property(c => c.Id).ValueGeneratedNever();
                entity.Property(c => c.MontoMinimoPedido).HasPrecision(18, 2);
                entity.ToTable("ConfiguracionTienda", table =>
                {
                    table.HasCheckConstraint("CK_ConfiguracionTienda_Unica", "\"Id\" = 1");
                    table.HasCheckConstraint("CK_ConfiguracionTienda_Minimo", "\"MontoMinimoPedido\" >= 0");
                });
                entity.HasData(new ConfiguracionTienda { Id = 1, RetiroHabilitado = true, MontoMinimoPedido = 0m });
            });
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
            // Descuento habitual del cliente (0 a 100, dos decimales, nullable)
            modelBuilder.Entity<Cliente>()
                .Property(c => c.DescuentoPorcentaje)
                .HasPrecision(5, 2);
            // Descuento del pedido (0 a 100, dos decimales)
            modelBuilder.Entity<Pedido>()
                .Property(p => p.DescuentoPorcentaje)
                .HasPrecision(5, 2);
            // DetallePedido -> Pedido
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Pedido)
                .WithMany(p => p.Detalles)
                .HasForeignKey(d => d.IdPedido);
            // DetallePedido -> Producto (restrict)
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
            // DetallePedido -> Insumo (Empaque, restrict, nullable)
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Empaque)
                .WithMany()
                .HasForeignKey(d => d.IdEmpaque)
                .OnDelete(DeleteBehavior.Restrict);
            // CostoEmpaque precision
            modelBuilder.Entity<DetallePedido>()
                .Property(d => d.CostoEmpaque)
                .HasPrecision(18, 4);
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
            // Insumo -> Proveedor (opcional)
            modelBuilder.Entity<Insumo>()
                .HasOne(i => i.Proveedor)
                .WithMany()
                .HasForeignKey(i => i.IdProveedor)
                .OnDelete(DeleteBehavior.SetNull);
            // UnidadCompra -> Insumo (cascade)
            modelBuilder.Entity<UnidadCompra>()
                .HasOne(u => u.Insumo)
                .WithMany(i => i.UnidadesCompra)
                .HasForeignKey(u => u.IdInsumo)
                .OnDelete(DeleteBehavior.Cascade);
            // Receta -> Producto (índice único: un producto, una receta)
            modelBuilder.Entity<Receta>()
                .HasIndex(r => r.IdProducto)
                .IsUnique();
            modelBuilder.Entity<Receta>()
                .HasOne(r => r.Producto)
                .WithMany()
                .HasForeignKey(r => r.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
            // RecetaDetalle -> Receta (cascade)
            modelBuilder.Entity<RecetaDetalle>()
                .HasOne(d => d.Receta)
                .WithMany(r => r.Detalles)
                .HasForeignKey(d => d.IdReceta)
                .OnDelete(DeleteBehavior.Cascade);
            // RecetaDetalle -> Insumo (restrict)
            modelBuilder.Entity<RecetaDetalle>()
                .HasOne(d => d.Insumo)
                .WithMany()
                .HasForeignKey(d => d.IdInsumo)
                .OnDelete(DeleteBehavior.Restrict);
            // RecetaDetalle -> SubReceta (restrict, nullable)
            modelBuilder.Entity<RecetaDetalle>()
                .HasOne(d => d.SubReceta)
                .WithMany()
                .HasForeignKey(d => d.IdSubReceta)
                .OnDelete(DeleteBehavior.Restrict);
            // SubRecetaDetalle -> SubReceta (cascade)
            modelBuilder.Entity<SubRecetaDetalle>()
                .HasOne(d => d.SubReceta)
                .WithMany(s => s.Detalles)
                .HasForeignKey(d => d.IdSubReceta)
                .OnDelete(DeleteBehavior.Cascade);
            // SubRecetaDetalle -> Insumo (restrict)
            modelBuilder.Entity<SubRecetaDetalle>()
                .HasOne(d => d.Insumo)
                .WithMany()
                .HasForeignKey(d => d.IdInsumo)
                .OnDelete(DeleteBehavior.Restrict);
            // CompraProveedor -> Proveedor (restrict)
            modelBuilder.Entity<CompraProveedor>()
                .HasOne(c => c.Proveedor)
                .WithMany()
                .HasForeignKey(c => c.IdProveedor)
                .OnDelete(DeleteBehavior.Restrict);
            // CompraDetalle -> CompraProveedor (cascade)
            modelBuilder.Entity<CompraDetalle>()
                .HasOne(d => d.Compra)
                .WithMany(c => c.Detalles)
                .HasForeignKey(d => d.IdCompra)
                .OnDelete(DeleteBehavior.Cascade);
            // CompraDetalle -> Insumo (restrict)
            modelBuilder.Entity<CompraDetalle>()
                .HasOne(d => d.Insumo)
                .WithMany()
                .HasForeignKey(d => d.IdInsumo)
                .OnDelete(DeleteBehavior.Restrict);
            // CompraDetalle -> UnidadCompra (restrict)
            modelBuilder.Entity<CompraDetalle>()
                .HasOne(d => d.UnidadCompra)
                .WithMany()
                .HasForeignKey(d => d.IdUnidadCompra)
                .OnDelete(DeleteBehavior.Restrict);
            // ProductoImagen -> Producto (cascade: la imagen muere con el producto; índice único: un producto, una imagen)
            modelBuilder.Entity<ProductoImagen>()
                .HasIndex(i => i.IdProducto)
                .IsUnique();
            modelBuilder.Entity<ProductoImagen>()
                .HasOne(i => i.Producto)
                .WithMany()
                .HasForeignKey(i => i.IdProducto)
                .OnDelete(DeleteBehavior.Cascade);
            // Etiqueta: nombre único (evita duplicados tipo "Vegano" / "vegano")
            modelBuilder.Entity<Etiqueta>()
                .HasIndex(e => e.Nombre)
                .IsUnique();
            // ProductoEtiqueta: clave compuesta, ambas FKs en cascade (la asignación no guarda historial)
            modelBuilder.Entity<ProductoEtiqueta>()
                .HasKey(pe => new { pe.IdProducto, pe.IdEtiqueta });
            modelBuilder.Entity<ProductoEtiqueta>()
                .HasOne(pe => pe.Producto)
                .WithMany(p => p.ProductoEtiquetas)
                .HasForeignKey(pe => pe.IdProducto)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ProductoEtiqueta>()
                .HasOne(pe => pe.Etiqueta)
                .WithMany()
                .HasForeignKey(pe => pe.IdEtiqueta)
                .OnDelete(DeleteBehavior.Cascade);
            // Suscripción de cada dispositivo administrador a notificaciones web push.
            modelBuilder.Entity<SuscripcionPush>()
                .HasIndex(s => s.Endpoint)
                .IsUnique();
            modelBuilder.Entity<SuscripcionPush>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            //filtro global consulta - soft delete
            modelBuilder.Entity<Pedido>().HasQueryFilter(p => !p.Anulado);
            modelBuilder.Entity<DetallePedido>().HasQueryFilter(d => !d.Pedido.Anulado);
        }
    }
}
