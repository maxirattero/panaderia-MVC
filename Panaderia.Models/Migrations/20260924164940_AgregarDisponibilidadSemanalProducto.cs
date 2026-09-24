using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDisponibilidadSemanalProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TieneDisponibilidadSemanal",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Activación inicial solo para las categorías de panes, sin confundir
            // productos como pan dulce ni aplicar la regla a todo lo "por encargo".
            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "TieneDisponibilidadSemanal" = TRUE
                FROM "CategoriasProducto" AS c
                WHERE p."IdCategoria" = c."Id"
                  AND lower(trim(c."Nombre")) IN ('pan', 'panes');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TieneDisponibilidadSemanal",
                table: "Productos");
        }
    }
}
