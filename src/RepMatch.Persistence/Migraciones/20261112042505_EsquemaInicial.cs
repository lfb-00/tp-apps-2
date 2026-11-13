using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepMatch.Persistence.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "busquedas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    motor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TextoLibre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MotivoFalla = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodigosObjetivo = table.Column<string[]>(type: "text[]", nullable: false),
                    PreciosConsultadosEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CodigosConPrecios = table.Column<string[]>(type: "text[]", nullable: false),
                    SistemaDiagnosticado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Urgencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CausasProbables = table.Column<string[]>(type: "text[]", nullable: false),
                    FuenteDiagnostico = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MecanicoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompartidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiagnosticoConfirmado = table.Column<bool>(type: "boolean", nullable: true),
                    CodigosRecomendados = table.Column<string[]>(type: "text[]", nullable: false),
                    NotaMecanico = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RevisadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_busquedas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "configuraciones_vehiculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnioDesde = table.Column<int>(type: "integer", nullable: false),
                    AnioHasta = table.Column<int>(type: "integer", nullable: false),
                    Motor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuraciones_vehiculo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "derivaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusquedaId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfertaId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreTienda = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UrlDestino = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MecanicoId = table.Column<Guid>(type: "uuid", nullable: true),
                    OcurridaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_derivaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoCanonico = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Sistema = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CodigosEquivalentes = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repuestos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ofertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusquedaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoRepuesto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NombreTienda = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    precio_monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    envio_monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    envio_moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    UrlOriginal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Disponible = table.Column<bool>(type: "boolean", nullable: false),
                    precio_lista_monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    precio_lista_moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Coincidencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Marca = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Ean = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReferenciaTienda = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Vendedor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CapturadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ofertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ofertas_busquedas_BusquedaId",
                        column: x => x.BusquedaId,
                        principalTable: "busquedas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aplicaciones_vehiculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepuestoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnioDesde = table.Column<int>(type: "integer", nullable: false),
                    AnioHasta = table.Column<int>(type: "integer", nullable: false),
                    Motor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aplicaciones_vehiculo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_aplicaciones_vehiculo_repuestos_RepuestoId",
                        column: x => x.RepuestoId,
                        principalTable: "repuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HashContrasena = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FechaAlta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TieneWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    Provincia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Localidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FotoPerfil = table.Column<byte[]>(type: "bytea", maxLength: 5242880, nullable: true),
                    FotoTipoContenido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VehiculoPredeterminadoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemaPreferido = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombreTaller = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Especialidades = table.Column<int[]>(type: "integer[]", nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    motor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    Alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehiculos_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aplicaciones_vehiculo_Marca_Modelo",
                table: "aplicaciones_vehiculo",
                columns: new[] { "Marca", "Modelo" });

            migrationBuilder.CreateIndex(
                name: "IX_aplicaciones_vehiculo_RepuestoId",
                table: "aplicaciones_vehiculo",
                column: "RepuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_busquedas_ClienteId",
                table: "busquedas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_busquedas_FechaCreacion",
                table: "busquedas",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_busquedas_MecanicoId",
                table: "busquedas",
                column: "MecanicoId");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_Email",
                table: "clientes",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_Rol",
                table: "clientes",
                column: "Rol");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_VehiculoPredeterminadoId",
                table: "clientes",
                column: "VehiculoPredeterminadoId");

            migrationBuilder.CreateIndex(
                name: "IX_configuraciones_vehiculo_Marca_Modelo_AnioDesde_AnioHasta_M~",
                table: "configuraciones_vehiculo",
                columns: new[] { "Marca", "Modelo", "AnioDesde", "AnioHasta", "Motor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_derivaciones_BusquedaId",
                table: "derivaciones",
                column: "BusquedaId");

            migrationBuilder.CreateIndex(
                name: "IX_derivaciones_MecanicoId",
                table: "derivaciones",
                column: "MecanicoId");

            migrationBuilder.CreateIndex(
                name: "IX_derivaciones_OcurridaEn",
                table: "derivaciones",
                column: "OcurridaEn");

            migrationBuilder.CreateIndex(
                name: "IX_ofertas_BusquedaId",
                table: "ofertas",
                column: "BusquedaId");

            migrationBuilder.CreateIndex(
                name: "IX_ofertas_Ean",
                table: "ofertas",
                column: "Ean");

            migrationBuilder.CreateIndex(
                name: "IX_repuestos_CodigoCanonico",
                table: "repuestos",
                column: "CodigoCanonico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehiculos_ClienteId",
                table: "vehiculos",
                column: "ClienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_clientes_vehiculos_VehiculoPredeterminadoId",
                table: "clientes",
                column: "VehiculoPredeterminadoId",
                principalTable: "vehiculos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clientes_vehiculos_VehiculoPredeterminadoId",
                table: "clientes");

            migrationBuilder.DropTable(
                name: "aplicaciones_vehiculo");

            migrationBuilder.DropTable(
                name: "configuraciones_vehiculo");

            migrationBuilder.DropTable(
                name: "derivaciones");

            migrationBuilder.DropTable(
                name: "ofertas");

            migrationBuilder.DropTable(
                name: "repuestos");

            migrationBuilder.DropTable(
                name: "busquedas");

            migrationBuilder.DropTable(
                name: "vehiculos");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
