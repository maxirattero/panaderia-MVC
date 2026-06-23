using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarStockYUnidadesCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StockActual",
                table: "Insumos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StockMinimo",
                table: "Insumos",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnidadesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdInsumo = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    FactorConversion = table.Column<decimal>(type: "numeric", nullable: false),
                    EsPorDefecto = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadesCompra_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesCompra_IdInsumo",
                table: "UnidadesCompra",
                column: "IdInsumo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnidadesCompra");

            migrationBuilder.DropColumn(
                name: "StockActual",
                table: "Insumos");

            migrationBuilder.DropColumn(
                name: "StockMinimo",
                table: "Insumos");
        }
    }
}
