using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class ConservarProduccionAlAmpliarPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadProducida",
                table: "DetallesPedido",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Conservar la producción ya confirmada antes de esta actualización.
            migrationBuilder.Sql("""
                UPDATE "DetallesPedido" AS d SET "CantidadProducida" = d."Cantidad"
                FROM "Pedidos" AS p WHERE p."Id" = d."IdPedido" AND p."Estado" IN (1, 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CantidadProducida",
                table: "DetallesPedido");
        }
    }
}
