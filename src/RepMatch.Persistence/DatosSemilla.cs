using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Persistence;

/// <summary>
/// Catalogo inicial de repuestos y clientes de prueba. Cubre los modelos mas vendidos en
/// Argentina para que la demo del ejercicio local/remoto tenga datos realistas con que buscar.
/// </summary>
public static class DatosSemilla
{
    public static async Task SembrarAsync(RepMatchDbContext contexto, CancellationToken ct = default)
    {
        if (!await contexto.Repuestos.AnyAsync(ct))
            await contexto.Repuestos.AddRangeAsync(ConstruirCatalogo(), ct);

        if (!await contexto.ConfiguracionesVehiculo.AnyAsync(ct))
            await contexto.ConfiguracionesVehiculo.AddRangeAsync(ConstruirConfiguracionesVehiculo(), ct);

        Cliente? clientePrincipal = null;
        Vehiculo? gol = null;
        if (!await contexto.Clientes.AnyAsync(ct))
        {
            clientePrincipal = new Cliente("Bruno Lo Faro", "blofaro@uade.edu.ar");
            clientePrincipal.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword("repMatch123!"));
            clientePrincipal.ActualizarContacto("+54 9 11 1234-5678", tieneWhatsApp: true,
                "Ciudad Autónoma de Buenos Aires", "Monserrat");
            gol = clientePrincipal.AgregarVehiculo(new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"), alias: "El Gol");
            clientePrincipal.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019, "1.6"), alias: "El 208");
            await contexto.Clientes.AddAsync(clientePrincipal, ct);

