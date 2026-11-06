using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class IncorporarFinanciacionInsumos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveOperacion",
                table: "ComprasProveedor",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdelantosPersonales",
                table: "CierresCaja",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReintegrosGenerados",
                table: "CierresCaja",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReintegrosProvisionados",
                table: "CierresCaja",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AportesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCompra = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cuenta = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AportesCompra", x => x.Id);
                    table.CheckConstraint("CK_Aporte_ImporteOrigen", "\"Monto\" > 0 AND ((\"Origen\" = 0 AND \"Cuenta\" IS NOT NULL AND \"Cuenta\" IN (1,2,3)) OR (\"Origen\" IN (1,2) AND \"Cuenta\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_AportesCompra_ComprasProveedor_IdCompra",
                        column: x => x.IdCompra,
                        principalTable: "ComprasProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotesFinanciados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    Unidades = table.Column<int>(type: "integer", nullable: false),
                    DesdeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaveOperacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesFinanciados", x => x.Id);
                    table.CheckConstraint("CK_Lote_Unidades", "\"Unidades\" > 0");
                    table.ForeignKey(
                        name: "FK_LotesFinanciados_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasReintegro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdAporte = table.Column<int>(type: "integer", nullable: false),
                    IdCierre = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasReintegro", x => x.Id);
                    table.CheckConstraint("CK_ReservaReintegro_Monto", "\"Monto\" > 0");
                    table.ForeignKey(
                        name: "FK_ReservasReintegro_AportesCompra_IdAporte",
                        column: x => x.IdAporte,
                        principalTable: "AportesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasReintegro_CierresCaja_IdCierre",
                        column: x => x.IdCierre,
                        principalTable: "CierresCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesCompraLote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCompra = table.Column<int>(type: "integer", nullable: false),
                    IdLote = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ClaveOperacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesCompraLote", x => x.Id);
                    table.CheckConstraint("CK_Asignacion_Monto", "\"Monto\" > 0");
                    table.ForeignKey(
                        name: "FK_AsignacionesCompraLote_ComprasProveedor_IdCompra",
                        column: x => x.IdCompra,
                        principalTable: "ComprasProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionesCompraLote_LotesFinanciados_IdLote",
                        column: x => x.IdLote,
                        principalTable: "LotesFinanciados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VentasLote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdLote = table.Column<int>(type: "integer", nullable: false),
                    IdDetallePedido = table.Column<int>(type: "integer", nullable: false),
                    Unidades = table.Column<int>(type: "integer", nullable: false),
                    IdCierre = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentasLote", x => x.Id);
                    table.CheckConstraint("CK_VentaLote_Unidades", "\"Unidades\" > 0");
                    table.ForeignKey(
                        name: "FK_VentasLote_CierresCaja_IdCierre",
                        column: x => x.IdCierre,
                        principalTable: "CierresCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentasLote_DetallesPedido_IdDetallePedido",
                        column: x => x.IdDetallePedido,
                        principalTable: "DetallesPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentasLote_LotesFinanciados_IdLote",
                        column: x => x.IdLote,
                        principalTable: "LotesFinanciados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagosReintegro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdAporte = table.Column<int>(type: "integer", nullable: false),
                    IdReserva = table.Column<int>(type: "integer", nullable: true),
                    IdMovimiento = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Usuario = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosReintegro", x => x.Id);
                    table.CheckConstraint("CK_PagoReintegro_Monto", "\"Monto\" > 0");
                    table.ForeignKey(
                        name: "FK_PagosReintegro_AportesCompra_IdAporte",
                        column: x => x.IdAporte,
                        principalTable: "AportesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosReintegro_ReportesCaja_IdMovimiento",
                        column: x => x.IdMovimiento,
                        principalTable: "ReportesCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosReintegro_ReservasReintegro_IdReserva",
                        column: x => x.IdReserva,
                        principalTable: "ReservasReintegro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasProveedor_ClaveOperacion",
                table: "ComprasProveedor",
                column: "ClaveOperacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AportesCompra_IdCompra_Origen",
                table: "AportesCompra",
                columns: new[] { "IdCompra", "Origen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesCompraLote_ClaveOperacion",
                table: "AsignacionesCompraLote",
                column: "ClaveOperacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesCompraLote_IdCompra_IdLote",
                table: "AsignacionesCompraLote",
                columns: new[] { "IdCompra", "IdLote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesCompraLote_IdLote",
                table: "AsignacionesCompraLote",
                column: "IdLote");

            migrationBuilder.CreateIndex(
                name: "IX_LotesFinanciados_ClaveOperacion",
                table: "LotesFinanciados",
                column: "ClaveOperacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesFinanciados_IdProducto",
                table: "LotesFinanciados",
                column: "IdProducto");

            migrationBuilder.CreateIndex(
                name: "IX_PagosReintegro_IdAporte",
                table: "PagosReintegro",
                column: "IdAporte");

            migrationBuilder.CreateIndex(
                name: "IX_PagosReintegro_IdMovimiento",
                table: "PagosReintegro",
                column: "IdMovimiento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosReintegro_IdReserva",
                table: "PagosReintegro",
                column: "IdReserva");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasReintegro_IdAporte_IdCierre",
                table: "ReservasReintegro",
                columns: new[] { "IdAporte", "IdCierre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservasReintegro_IdCierre",
                table: "ReservasReintegro",
                column: "IdCierre");

            migrationBuilder.CreateIndex(
                name: "IX_VentasLote_IdCierre",
                table: "VentasLote",
                column: "IdCierre");

            migrationBuilder.CreateIndex(
                name: "IX_VentasLote_IdDetallePedido",
                table: "VentasLote",
                column: "IdDetallePedido");

            migrationBuilder.CreateIndex(
                name: "IX_VentasLote_IdLote_IdDetallePedido",
                table: "VentasLote",
                columns: new[] { "IdLote", "IdDetallePedido" },
                unique: true);

            // Solo se reconstruyen compras con un pago inequívoco ya registrado.
            // Las compras antiguas sin cuenta conocida no se convierten en deuda personal.
            migrationBuilder.Sql("""
                INSERT INTO "AportesCompra" ("IdCompra", "Origen", "Monto", "Cuenta")
                SELECT c."Id", 0, c."MontoTotal", r."Cuenta"
                FROM "ComprasProveedor" c JOIN "ReportesCaja" r ON r."IdCompra" = c."Id"
                WHERE r."Tipo" = 1 AND r."Categoria" = 1 AND r."Cuenta" IN (1,2,3)
                  AND c."MontoTotal" > 0 AND r."Monto" = c."MontoTotal"
                  AND (SELECT count(*) FROM "ReportesCaja" m WHERE m."IdCompra" = c."Id") = 1;

                UPDATE "ComprasProveedor" c SET "ClaveOperacion" = r."ClaveOperacion"
                FROM "ReportesCaja" r WHERE r."IdCompra" = c."Id" AND r."ClaveOperacion" IS NOT NULL;
                """);

            // Tablas internas de MVC/Identity: acceso directo como propietario, sin Data API.
            // No hay políticas para anon/authenticated: una sesión Supabase no es un Admin MVC.
            // También revocamos grants por defecto previos al cambio de Supabase de octubre.
            migrationBuilder.Sql("""
                DO $seguridad$
                DECLARE tabla text; rol text; secuencia text;
                BEGIN
                  FOREACH tabla IN ARRAY ARRAY['AportesCompra','LotesFinanciados','AsignacionesCompraLote',
                    'VentasLote','ReservasReintegro','PagosReintegro'] LOOP
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', tabla);
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', tabla);
                    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.%I TO %I', tabla, current_user);
                    secuencia := pg_get_serial_sequence(format('public.%I', tabla), 'Id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', secuencia);
                    EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO %I', secuencia, current_user);
                    FOREACH rol IN ARRAY ARRAY['anon','authenticated','service_role'] LOOP
                      IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = rol) THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', tabla, rol);
                        EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', secuencia, rol);
                      END IF;
                    END LOOP;
                  END LOOP;
                END $seguridad$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesCompraLote");

            migrationBuilder.DropTable(
                name: "PagosReintegro");

            migrationBuilder.DropTable(
                name: "VentasLote");

            migrationBuilder.DropTable(
                name: "ReservasReintegro");

            migrationBuilder.DropTable(
                name: "LotesFinanciados");

            migrationBuilder.DropTable(
                name: "AportesCompra");

            migrationBuilder.DropIndex(
                name: "IX_ComprasProveedor_ClaveOperacion",
                table: "ComprasProveedor");

            migrationBuilder.DropColumn(
                name: "ClaveOperacion",
                table: "ComprasProveedor");

            migrationBuilder.DropColumn(
                name: "AdelantosPersonales",
                table: "CierresCaja");

            migrationBuilder.DropColumn(
                name: "ReintegrosGenerados",
                table: "CierresCaja");

            migrationBuilder.DropColumn(
                name: "ReintegrosProvisionados",
                table: "CierresCaja");
        }
    }
}
