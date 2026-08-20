using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEtiquetasTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Etiquetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Icono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etiquetas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductoEtiquetas",
                columns: table => new
                {
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    IdEtiqueta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoEtiquetas", x => new { x.IdProducto, x.IdEtiqueta });
                    table.ForeignKey(
                        name: "FK_ProductoEtiquetas_Etiquetas_IdEtiqueta",
                        column: x => x.IdEtiqueta,
                        principalTable: "Etiquetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoEtiquetas_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Etiquetas_Nombre",
                table: "Etiquetas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoEtiquetas_IdEtiqueta",
                table: "ProductoEtiquetas",
                column: "IdEtiqueta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductoEtiquetas");

            migrationBuilder.DropTable(
                name: "Etiquetas");
        }
    }
}
