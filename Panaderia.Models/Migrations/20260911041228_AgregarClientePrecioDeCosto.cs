using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AgregarClientePrecioDeCosto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrecioDeCosto",
                table: "Clientes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecioDeCosto",
                table: "Clientes");
        }
    }
}
