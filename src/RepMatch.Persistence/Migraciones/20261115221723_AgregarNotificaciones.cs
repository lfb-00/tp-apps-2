using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepMatch.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BusquedaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_DestinatarioId_BusquedaId",
                table: "notificaciones",
                columns: new[] { "DestinatarioId", "BusquedaId" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_DestinatarioId_LeidaEn",
                table: "notificaciones",
                columns: new[] { "DestinatarioId", "LeidaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones");
        }
    }
}
