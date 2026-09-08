using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionTienda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RetiroHabilitado = table.Column<bool>(type: "boolean", nullable: false),
                    MontoMinimoPedido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionTienda", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionTienda_Minimo", "\"MontoMinimoPedido\" >= 0");
                    table.CheckConstraint("CK_ConfiguracionTienda_Unica", "\"Id\" = 1");
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionTienda",
                columns: new[] { "Id", "MontoMinimoPedido", "RetiroHabilitado" },
                values: new object[] { 1, 0m, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionTienda");
        }
    }
}
