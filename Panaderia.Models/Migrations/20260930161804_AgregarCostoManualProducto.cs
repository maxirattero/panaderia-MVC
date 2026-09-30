using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCostoManualProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoManual",
                table: "Productos",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_CostoManual",
                table: "Productos",
                sql: "\"CostoManual\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_CostoManual",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CostoManual",
                table: "Productos");
        }
    }
}
