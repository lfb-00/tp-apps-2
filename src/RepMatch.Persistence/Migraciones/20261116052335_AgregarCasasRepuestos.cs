using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepMatch.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCasasRepuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "casas_repuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdMapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latitud = table.Column<double>(type: "double precision", nullable: false),
                    Longitud = table.Column<double>(type: "double precision", nullable: false),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ImportadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_casas_repuestos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_casas_repuestos_IdMapa",
                table: "casas_repuestos",
                column: "IdMapa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_casas_repuestos_Latitud_Longitud",
                table: "casas_repuestos",
                columns: new[] { "Latitud", "Longitud" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "casas_repuestos");
        }
    }
}
