using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class SimplificarBolsasPorEmpaque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bolsa",
                table: "DetallesPedido");

            migrationBuilder.AddColumn<bool>(
                name: "EsBolsaPapel",
                table: "Insumos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsBolsaPapel",
                table: "Insumos");

            migrationBuilder.AddColumn<int>(
                name: "Bolsa",
                table: "DetallesPedido",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
