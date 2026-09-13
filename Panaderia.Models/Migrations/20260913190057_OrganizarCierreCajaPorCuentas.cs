using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class OrganizarCierreCajaPorCuentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveOperacion",
                table: "ReportesCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cuenta",
                table: "ReportesCaja",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DescontarDelReparto",
                table: "ReportesCaja",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Destino",
                table: "ReportesCaja",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCierre",
                table: "ReportesCaja",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCierreDestino",
                table: "ReportesCaja",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCompra",
                table: "ReportesCaja",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdTransferencia",
                table: "ReportesCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEntregaReal",
                table: "Pedidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoIngredientes",
                table: "DetallesPedido",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCosto",
                table: "DetallesPedido",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CierresCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InicioSemana = table.Column<DateOnly>(type: "date", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Usuario = table.Column<string>(type: "text", nullable: true),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    EsHistorico = table.Column<bool>(type: "boolean", nullable: false),
                    IdMovimientoHistorico = table.Column<int>(type: "integer", nullable: true),
                    RetiroHistorico = table.Column<decimal>(type: "numeric", nullable: true),
                    PorcentajeReserva = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CobrosEfectivo = table.Column<decimal>(type: "numeric", nullable: false),
                    CobrosBanco = table.Column<decimal>(type: "numeric", nullable: false),
                    CobrosSinClasificar = table.Column<decimal>(type: "numeric", nullable: false),
                    Devoluciones = table.Column<decimal>(type: "numeric", nullable: false),
                    GastosReparto = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseReparto = table.Column<decimal>(type: "numeric", nullable: false),
                    Reserva = table.Column<decimal>(type: "numeric", nullable: false),
                    Ani = table.Column<decimal>(type: "numeric", nullable: false),
                    Maxi = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalVendido = table.Column<decimal>(type: "numeric", nullable: true),
                    CostoInsumos = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresCaja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeReserva = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    InicioSaldosUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AperturaEfectivo = table.Column<decimal>(type: "numeric", nullable: false),
                    AperturaBanco = table.Column<decimal>(type: "numeric", nullable: false),
                    AperturaReserva = table.Column<decimal>(type: "numeric", nullable: false),
                    FechaConfiguracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsuarioConfiguracion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionCaja", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionCaja_Id", "\"Id\" = 1");
                    table.CheckConstraint("CK_ConfiguracionCaja_Porcentaje", "\"PorcentajeReserva\" BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "CierresCajaCostos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCierre = table.Column<int>(type: "integer", nullable: false),
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    Producto = table.Column<string>(type: "text", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    Ingredientes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Empaque = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoPendiente = table.Column<bool>(type: "boolean", nullable: false),
                    Reconstruido = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresCajaCostos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresCajaCostos_CierresCaja_IdCierre",
                        column: x => x.IdCierre,
                        principalTable: "CierresCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CierresCajaSaldos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCierre = table.Column<int>(type: "integer", nullable: false),
                    Cuenta = table.Column<int>(type: "integer", nullable: false),
                    Inicial = table.Column<decimal>(type: "numeric", nullable: false),
                    Entradas = table.Column<decimal>(type: "numeric", nullable: false),
                    Salidas = table.Column<decimal>(type: "numeric", nullable: false),
                    Final = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresCajaSaldos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresCajaSaldos_CierresCaja_IdCierre",
                        column: x => x.IdCierre,
                        principalTable: "CierresCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Conservar cada cierre anterior sin repetir egresos ni suponer reparto o cuentas.
            migrationBuilder.Sql("""
                INSERT INTO "CierresCaja" (
                    "InicioSemana", "InicioUtc", "FinUtc", "RegistradoUtc", "Notas", "EsHistorico",
                    "IdMovimientoHistorico", "RetiroHistorico", "TotalVendido", "CobrosEfectivo", "CobrosBanco",
                    "CobrosSinClasificar", "Devoluciones", "GastosReparto", "BaseReparto", "Reserva", "Ani", "Maxi")
                SELECT (r."FechaInicioPeriodo" AT TIME ZONE 'UTC')::date,
                       r."FechaInicioPeriodo", r."FechaFinPeriodo", r."Fecha", r."Descripcion", true,
                       r."Id", r."Monto", r."TotalVendidoInformativo", 0, 0, 0, 0, 0, 0, 0, 0, 0
                FROM "ReportesCaja" r
                WHERE r."FechaInicioPeriodo" IS NOT NULL AND r."FechaFinPeriodo" IS NOT NULL
                  AND r."FechaFinPeriodo" > r."FechaInicioPeriodo";
                """);

            migrationBuilder.InsertData(
                table: "ConfiguracionCaja",
                columns: new[] { "Id", "AperturaBanco", "AperturaEfectivo", "AperturaReserva", "FechaConfiguracion", "InicioSaldosUtc", "PorcentajeReserva", "UsuarioConfiguracion" },
                values: new object[] { 1, 0m, 0m, 0m, null, null, 30m, null });

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_ClaveOperacion",
                table: "ReportesCaja",
                column: "ClaveOperacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_Cuenta_Fecha",
                table: "ReportesCaja",
                columns: new[] { "Cuenta", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_Fecha",
                table: "ReportesCaja",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_IdCierre",
                table: "ReportesCaja",
                column: "IdCierre");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_IdCierreDestino",
                table: "ReportesCaja",
                column: "IdCierreDestino");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_IdCompra",
                table: "ReportesCaja",
                column: "IdCompra");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_IdTransferencia_Tipo",
                table: "ReportesCaja",
                columns: new[] { "IdTransferencia", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_FechaEntregaReal_Estado",
                table: "Pedidos",
                columns: new[] { "FechaEntregaReal", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_CierresCaja_IdMovimientoHistorico",
                table: "CierresCaja",
                column: "IdMovimientoHistorico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CierresCaja_InicioSemana",
                table: "CierresCaja",
                column: "InicioSemana",
                unique: true,
                filter: "NOT \"EsHistorico\"");

            migrationBuilder.CreateIndex(
                name: "IX_CierresCajaCostos_IdCierre",
                table: "CierresCajaCostos",
                column: "IdCierre");

            migrationBuilder.CreateIndex(
                name: "IX_CierresCajaSaldos_IdCierre",
                table: "CierresCajaSaldos",
                column: "IdCierre");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportesCaja_CierresCaja_IdCierre",
                table: "ReportesCaja",
                column: "IdCierre",
                principalTable: "CierresCaja",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportesCaja_CierresCaja_IdCierreDestino",
                table: "ReportesCaja",
                column: "IdCierreDestino",
                principalTable: "CierresCaja",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportesCaja_ComprasProveedor_IdCompra",
                table: "ReportesCaja",
                column: "IdCompra",
                principalTable: "ComprasProveedor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportesCaja_CierresCaja_IdCierre",
                table: "ReportesCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportesCaja_CierresCaja_IdCierreDestino",
                table: "ReportesCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportesCaja_ComprasProveedor_IdCompra",
                table: "ReportesCaja");

            migrationBuilder.DropTable(
                name: "CierresCajaCostos");

            migrationBuilder.DropTable(
                name: "CierresCajaSaldos");

            migrationBuilder.DropTable(
                name: "ConfiguracionCaja");

            migrationBuilder.DropTable(
                name: "CierresCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_ClaveOperacion",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_Cuenta_Fecha",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_Fecha",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_IdCierre",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_IdCierreDestino",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_IdCompra",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_IdTransferencia_Tipo",
                table: "ReportesCaja");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_FechaEntregaReal_Estado",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ClaveOperacion",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "Cuenta",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "DescontarDelReparto",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "Destino",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "IdCierre",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "IdCierreDestino",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "IdCompra",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "IdTransferencia",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "FechaEntregaReal",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "CostoIngredientes",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "FechaCosto",
                table: "DetallesPedido");
        }
    }
}
