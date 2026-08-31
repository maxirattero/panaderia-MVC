using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTotalVendidoAlCierre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalVendidoInformativo",
                table: "ReportesCaja",
                type: "numeric",
                nullable: true);

            // Completa los cierres existentes con las ventas de su período y congela el dato.
            migrationBuilder.Sql("""
                UPDATE "ReportesCaja" AS r
                SET "TotalVendidoInformativo" = COALESCE((
                    SELECT SUM(p."MontoTotal")
                    FROM "Pedidos" AS p
                    WHERE p."FechaEntrega" >= r."FechaInicioPeriodo"
                      AND p."FechaEntrega" < r."FechaFinPeriodo"
                ), 0)
                WHERE r."Categoria" = 3
                  AND r."FechaInicioPeriodo" IS NOT NULL
                  AND r."FechaFinPeriodo" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalVendidoInformativo",
                table: "ReportesCaja");
        }
    }
}
