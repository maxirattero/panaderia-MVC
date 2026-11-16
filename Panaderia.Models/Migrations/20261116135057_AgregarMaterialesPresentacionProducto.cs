using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMaterialesPresentacionProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialesJson",
                table: "DetallesPedido",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductosMateriales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    IdInsumo = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosMateriales", x => x.Id);
                    table.CheckConstraint("CK_ProductoMaterial_Cantidad", "\"Cantidad\" > 0 AND \"Cantidad\" <= 999999.9999");
                    table.ForeignKey(
                        name: "FK_ProductosMateriales_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductosMateriales_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductosMateriales_IdInsumo",
                table: "ProductosMateriales",
                column: "IdInsumo");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosMateriales_IdProducto_IdInsumo",
                table: "ProductosMateriales",
                columns: new[] { "IdProducto", "IdInsumo" },
                unique: true);

            // Datos internos de MVC: permisos explícitos del propietario y RLS, sin Data API.
            migrationBuilder.Sql("""
                ALTER TABLE public."ProductosMateriales" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public."ProductosMateriales" FROM PUBLIC;
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public."ProductosMateriales" TO CURRENT_USER;
                DO $seguridad$
                DECLARE rol text; secuencia text;
                BEGIN
                    secuencia := pg_get_serial_sequence('public."ProductosMateriales"', 'Id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', secuencia);
                    EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO %I', secuencia, current_user);
                    FOREACH rol IN ARRAY ARRAY['anon','authenticated','service_role'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = rol) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public."ProductosMateriales" FROM %I', rol);
                            EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', secuencia, rol);
                        END IF;
                    END LOOP;
                END $seguridad$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductosMateriales");

            migrationBuilder.DropColumn(
                name: "MaterialesJson",
                table: "DetallesPedido");
        }
    }
}
