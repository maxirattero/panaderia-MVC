using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Panaderia.Models.Data;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Implementations;

public static class ReglasCaja
{
    public static async Task<IDbContextTransaction> IniciarAsync(PanaderiaContext db)
    {
        var transaction = await db.Database.BeginTransactionAsync();
        try { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71001, 0)"); return transaction; }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public static void CuentaValida(CuentaCaja cuenta, bool cobro = false)
    {
        if (cuenta != CuentaCaja.Efectivo && cuenta != CuentaCaja.Banco && (!(!cobro && cuenta == CuentaCaja.ReservaMercadoPago)))
            throw new InvalidOperationException(cobro ? "Elegí efectivo o transferencia para el cobro." : "Elegí el origen del dinero.");
    }

    public static void ImporteValido(decimal monto)
    {
        if (monto <= 0 || monto != decimal.Round(monto, 2))
            throw new InvalidOperationException("El importe debe ser positivo y tener hasta dos decimales.");
    }

    public static async Task PeriodoAbiertoAsync(PanaderiaContext db, DateTime fecha)
    {
        if (fecha.Kind != DateTimeKind.Utc || fecha > DateTime.UtcNow.AddMinutes(1))
            throw new InvalidOperationException("La fecha debe ser válida y no puede estar en el futuro.");
        if (await db.CierresCaja.AnyAsync(c => fecha >= c.InicioUtc && fecha < c.FinUtc))
            throw new InvalidOperationException("Ese período ya está cerrado. No se pueden modificar sus operaciones.");
    }

    public static async Task PedidoEditableAsync(PanaderiaContext db, int id)
    {
        var p = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return;
        if (p.Estado == EstadoPedido.Entregado)
            throw new InvalidOperationException("El pedido ya fue entregado. Registrá una devolución si corresponde; no se reescribe la venta.");
        if (await db.ReportesCaja.AnyAsync(r => r.IdPedido == id && r.IdCierre != null))
            throw new InvalidOperationException("El pedido tiene cobros incluidos en un cierre. No se puede modificar su total ni anularlo.");
    }
}
