using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class EmpaquesPredeterminadosYSubRecetasAnidadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "IdInsumo",
                table: "SubRecetaDetalles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "IdSubRecetaIngrediente",
                table: "SubRecetaDetalles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EtiquetaPredeterminada",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdEmpaquePredeterminado",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubRecetaDetalles_IdSubRecetaIngrediente",
                table: "SubRecetaDetalles",
                column: "IdSubRecetaIngrediente");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubRecetaDetalle_Ingrediente",
                table: "SubRecetaDetalles",
                sql: "(\"IdInsumo\" IS NOT NULL) <> (\"IdSubRecetaIngrediente\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubRecetaDetalle_NoAutoReferencia",
                table: "SubRecetaDetalles",
                sql: "\"IdSubRecetaIngrediente\" IS NULL OR \"IdSubRecetaIngrediente\" <> \"IdSubReceta\"");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdEmpaquePredeterminado",
                table: "Productos",
                column: "IdEmpaquePredeterminado");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Insumos_IdEmpaquePredeterminado",
                table: "Productos",
                column: "IdEmpaquePredeterminado",
                principalTable: "Insumos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Asociar los insumos existentes por nombre, sin modificar pedidos históricos.
            migrationBuilder.Sql("""
                UPDATE "Productos" p
                SET "IdEmpaquePredeterminado" = i."Id",
                    "EtiquetaPredeterminada" = lower(trim(c."Nombre")) NOT IN ('pan', 'panes')
                FROM "CategoriasProducto" c, "Insumos" i
                WHERE p."IdCategoria" = c."Id" AND i."TipoInsumo" = 1 AND i."Activo"
                  AND lower(trim(i."Nombre")) = CASE
                    WHEN lower(trim(c."Nombre")) IN ('pan', 'panes') THEN 'bolsa papel kraft delivery n°7'
                    WHEN lower(trim(c."Nombre")) IN ('cracker', 'crackers') THEN 'bolsa crackers 15x25'
                    WHEN lower(trim(c."Nombre")) IN ('prepizza', 'prepizzas', 'pizza', 'pizzas') THEN 'bolsa prepizza 35x45'
                  END;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_SubRecetaDetalles_SubRecetas_IdSubRecetaIngrediente",
                table: "SubRecetaDetalles",
                column: "IdSubRecetaIngrediente",
                principalTable: "SubRecetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SubRecetaDetalles" WHERE "IdSubRecetaIngrediente" IS NOT NULL) THEN
                        RAISE EXCEPTION 'No se puede revertir mientras existan sub-recetas anidadas.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Insumos_IdEmpaquePredeterminado",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_SubRecetaDetalles_SubRecetas_IdSubRecetaIngrediente",
                table: "SubRecetaDetalles");

            migrationBuilder.DropIndex(
                name: "IX_SubRecetaDetalles_IdSubRecetaIngrediente",
                table: "SubRecetaDetalles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubRecetaDetalle_Ingrediente",
                table: "SubRecetaDetalles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubRecetaDetalle_NoAutoReferencia",
                table: "SubRecetaDetalles");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdEmpaquePredeterminado",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdSubRecetaIngrediente",
                table: "SubRecetaDetalles");

            migrationBuilder.DropColumn(
                name: "EtiquetaPredeterminada",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdEmpaquePredeterminado",
                table: "Productos");

            migrationBuilder.AlterColumn<int>(
                name: "IdInsumo",
                table: "SubRecetaDetalles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
