using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class FixDetallePedidoProductoCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
