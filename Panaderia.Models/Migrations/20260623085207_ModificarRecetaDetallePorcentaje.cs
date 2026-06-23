using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class ModificarRecetaDetallePorcentaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "RecetaDetalles");

            migrationBuilder.AddColumn<decimal>(
                name: "PesoHarinaGramos",
                table: "Recetas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CantidadFija",
                table: "RecetaDetalles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajePanadero",
                table: "RecetaDetalles",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PesoHarinaGramos",
                table: "Recetas");

            migrationBuilder.DropColumn(
                name: "CantidadFija",
                table: "RecetaDetalles");

            migrationBuilder.DropColumn(
                name: "PorcentajePanadero",
                table: "RecetaDetalles");

            migrationBuilder.AddColumn<decimal>(
                name: "Cantidad",
                table: "RecetaDetalles",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
