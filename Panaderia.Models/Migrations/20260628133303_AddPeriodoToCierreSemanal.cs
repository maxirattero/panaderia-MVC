using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Panaderia.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodoToCierreSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinPeriodo",
                table: "ReportesCaja",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioPeriodo",
                table: "ReportesCaja",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaFinPeriodo",
                table: "ReportesCaja");

            migrationBuilder.DropColumn(
                name: "FechaInicioPeriodo",
                table: "ReportesCaja");
        }
    }
}
