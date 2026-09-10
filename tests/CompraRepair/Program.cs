using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Panaderia.Models.Migrations;

// Requiere COMPRAS_TEST_CONNECTION (PostgreSQL). Solo usa tablas temporales:
// el search_path excluye public y toda la transacción termina con rollback.
await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("COMPRAS_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Configurar COMPRAS_TEST_CONNECTION para ejecutar la prueba."));
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
async Task Execute(string sql) => await new NpgsqlCommand(sql, connection, transaction).ExecuteNonQueryAsync();
async Task<string> Snapshot() => (string)(await new NpgsqlCommand("""
    SELECT jsonb_build_object(
      'compras', (SELECT jsonb_agg(to_jsonb(c) ORDER BY "Id") FROM "ComprasProveedor" c),
      'detalles', (SELECT jsonb_agg(to_jsonb(d) ORDER BY "IdCompra", "IdInsumo") FROM "ComprasDetalle" d),
      'insumos', (SELECT jsonb_agg(to_jsonb(i) ORDER BY "Id") FROM "Insumos" i),
      'caja', (SELECT jsonb_agg(to_jsonb(r)) FROM "ReportesCaja" r))::text
    """, connection, transaction).ExecuteScalarAsync())!;

var sql = new CorregirEnvioCompraHarinasSeptiembre().UpOperations.Cast<SqlOperation>().Single().Sql;
await Execute("""
    SET LOCAL search_path = pg_temp;
    CREATE TEMP TABLE "Proveedores" ("Id" integer, "Nombre" text) ON COMMIT DROP;
    CREATE TEMP TABLE "ComprasProveedor" ("Id" integer, "IdProveedor" integer, "Fecha" timestamptz, "MontoTotal" numeric) ON COMMIT DROP;
    CREATE TEMP TABLE "ComprasDetalle" ("IdCompra" integer, "IdInsumo" integer, "Cantidad" numeric, "PrecioUnitario" numeric, "CostoEnvio" numeric, "Subtotal" numeric) ON COMMIT DROP;
    CREATE TEMP TABLE "Insumos" ("Id" integer, "CantidadRendimiento" numeric, "PrecioCompra" numeric, "StockActual" numeric, "FechaModificacion" timestamptz) ON COMMIT DROP;
    CREATE TEMP TABLE "ReportesCaja" ("Monto" numeric) ON COMMIT DROP;
    INSERT INTO "ReportesCaja" VALUES (264302.02);
    INSERT INTO "Proveedores" VALUES (1, 'Distribuidora 550');
    INSERT INTO "ComprasProveedor" VALUES (7, 1, '2026-09-10 00:00:00+00', 264302.02);
    INSERT INTO "ComprasDetalle" VALUES
      (7, 1, 2, 40787.12, 7500, 89074.24),
      (7, 23, 1, 36687.09, 7500, 44187.09),
      (7, 2, 1, 53085.42, 7500, 60585.42),
      (7, 3, 1, 62955.27, 7500, 70455.27);
    INSERT INTO "Insumos" VALUES
      (1, 25000, 44537.12, 96879.89, NULL),
      (23, 25000, 44187.09, 50000, NULL),
      (2, 25000, 60585.42, 69532.746, NULL),
      (3, 20000, 70455.27, 33950, NULL),
      (99, 1, 123, 456, NULL);
    SAVEPOINT original;
    """);
await Execute(sql);
var valid = (bool)(await new NpgsqlCommand("""
    SELECT
      (SELECT sum("CostoEnvio") = 30000 AND sum("Subtotal") = 264302.02 FROM "ComprasDetalle")
      AND (SELECT "MontoTotal" = 264302.02 FROM "ComprasProveedor" WHERE "Id" = 7)
      AND (SELECT "Monto" = 264302.02 FROM "ReportesCaja")
      AND (SELECT count(*) = 4 FROM "Insumos" i JOIN (VALUES
        (1, 46787.12, 96879.89), (23, 42687.09, 50000),
        (2, 59085.42, 69532.746), (3, 68955.27, 33950)
      ) e(id, precio, stock) ON i."Id" = e.id AND i."PrecioCompra" = e.precio AND i."StockActual" = e.stock)
      AND (SELECT "PrecioCompra" = 123 AND "StockActual" = 456 FROM "Insumos" WHERE "Id" = 99)
    """, connection, transaction).ExecuteScalarAsync())!;
if (!valid) throw new Exception("Importes, stock o datos ajenos incorrectos.");
Console.WriteLine("PASS: corrige los cuatro costos y conserva total, stock, caja y otros insumos.");
var corrected = await Snapshot();
await Execute(sql);
if (corrected != await Snapshot()) throw new Exception("La reparación no es idempotente.");
Console.WriteLine("PASS: repetir la reparación no altera los datos.");

foreach (var change in new[] {
    "UPDATE \"Insumos\" SET \"PrecioCompra\" = 1 WHERE \"Id\" = 1",
    "DELETE FROM \"ComprasDetalle\" WHERE \"IdInsumo\" = 23",
    "INSERT INTO \"ComprasDetalle\" VALUES (8, 1, 1, 50000, 0, 50000)"
})
{
    await Execute("ROLLBACK TO SAVEPOINT original");
    await Execute(change);
    var before = await Snapshot();
    await Execute("SAVEPOINT conflict");
    try { await Execute(sql); throw new Exception("La reparación aceptó datos modificados."); }
    catch (PostgresException ex) when (ex.SqlState == "P0001") { }
    await Execute("ROLLBACK TO SAVEPOINT conflict");
    if (before != await Snapshot()) throw new Exception("Se modificaron datos en un conflicto.");
}
Console.WriteLine("PASS: rechaza cambios de costos, renglones faltantes y compras posteriores.");
await Execute("ROLLBACK TO SAVEPOINT original; DELETE FROM \"ComprasProveedor\" WHERE \"Id\" = 7");
var absent = await Snapshot();
await Execute(sql);
if (absent != await Snapshot()) throw new Exception("Se modificó otra base sin la compra objetivo.");
Console.WriteLine("PASS: no modifica bases sin la compra objetivo.");
await transaction.RollbackAsync();
