using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarPesoHarinaAPesoUnitario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PesoHarinaGramos",
                table: "Recetas",
                newName: "PesoUnitario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PesoUnitario",
                table: "Recetas",
                newName: "PesoHarinaGramos");
        }
    }
}
