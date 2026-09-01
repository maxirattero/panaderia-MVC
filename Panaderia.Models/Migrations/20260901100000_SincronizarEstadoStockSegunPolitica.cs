using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Panaderia.Models.Data;

#nullable disable

namespace Panaderia.Models.Migrations
{
    [DbContext(typeof(PanaderiaContext))]
    [Migration("20260901100000_SincronizarEstadoStockSegunPolitica")]
    public partial class SincronizarEstadoStockSegunPolitica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Panes y crackers se elaboran por encargo, incluso si hoy no tienen unidades cargadas.
            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "PorEncargo" = TRUE
                FROM "CategoriasProducto" AS c
                WHERE p."IdCategoria" = c."Id"
                  AND (LOWER(c."Nombre") LIKE 'pan%' OR LOWER(c."Nombre") LIKE 'cracker%');
                """);

            // Para el resto de los productos, la disponibilidad se determina exclusivamente por el stock.
            migrationBuilder.Sql("""
                UPDATE "Productos"
                SET "SinStock" = CASE
                    WHEN "PorEncargo" THEN FALSE
                    WHEN "Stock" <= 0 THEN TRUE
                    ELSE FALSE
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
