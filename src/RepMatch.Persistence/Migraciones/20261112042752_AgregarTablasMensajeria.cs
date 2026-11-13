using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepMatch.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarTablasMensajeria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS comisiones_tienda (
                    "Id" uuid NOT NULL,
                    "Tienda" character varying(80) NOT NULL,
                    "Periodo" character varying(7) NOT NULL,
                    "Derivaciones" integer NOT NULL,
                    "AtribuidasAMecanico" integer NOT NULL,
                    "UltimaEn" timestamp with time zone,
                    CONSTRAINT "PK_comisiones_tienda" PRIMARY KEY ("Id")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_comisiones_tienda_Tienda_Periodo"
                    ON comisiones_tienda ("Tienda", "Periodo");

                CREATE TABLE IF NOT EXISTS mensajes_procesados (
                    "MensajeId" uuid NOT NULL,
                    "Consumidor" character varying(80) NOT NULL,
                    "ProcesadoEn" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_mensajes_procesados" PRIMARY KEY ("MensajeId", "Consumidor")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comisiones_tienda");

            migrationBuilder.DropTable(
                name: "mensajes_procesados");
        }
    }
}
