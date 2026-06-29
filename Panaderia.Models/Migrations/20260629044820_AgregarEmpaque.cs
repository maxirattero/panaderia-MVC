using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEmpaque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoInsumo",
                table: "Insumos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoEmpaque",
                table: "DetallesPedido",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "IdEmpaque",
                table: "DetallesPedido",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LlevaEtiqueta",
                table: "DetallesPedido",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_IdEmpaque",
                table: "DetallesPedido",
                column: "IdEmpaque");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Insumos_IdEmpaque",
                table: "DetallesPedido",
                column: "IdEmpaque",
                principalTable: "Insumos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Insumos_IdEmpaque",
                table: "DetallesPedido");

            migrationBuilder.DropIndex(
                name: "IX_DetallesPedido_IdEmpaque",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "TipoInsumo",
                table: "Insumos");

            migrationBuilder.DropColumn(
                name: "CostoEmpaque",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "IdEmpaque",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "LlevaEtiqueta",
                table: "DetallesPedido");
        }
    }
}