            // Dos mecanicos para que el directorio muestre el orden por plan: el Pro primero.
            var taller = new Cliente("Taller San Martin", "contacto@tallersanmartin.com.ar", RolUsuario.Mecanico);
            taller.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword("repMatch123!"));
            taller.ActualizarContacto("+54 9 11 4567-8901", tieneWhatsApp: true,
                "Ciudad Autónoma de Buenos Aires", "San Nicolás");
            taller.ActualizarTaller("Taller San Martín", [SistemaVehiculo.Frenos, SistemaVehiculo.Suspension]);
            taller.AgregarVehiculo(new DatosVehiculo("Toyota", "Corolla", 2018, "1.8"));
            await contexto.Clientes.AddAsync(taller, ct);

            var mecanicoPro = new Cliente("Carla Benítez", "carla@motoresbenitez.com.ar", RolUsuario.Mecanico);
            mecanicoPro.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword("repMatch123!"));
            mecanicoPro.ActualizarContacto("+54 9 11 2345-6789", tieneWhatsApp: true,
                "Ciudad Autónoma de Buenos Aires", "Caballito");
            mecanicoPro.ActualizarTaller("Motores Benítez", [SistemaVehiculo.Motor, SistemaVehiculo.Refrigeracion]);
            mecanicoPro.CambiarPlan(PlanMecanico.Pro);
            await contexto.Clientes.AddAsync(mecanicoPro, ct);
        }

        await contexto.SaveChangesAsync(ct);

        if (clientePrincipal is not null && gol is not null)
        {
            // El predeterminado va en un segundo guardado: cliente -> vehiculo predeterminado y
            // vehiculo -> cliente forman un ciclo que EF no puede ordenar en un solo INSERT.
            clientePrincipal.EstablecerVehiculoPredeterminado(gol.Id);
            await contexto.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Solo las configuraciones que tienen al menos un repuesto con fuente en el catalogo: un
    /// modelo en el desplegable sin ninguna pieza seria una busqueda que siempre vuelve vacia.
    /// Sumar un modelo es sumar sus piezas con su fuente (docs/catalogo-fuentes.md) y despues
    /// su configuracion aca.
    /// </summary>
    private static IEnumerable<ConfiguracionVehiculo> ConstruirConfiguracionesVehiculo()
    {
        yield return new("Volkswagen", "Gol", 2009, 2019, "1.6");
        yield return new("Volkswagen", "Amarok", 2010, 2026, "2.0 TDI");
        yield return new("Volkswagen", "Amarok", 2020, 2026, "3.0 V6 TDI");
        yield return new("Chevrolet", "Onix", 2013, 2019, "1.4");
        yield return new("Chevrolet", "Onix", 2020, 2026, "1.0 Turbo");
        yield return new("Ford", "Ranger", 2012, 2022, "2.2 TDCi");
        yield return new("Ford", "Ranger", 2012, 2022, "3.2 TDCi");
        yield return new("Ford", "Ranger", 2023, 2026, "2.0 Bi-Turbo");
        yield return new("Ford", "Ranger", 2023, 2026, "3.0 V6");
        yield return new("Toyota", "Corolla", 2014, 2019, "1.8");
        yield return new("Toyota", "Hilux", 2016, 2026, "2.4 TDI");
        yield return new("Toyota", "Hilux", 2016, 2026, "2.8 TDI");
        yield return new("Renault", "Sandero", 2011, 2015, "1.6");
        yield return new("Renault", "Sandero", 2016, 2026, "1.6 SCe");
        yield return new("Renault", "Logan", 2011, 2015, "1.6");
        yield return new("Peugeot", "208", 2013, 2020, "1.6");
        yield return new("Peugeot", "208", 2020, 2026, "1.2");
        yield return new("Peugeot", "208", 2020, 2026, "1.6");
        yield return new("Fiat", "Cronos", 2018, 2026, "1.8");
    }

    /// <summary>
    /// Catalogo de repuestos con datos de catalogos oficiales de fabricantes, no inventados. Cada
    /// equivalencia (numero original de la automotriz o codigo de la marca de reposicion) y cada
    /// aplicacion (modelo, anios, motor) sale de una pagina citada en docs/catalogo-fuentes.md:
    /// Volkswagen Linha Economy (ed. 7, 2020), Bosch Filtros Linha Leve 2019/2020, Bosch Velas,
    /// Cabos e Bobinas 2019/2020, MAHLE Catalogo de Filtros 2025 (Mexico) y MANN-FILTER
    /// (guia Utes and LCV 01/2024, informativos tecnicos y catalogo en linea). Cubre diez familias
    /// (Gol, Amarok, Onix, Cronos, 208, Corolla, Hilux, Ranger, Sandero y Logan) y solo los tipos de pieza que
    /// esas fuentes documentan: lo que no tiene fuente no esta, en vez de estar inventado.
    /// </summary>
    private static IEnumerable<Repuesto> ConstruirCatalogo()
    {
        // --- Volkswagen Gol 1.6 (Linha Economy y Bosch) ---
        yield return Pieza("PAST-VW-GOL-DEL", "Pastillas de freno delanteras", SistemaVehiculo.Frenos,
            "Juego para eje delantero. Desde 2012 VW lista tambien 5U0-698-151-B (Cobreq N-286) segun el sistema de freno.",
            ["5U0-698-151-A", "JZZ-698-151-AF", "Cobreq N-288"],
            A("Volkswagen", "Gol", 2009, 2019));

        yield return Pieza("DISC-VW-GOL-DEL", "Discos de freno delanteros (par)", SistemaVehiculo.Frenos,
            "Kit de dos discos (JZZ-698-302-C); cada unidad es 5U0-615-301-C. Desde 2013 VW lista tambien 1S0-615-301-F.",
            ["5U0-615-301-C", "JZZ-698-302-C", "Fremax BD-5297"],
            A("Volkswagen", "Gol", 2009, 2019));

        yield return Pieza("ZAPA-VW-GOL-TRAS", "Zapatas de freno traseras", SistemaVehiculo.Frenos,
            "Juego para freno a tambor trasero.",
            ["5U0-698-525-B", "JZZ-698-520", "Bosch 0 986 487 270"],
            A("Volkswagen", "Gol", 2009, 2016));

        yield return Pieza("FILT-AIRE-VW-16", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 1.6 8V (EA111). El 1.6 MSI 16V de 2017 en adelante usa 04E-129-620-B.",
            ["5Z0-129-620", "JZZ-129-620-C", "Fram CA9410", "Tecfil ART6098", "Bosch 0 986 B02 371"],
            A("Volkswagen", "Gol", 2009, 2019, "1.6"));

        yield return Pieza("FILT-ACE-VW-16", "Filtro de aceite", SistemaVehiculo.Motor,
            "Filtro roscado. Motor 1.6 8V (EA111).",
            ["030-115-561-AR", "030-115-561-AQ", "JZZ-115-561-A", "Fram PH 10907", "Tecfil PSL560",
             "Bosch 0 986 B00 016", "Bosch 0 986 B00 043"],
            A("Volkswagen", "Gol", 2009, 2019, "1.6"));

        yield return Pieza("BUJI-VW-GOL-16", "Juego de bujias", SistemaVehiculo.Motor,
            "Bosch FR 5 DE 2, calibracion 0,9 mm. Motor 1.6 8V (EA111, CCRA).",
            ["Bosch F 000 KE0 P30", "Bosch SP 30"],
            A("Volkswagen", "Gol", 2009, 2019, "1.6"));

        yield return Pieza("BOBI-VW-GOL-16", "Bobina de encendido", SistemaVehiculo.Electrico,
            "Motor 1.6 8V (EA111, CCRA).",
            ["Bosch F 000 ZS0 210"],
            A("Volkswagen", "Gol", 2009, 2016, "1.6"));

        yield return Pieza("CABL-VW-GOL-16", "Juego de cables de bujia", SistemaVehiculo.Electrico,
            "Motor 1.6 8V (EA111, CCRA).",
            ["Bosch F 000 99C 125"],
            A("Volkswagen", "Gol", 2009, 2016, "1.6"));

        // --- Piezas compartidas entre marcas ---
        yield return Pieza("FILT-COMB-VW-REN-16", "Filtro de combustible", SistemaVehiculo.Motor,
            "Mismo filtro para el Gol 1.6 8V y los Renault 1.6 (K4M y K7M).",
            ["6QE-201-511-C", "JZW-201-511", "Bosch 0 986 BF0 024", "MANN WK 58/3", "Tecfil GI50/7"],
            A("Volkswagen", "Gol", 2009, 2016, "1.6"),
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2013, "1.6"));

        yield return Pieza("BATE-12V-50AH", "Bateria 12V 50Ah", SistemaVehiculo.Electrico,
            "Tamanio H4. Volkswagen la indica tambien para Onix, 208 y Sandero.",
            ["5GM-915-105-AF", "5GM-915-105-AG", "5GM-915-105-B", "5GM-915-105-C", "JZZ-915-105"],
            A("Volkswagen", "Gol", 2013, 2019),
            A("Chevrolet", "Onix", 2013, 2026),
            A("Peugeot", "208", 2013, 2026),
            A("Renault", "Sandero", 2011, 2026));

        // --- Chevrolet Onix 1.4 (Bosch, motor X14YFH) ---
        yield return Pieza("FILT-AIRE-GM-ONIX-14", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 1.4 8V (X14YFH).",
            ["Bosch 0 986 B02 530"],
            A("Chevrolet", "Onix", 2013, 2019, "1.4"));

        yield return Pieza("FILT-ACE-GM-ONIX-14", "Filtro de aceite", SistemaVehiculo.Motor,
            "Motor 1.4 8V (X14YFH).",
            ["Bosch 0 986 B00 041", "Bosch 0 986 B00 021"],
            A("Chevrolet", "Onix", 2013, 2019, "1.4"));

        yield return Pieza("FILT-COMB-GM-ONIX-14", "Filtro de combustible", SistemaVehiculo.Motor,
            "Motor 1.4 8V (X14YFH).",
            ["Bosch 0 986 BF0 018"],
            A("Chevrolet", "Onix", 2013, 2019, "1.4"));

        yield return Pieza("BUJI-GM-ONIX-14", "Juego de bujias", SistemaVehiculo.Motor,
            "Bosch WR 6 DC+, calibracion 0,9 mm. Motor 1.4 8V (X14YFH).",
            ["Bosch F 000 KE0 P34", "Bosch SP 34"],
            A("Chevrolet", "Onix", 2013, 2019, "1.4"));

        yield return Pieza("CABL-GM-ONIX-14", "Juego de cables de bujia", SistemaVehiculo.Electrico,
            "Motor 1.4 8V (X14YFH).",
            ["Bosch F 000 99C 612"],
            A("Chevrolet", "Onix", 2013, 2019, "1.4"));

        // --- Peugeot 208 1.6 (Bosch, motor NFP / EC5) ---
        yield return Pieza("FILT-AIRE-PSA-208-16", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 1.6 16V (NFP).",
            ["Bosch 0 986 B02 367"],
            A("Peugeot", "208", 2013, 2020, "1.6"));

        yield return Pieza("FILT-ACE-PSA-208-16", "Filtro de aceite", SistemaVehiculo.Motor,
            "Elemento filtrante (cartucho). Motor 1.6 16V (NFP).",
            ["Bosch 1 457 429 249"],
            A("Peugeot", "208", 2013, 2020, "1.6"));

        yield return Pieza("BUJI-PSA-208-16", "Juego de bujias", SistemaVehiculo.Motor,
            "Bosch FR 5 ME, calibracion 0,9 mm. Motor 1.6 16V (NFP).",
            ["Bosch F 000 KE0 P51"],
            A("Peugeot", "208", 2013, 2020, "1.6"));

        yield return Pieza("BOBI-PSA-208-16", "Bobina de encendido", SistemaVehiculo.Electrico,
            "Motor 1.6 16V (NFP).",
            ["Bosch F 000 ZS1 426"],
            A("Peugeot", "208", 2015, 2020, "1.6"));

        // --- Renault Sandero y Logan 1.6 (Bosch, motores K7M 8V y K4M 16V) ---
        yield return Pieza("FILT-ACE-REN-16", "Filtro de aceite", SistemaVehiculo.Motor,
            "Mismo filtro para los motores 1.6 8V (K7M) y 16V (K4M).",
            ["Bosch 0 451 103 332"],
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2013, "1.6"));

        yield return Pieza("FILT-AIRE-REN-16-8V", "Filtro de aire del motor (1.6 8V)", SistemaVehiculo.Motor,
            "Motor 1.6 8V (K7M). El 16V (K4M) usa otro filtro.",
            ["Bosch 0 986 B02 372"],
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2012, "1.6"));

        yield return Pieza("FILT-AIRE-REN-16-16V", "Filtro de aire del motor (1.6 16V)", SistemaVehiculo.Motor,
            "Motor 1.6 16V (K4M). El 8V (K7M) usa otro filtro.",
            ["Bosch 0 986 B02 333"],
            A("Renault", "Sandero", 2011, 2014, "1.6"),
            A("Renault", "Logan", 2011, 2013, "1.6"));

        yield return Pieza("BUJI-REN-16-8V", "Juego de bujias (1.6 8V)", SistemaVehiculo.Motor,
            "Bosch FR 7 LD+, calibracion 0,8 mm. Motor 1.6 8V (K7M).",
            ["Bosch F 000 KE0 P20", "Bosch SP 20"],
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2015, "1.6"));

        yield return Pieza("BUJI-REN-16-16V", "Juego de bujias (1.6 16V)", SistemaVehiculo.Motor,
            "Bosch FQR 6 DCW+, calibracion 0,9 mm. Motor 1.6 16V (K4M).",
            ["Bosch F 000 KE0 P49"],
            A("Renault", "Sandero", 2011, 2014, "1.6"),
            A("Renault", "Logan", 2011, 2013, "1.6"));

        yield return Pieza("BOBI-REN-16-8V", "Bobina de encendido (1.6 8V)", SistemaVehiculo.Electrico,
            "Motor 1.6 8V (K7M).",
            ["Bosch 0 986 221 060"],
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2015, "1.6"));

        yield return Pieza("CABL-REN-16-8V", "Juego de cables de bujia (1.6 8V)", SistemaVehiculo.Electrico,
            "Motor 1.6 8V (K7M).",
            ["Bosch F 000 99C 135"],
            A("Renault", "Sandero", 2011, 2015, "1.6"),
            A("Renault", "Logan", 2011, 2015, "1.6"));

        // --- Fiat Cronos 1.8 (Bosch, motor E.torQ) ---
        yield return Pieza("BUJI-FIAT-CRONOS-18", "Juego de bujias", SistemaVehiculo.Motor,
            "Bosch YR 7 KII 33 T, calibracion 0,8 mm. Motor 1.8 16V (E.torQ).",
            ["Bosch 0 242 135 563"],
            A("Fiat", "Cronos", 2018, 2026, "1.8"));

        // --- Toyota Corolla 1.8 (MAHLE, mercado mexicano: aplicacion por cilindrada y anio) ---
        yield return Pieza("FILT-AIRE-TOY-COR-18", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 1.8.",
            ["MAHLE LX 7292"],
            A("Toyota", "Corolla", 2014, 2019, "1.8"));

        yield return Pieza("FILT-ACE-TOY-COR-18", "Filtro de aceite (cartucho)", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 1.8, hasta 2016.",
            ["MAHLE OX 416D1"],
            A("Toyota", "Corolla", 2014, 2016, "1.8"));

        yield return Pieza("FILT-ACE-TOY-COR-18-R", "Filtro de aceite (roscado)", SistemaVehiculo.Motor,
            "Filtro roscado. Motor 1.8, desde 2017.",
            ["MAHLE OC 340"],
            A("Toyota", "Corolla", 2017, 2019, "1.8"));

        // --- Ford Ranger diesel (MAHLE) ---
        yield return Pieza("FILT-ACE-FORD-RAN-TDCI", "Filtro de aceite (cartucho)", SistemaVehiculo.Motor,
            "Elemento filtrante. Motores diesel 2.2 y 3.2 TDCi y 2.0 Bi-Turbo.",
            ["MAHLE OX 834D", "MANN HU 7002 z"],
            A("Ford", "Ranger", 2012, 2022, "2.2 TDCi"),
            A("Ford", "Ranger", 2012, 2022, "3.2 TDCi"),
            A("Ford", "Ranger", 2023, 2026, "2.0 Bi-Turbo"));

        yield return Pieza("FILT-AIRE-FORD-RAN-32", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 3.2 TDCi.",
            ["MANN C 22 024"],
            A("Ford", "Ranger", 2012, 2022, "3.2 TDCi"));

        yield return Pieza("FILT-COMB-FORD-RAN-32", "Filtro de combustible", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 3.2 TDCi.",
            ["MANN PU 9008 z"],
            A("Ford", "Ranger", 2012, 2022, "3.2 TDCi"));

        yield return Pieza("FILT-AIRE-FORD-RAN-23", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Ranger desde 2023: 2.0 Bi-Turbo y 3.0 V6.",
            ["MANN C 34 001"],
            A("Ford", "Ranger", 2023, 2026, "2.0 Bi-Turbo"),
            A("Ford", "Ranger", 2023, 2026, "3.0 V6"));

        yield return Pieza("FILT-ACE-FORD-RAN-V6", "Filtro de aceite (cartucho)", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 3.0 V6.",
            ["MANN HU 826 x"],
            A("Ford", "Ranger", 2023, 2026, "3.0 V6"));

        yield return Pieza("FILT-COMB-FORD-RAN-23", "Filtro de combustible", SistemaVehiculo.Motor,
            "Ranger desde 2023: 2.0 Bi-Turbo y 3.0 V6.",
            ["MANN WK 11 027"],
            A("Ford", "Ranger", 2023, 2026, "2.0 Bi-Turbo"),
            A("Ford", "Ranger", 2023, 2026, "3.0 V6"));

        // --- Volkswagen Amarok (Linha Economy, Bosch, MAHLE y MANN; primera generacion, 2H) ---
        yield return Pieza("PAST-VW-AMAROK-DEL", "Pastillas de freno delanteras", SistemaVehiculo.Frenos,
            "Juego para eje delantero.",
            ["2H0-698-151-A", "JZZ-698-151-AH", "Cobreq N-293"],
            A("Volkswagen", "Amarok", 2010, 2026));

        yield return Pieza("FILT-AIRE-VW-AMAROK", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Mismo filtro para el 2.0 TDI y el 3.0 V6 TDI.",
            ["MANN C 32 013", "Bosch F 026 400 286", "MAHLE LX 3138", "MAHLE LX 3139"],
            A("Volkswagen", "Amarok", 2010, 2026, "2.0 TDI"),
            A("Volkswagen", "Amarok", 2020, 2026, "3.0 V6 TDI"));

        yield return Pieza("FILT-ACE-VW-AMAROK-20", "Filtro de aceite (cartucho)", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 2.0 TDI.",
            ["MANN HU 7008 z", "Bosch F 026 407 023", "MAHLE OX 388D"],
            A("Volkswagen", "Amarok", 2010, 2026, "2.0 TDI"));

        yield return Pieza("FILT-ACE-VW-AMAROK-V6", "Filtro de aceite (cartucho)", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 3.0 V6 TDI.",
            ["MANN HU 7012 z"],
            A("Volkswagen", "Amarok", 2020, 2026, "3.0 V6 TDI"));

        yield return Pieza("FILT-COMB-VW-AMAROK-20", "Filtro de combustible", SistemaVehiculo.Motor,
            "Motor 2.0 TDI.",
            ["MANN WK 9016", "Bosch F 026 402 828", "MAHLE KL 787"],
            A("Volkswagen", "Amarok", 2010, 2026, "2.0 TDI"));

        yield return Pieza("FILT-COMB-VW-AMAROK-V6", "Filtro de combustible", SistemaVehiculo.Motor,
            "Motor 3.0 V6 TDI.",
            ["MANN WK 10 046 z"],
            A("Volkswagen", "Amarok", 2020, 2026, "3.0 V6 TDI"));

        // --- Toyota Hilux (MANN, octava generacion) ---
        yield return Pieza("FILT-AIRE-TOY-HILUX", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motores 2.4 y 2.8 diesel. MANN C 33 017/1 reemplaza al C 33 017.",
            ["17801-0L040", "MANN C 33 017/1", "MANN C 33 017", "Tecfil ARL2206"],
            A("Toyota", "Hilux", 2016, 2026, "2.4 TDI"),
            A("Toyota", "Hilux", 2016, 2026, "2.8 TDI"));

        yield return Pieza("FILT-ACE-TOY-HILUX-28", "Filtro de aceite", SistemaVehiculo.Motor,
            "Filtro roscado. Motor 2.8 diesel (1GD-FTV).",
            ["MANN W 712/83"],
            A("Toyota", "Hilux", 2016, 2026, "2.8 TDI"));

        yield return Pieza("FILT-COMB-TOY-HILUX-28", "Filtro de combustible", SistemaVehiculo.Motor,
            "Elemento filtrante. Motor 2.8 diesel (1GD-FTV).",
            ["MANN PU 9023 z"],
            A("Toyota", "Hilux", 2016, 2026, "2.8 TDI"));

        // --- Renault Sandero 1.6 SCe (MANN) ---
        yield return Pieza("FILT-AIRE-REN-SCE", "Filtro de aire del motor", SistemaVehiculo.Motor,
            "Motor 1.6 16V SCe. MANN C 27 030/1 reemplaza al C 27 030.",
            ["MANN C 27 030/1", "MANN C 27 030"],
            A("Renault", "Sandero", 2016, 2026, "1.6 SCe"));

        // --- Volkswagen Gol: embrague (Linha Economy) ---
        yield return Pieza("KIT-EMB-VW-GOL", "Kit de embrague (placa y disco)", SistemaVehiculo.Transmision,
            "Caja manual. El Gol I-Motion usa otro kit. VW lo arma con 036-141-026-Q + 032-141-033-D o con 030-141-026-F + 030-141-034-L.",
            ["JZZ-198-015-C", "036-141-026-Q", "032-141-033-D", "030-141-026-F", "030-141-034-L"],
            A("Volkswagen", "Gol", 2009, 2019));
    }

    private static AplicacionVehiculo A(string marca, string modelo, int desde, int hasta, string? motor = null) =>
        new(marca, modelo, desde, hasta, motor);

    private static Repuesto Pieza(string codigo, string nombre, SistemaVehiculo sistema, string descripcion,
        string[] equivalencias, params AplicacionVehiculo[] aplicaciones)
    {
        var repuesto = new Repuesto(codigo, nombre, sistema, descripcion);
        foreach (var equivalencia in equivalencias)
            repuesto.AgregarEquivalencia(equivalencia);
        foreach (var aplicacion in aplicaciones)
            repuesto.AgregarAplicacion(aplicacion);
        return repuesto;
    }
}
