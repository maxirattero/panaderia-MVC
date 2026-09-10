using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Panaderia.Models.Data;

namespace Panaderia.Models.Migrations;

// Reparación puntual autorizada: compra 7 del 10/09/2026, cinco bolsas, envío $30.000.
// No modifica stock, caja, cantidades, precios del proveedor ni total de la compra.
[DbContext(typeof(PanaderiaContext))]
[Migration("20260910153000_CorregirEnvioCompraHarinasSeptiembre")]
public class CorregirEnvioCompraHarinasSeptiembre : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $repair$
            DECLARE
                cantidad_detalles integer;
                cantidad_coincidencias integer;
                estado_anterior boolean;
                estado_corregido boolean;
            BEGIN
                -- Otras bases (por ejemplo desarrollo) no contienen esta compra.
                IF NOT EXISTS (
                    SELECT 1 FROM "ComprasProveedor" c
                    JOIN "Proveedores" p ON p."Id" = c."IdProveedor"
                    WHERE c."Id" = 7 AND c."Fecha" = TIMESTAMPTZ '2026-09-10 00:00:00+00'
                      AND c."MontoTotal" = 264302.02 AND p."Nombre" = 'Distribuidora 550'
                ) THEN
                    RETURN;
                END IF;

                PERFORM 1 FROM "ComprasProveedor" WHERE "Id" = 7 FOR UPDATE;
                PERFORM 1 FROM "Insumos" WHERE "Id" IN (1, 23, 2, 3) ORDER BY "Id" FOR UPDATE;
                PERFORM 1 FROM "ComprasDetalle" WHERE "IdCompra" = 7 FOR UPDATE;

                SELECT count(*) INTO cantidad_detalles FROM "ComprasDetalle" WHERE "IdCompra" = 7;
                SELECT count(*),
                    bool_and(d."CostoEnvio" = 7500
                        AND d."Subtotal" = e.cantidad * e.precio + 7500
                        AND i."PrecioCompra" = e.precio + 7500 / e.cantidad),
                    bool_and(d."CostoEnvio" = e.cantidad * 6000
                        AND d."Subtotal" = e.cantidad * (e.precio + 6000)
                        AND i."PrecioCompra" = e.precio + 6000)
                INTO cantidad_coincidencias, estado_anterior, estado_corregido
                FROM (VALUES
                    (1, 2::numeric, 40787.12, 25000),
                    (23, 1::numeric, 36687.09, 25000),
                    (2, 1::numeric, 53085.42, 25000),
                    (3, 1::numeric, 62955.27, 20000)
                ) AS e(id, cantidad, precio, rendimiento)
                JOIN "ComprasDetalle" d ON d."IdInsumo" = e.id AND d."IdCompra" = 7
                    AND d."Cantidad" = e.cantidad AND d."PrecioUnitario" = e.precio
                JOIN "Insumos" i ON i."Id" = e.id AND i."CantidadRendimiento" = e.rendimiento;

                IF cantidad_detalles <> 4 OR cantidad_coincidencias <> 4 THEN
                    RAISE EXCEPTION 'La compra 7 cambió: se cancela la corrección del envío.';
                END IF;
                IF estado_corregido THEN
                    RETURN;
                END IF;
                IF NOT estado_anterior OR EXISTS (
                    SELECT 1 FROM "ComprasDetalle" d
                    WHERE d."IdCompra" > 7 AND d."IdInsumo" IN (1, 23, 2, 3)
                ) THEN
                    RAISE EXCEPTION 'Los costos de la compra 7 o sus insumos cambiaron: se cancela la corrección.';
                END IF;

                UPDATE "ComprasDetalle"
                SET "CostoEnvio" = "Cantidad" * 6000,
                    "Subtotal" = "Cantidad" * ("PrecioUnitario" + 6000)
                WHERE "IdCompra" = 7;

                UPDATE "Insumos" i
                SET "PrecioCompra" = d."PrecioUnitario" + 6000,
                    "FechaModificacion" = CURRENT_TIMESTAMP
                FROM "ComprasDetalle" d
                WHERE d."IdCompra" = 7 AND i."Id" = d."IdInsumo";
            END $repair$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Una reversión de código no debe reintroducir costos incorrectos ni pisar compras posteriores.
    }
}
