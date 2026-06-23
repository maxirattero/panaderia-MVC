using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComprasProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdProveedor = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NumeroFactura = table.Column<string>(type: "text", nullable: true),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    MontoTotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasProveedor_Proveedores_IdProveedor",
                        column: x => x.IdProveedor,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprasDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCompra = table.Column<int>(type: "integer", nullable: false),
                    IdInsumo = table.Column<int>(type: "integer", nullable: false),
                    IdUnidadCompra = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasDetalle_ComprasProveedor_IdCompra",
                        column: x => x.IdCompra,
                        principalTable: "ComprasProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComprasDetalle_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComprasDetalle_UnidadesCompra_IdUnidadCompra",
                        column: x => x.IdUnidadCompra,
                        principalTable: "UnidadesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDetalle_IdCompra",
                table: "ComprasDetalle",
                column: "IdCompra");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDetalle_IdInsumo",
                table: "ComprasDetalle",
                column: "IdInsumo");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDetalle_IdUnidadCompra",
                table: "ComprasDetalle",
                column: "IdUnidadCompra");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasProveedor_IdProveedor",
                table: "ComprasProveedor",
                column: "IdProveedor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComprasDetalle");

            migrationBuilder.DropTable(
                name: "ComprasProveedor");
        }
    }
}
