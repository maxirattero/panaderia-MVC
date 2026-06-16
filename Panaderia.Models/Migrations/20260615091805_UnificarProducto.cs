using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class UnificarProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_ProductosFinales_IdProductoFinal",
                table: "DetallesPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportesCaja_Pedidos_IdProveedor",
                table: "ReportesCaja");

            migrationBuilder.DropTable(
                name: "ProductosFinales");

            migrationBuilder.DropTable(
                name: "ProductosBase");

            migrationBuilder.RenameColumn(
                name: "IdProductoFinal",
                table: "DetallesPedido",
                newName: "IdProducto");

            migrationBuilder.RenameIndex(
                name: "IX_DetallesPedido_IdProductoFinal",
                table: "DetallesPedido",
                newName: "IX_DetallesPedido_IdProducto");

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCategoria = table.Column<int>(type: "integer", nullable: false),
                    Masa = table.Column<int>(type: "integer", nullable: false),
                    Variedad = table.Column<int>(type: "integer", nullable: true),
                    IdFormato = table.Column<int>(type: "integer", nullable: true),
                    IdTamano = table.Column<int>(type: "integer", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    PrecioFinal = table.Column<decimal>(type: "numeric", nullable: false),
                    PrecioReventa = table.Column<decimal>(type: "numeric", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    ImagenURL = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_CategoriasProducto_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "CategoriasProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Productos_Formatos_IdFormato",
                        column: x => x.IdFormato,
                        principalTable: "Formatos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Productos_Tamanos_IdTamano",
                        column: x => x.IdTamano,
                        principalTable: "Tamanos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportesCaja_IdPedido",
                table: "ReportesCaja",
                column: "IdPedido");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdCategoria",
                table: "Productos",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdFormato",
                table: "Productos",
                column: "IdFormato");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdTamano",
                table: "Productos",
                column: "IdTamano");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportesCaja_Pedidos_IdPedido",
                table: "ReportesCaja",
                column: "IdPedido",
                principalTable: "Pedidos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Productos_IdProducto",
                table: "DetallesPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportesCaja_Pedidos_IdPedido",
                table: "ReportesCaja");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_ReportesCaja_IdPedido",
                table: "ReportesCaja");

            migrationBuilder.RenameColumn(
                name: "IdProducto",
                table: "DetallesPedido",
                newName: "IdProductoFinal");

            migrationBuilder.RenameIndex(
                name: "IX_DetallesPedido_IdProducto",
                table: "DetallesPedido",
                newName: "IX_DetallesPedido_IdProductoFinal");

            migrationBuilder.CreateTable(
                name: "ProductosBase",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCategoria = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosBase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductosBase_CategoriasProducto_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "CategoriasProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductosFinales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdFormato = table.Column<int>(type: "integer", nullable: false),
                    IdProductoBase = table.Column<int>(type: "integer", nullable: false),
                    IdTamano = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImagenURL = table.Column<string>(type: "text", nullable: true),
                    PrecioFinal = table.Column<decimal>(type: "numeric", nullable: false),
                    PrecioReventa = table.Column<decimal>(type: "numeric", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosFinales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductosFinales_Formatos_IdFormato",
                        column: x => x.IdFormato,
                        principalTable: "Formatos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductosFinales_ProductosBase_IdProductoBase",
                        column: x => x.IdProductoBase,
                        principalTable: "ProductosBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductosFinales_Tamanos_IdTamano",
                        column: x => x.IdTamano,
                        principalTable: "Tamanos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductosBase_IdCategoria",
                table: "ProductosBase",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosFinales_IdFormato",
                table: "ProductosFinales",
                column: "IdFormato");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosFinales_IdProductoBase",
                table: "ProductosFinales",
                column: "IdProductoBase");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosFinales_IdTamano",
                table: "ProductosFinales",
                column: "IdTamano");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_ProductosFinales_IdProductoFinal",
                table: "DetallesPedido",
                column: "IdProductoFinal",
                principalTable: "ProductosFinales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportesCaja_Pedidos_IdProveedor",
                table: "ReportesCaja",
                column: "IdProveedor",
                principalTable: "Pedidos",
                principalColumn: "Id");
        }
    }
}
