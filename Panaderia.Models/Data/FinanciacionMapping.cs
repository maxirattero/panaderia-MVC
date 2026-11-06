using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Entities;

namespace Panaderia.Models.Data;

internal static class FinanciacionMapping
{
    public static void Configurar(ModelBuilder model)
    {
        model.Entity<CompraProveedor>().HasIndex(x => x.ClaveOperacion).IsUnique();
        model.Entity<CompraProveedor>().HasMany(x => x.Aportes).WithOne(x => x.Compra).HasForeignKey(x => x.IdCompra).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AporteCompra>(e => {
            e.HasIndex(x => new { x.IdCompra, x.Origen }).IsUnique();
            e.Property(x => x.Monto).HasPrecision(18, 4);
            e.ToTable("AportesCompra", t => t.HasCheckConstraint("CK_Aporte_ImporteOrigen", "\"Monto\" > 0 AND ((\"Origen\" = 0 AND \"Cuenta\" IS NOT NULL AND \"Cuenta\" IN (1,2,3)) OR (\"Origen\" IN (1,2) AND \"Cuenta\" IS NULL))"));
        });
        model.Entity<LoteFinanciado>(e => {
            e.Property(x => x.Nombre).HasMaxLength(120);
            e.HasIndex(x => x.ClaveOperacion).IsUnique();
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("LotesFinanciados", t => t.HasCheckConstraint("CK_Lote_Unidades", "\"Unidades\" > 0"));
        });
        model.Entity<AsignacionCompraLote>(e => {
            e.Property(x => x.Monto).HasPrecision(18, 4);
            e.HasIndex(x => x.ClaveOperacion).IsUnique();
            e.HasIndex(x => new { x.IdCompra, x.IdLote }).IsUnique();
            e.HasOne(x => x.Compra).WithMany().HasForeignKey(x => x.IdCompra).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lote).WithMany(x => x.Asignaciones).HasForeignKey(x => x.IdLote).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("AsignacionesCompraLote", t => t.HasCheckConstraint("CK_Asignacion_Monto", "\"Monto\" > 0"));
        });
        model.Entity<VentaLote>(e => {
            e.HasQueryFilter(x => !x.DetallePedido.Pedido.Anulado);
            e.HasIndex(x => new { x.IdLote, x.IdDetallePedido }).IsUnique();
            e.HasOne(x => x.Lote).WithMany().HasForeignKey(x => x.IdLote).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DetallePedido).WithMany().HasForeignKey(x => x.IdDetallePedido).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cierre).WithMany().HasForeignKey(x => x.IdCierre).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("VentasLote", t => t.HasCheckConstraint("CK_VentaLote_Unidades", "\"Unidades\" > 0"));
        });
        model.Entity<ReservaReintegro>(e => {
            e.Property(x => x.Monto).HasPrecision(18, 4);
            e.HasIndex(x => new { x.IdAporte, x.IdCierre }).IsUnique();
            e.HasOne(x => x.Aporte).WithMany().HasForeignKey(x => x.IdAporte).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cierre).WithMany().HasForeignKey(x => x.IdCierre).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("ReservasReintegro", t => t.HasCheckConstraint("CK_ReservaReintegro_Monto", "\"Monto\" > 0"));
        });
        model.Entity<PagoReintegro>(e => {
            e.Property(x => x.Monto).HasPrecision(18, 4);
            e.HasIndex(x => x.IdMovimiento).IsUnique();
            e.HasOne(x => x.Aporte).WithMany().HasForeignKey(x => x.IdAporte).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reserva).WithMany().HasForeignKey(x => x.IdReserva).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Movimiento).WithMany().HasForeignKey(x => x.IdMovimiento).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("PagosReintegro", t => t.HasCheckConstraint("CK_PagoReintegro_Monto", "\"Monto\" > 0"));
        });
        model.Entity<CierreCaja>().Property(x => x.ReintegrosProvisionados).HasPrecision(18, 4);
        model.Entity<CierreCaja>().Property(x => x.ReintegrosGenerados).HasPrecision(18, 4);
        model.Entity<CierreCaja>().Property(x => x.AdelantosPersonales).HasPrecision(18, 4);
    }
}
