using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDescripcionEIngredientesProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionTienda",
                table: "Productos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ingredientes",
                table: "Productos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "DescripcionTienda" = $$🌾 Crackers - Sal Marina y Aceite de Oliva
                • Peso: 100g
                • A base de Harinas Orgánicas (Certificadas a Nivel Nacional)
                🌿 Apto veganos$$,
                    "Ingredientes" = $$Harina integral de trigo orgánica, harina de trigo 000 orgánica, agua, aceite de oliva y sal.$$
                FROM "CategoriasProducto" AS c
                WHERE p."IdCategoria" = c."Id"
                  AND LOWER(c."Nombre") LIKE 'cracker%'
                  AND p."Variedad" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionTienda",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Ingredientes",
                table: "Productos");
        }
    }
}
