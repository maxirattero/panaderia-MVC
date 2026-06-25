using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSubRecetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "IdInsumo",
                table: "RecetaDetalles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "IdSubReceta",
                table: "RecetaDetalles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubRecetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    MargenSeguridad = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubRecetas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubRecetaDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdSubReceta = table.Column<int>(type: "integer", nullable: false),
                    IdInsumo = table.Column<int>(type: "integer", nullable: false),
                    PorcentajePanadero = table.Column<decimal>(type: "numeric", nullable: true),
                    CantidadFija = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubRecetaDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubRecetaDetalles_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubRecetaDetalles_SubRecetas_IdSubReceta",
                        column: x => x.IdSubReceta,
                        principalTable: "SubRecetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecetaDetalles_IdSubReceta",
                table: "RecetaDetalles",
                column: "IdSubReceta");

            migrationBuilder.CreateIndex(
                name: "IX_SubRecetaDetalles_IdInsumo",
                table: "SubRecetaDetalles",
                column: "IdInsumo");

            migrationBuilder.CreateIndex(
                name: "IX_SubRecetaDetalles_IdSubReceta",
                table: "SubRecetaDetalles",
                column: "IdSubReceta");

            migrationBuilder.AddForeignKey(
                name: "FK_RecetaDetalles_SubRecetas_IdSubReceta",
                table: "RecetaDetalles",
                column: "IdSubReceta",
                principalTable: "SubRecetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecetaDetalles_SubRecetas_IdSubReceta",
                table: "RecetaDetalles");

            migrationBuilder.DropTable(
                name: "SubRecetaDetalles");

            migrationBuilder.DropTable(
                name: "SubRecetas");

            migrationBuilder.DropIndex(
                name: "IX_RecetaDetalles_IdSubReceta",
                table: "RecetaDetalles");

            migrationBuilder.DropColumn(
                name: "IdSubReceta",
                table: "RecetaDetalles");

            migrationBuilder.AlterColumn<int>(
                name: "IdInsumo",
                table: "RecetaDetalles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
