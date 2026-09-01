using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReservaStockPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReservaStock",
                table: "DetallesPedido",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Los pedidos abiertos ya cargados también deben reservar sus productos
            // de stock. Los pedidos por encargo y los entregados quedan fuera.
            migrationBuilder.Sql("""
                WITH reservas AS (
                    SELECT d."IdProducto", SUM(d."Cantidad") AS "Cantidad"
                    FROM "DetallesPedido" AS d
                    INNER JOIN "Pedidos" AS pe ON pe."Id" = d."IdPedido"
                    INNER JOIN "Productos" AS pr ON pr."Id" = d."IdProducto"
                    WHERE NOT pe."Anulado"
                      AND pe."Estado" IN (0, 2)
                      AND NOT pr."PorEncargo"
                    GROUP BY d."IdProducto"
                )
                UPDATE "Productos" AS p
                SET "Stock" = GREATEST(p."Stock" - r."Cantidad", 0),
                    "SinStock" = GREATEST(p."Stock" - r."Cantidad", 0) <= 0
                FROM reservas AS r
                WHERE p."Id" = r."IdProducto";
                """);

            migrationBuilder.Sql("""
                UPDATE "DetallesPedido" AS d
                SET "ReservaStock" = TRUE
                FROM "Pedidos" AS pe, "Productos" AS pr
                WHERE pe."Id" = d."IdPedido"
                  AND pr."Id" = d."IdProducto"
                  AND NOT pe."Anulado"
                  AND pe."Estado" IN (0, 2)
                  AND NOT pr."PorEncargo";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservaStock",
                table: "DetallesPedido");
        }
    }
}
