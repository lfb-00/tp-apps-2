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

            var otro = new Cliente("Taller San Martin", "contacto@tallersanmartin.com.ar");
            otro.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword("repMatch123!"));
            otro.AgregarVehiculo(new DatosVehiculo("Toyota", "Corolla", 2018, "1.8"));
            await contexto.Clientes.AddAsync(otro, ct);
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

    private static IEnumerable<ConfiguracionVehiculo> ConstruirConfiguracionesVehiculo()
    {
        yield return new("Volkswagen", "Gol", 2009, 2019, "1.6");
        yield return new("Volkswagen", "Polo", 2018, 2026, "1.6 MSI");
        yield return new("Volkswagen", "Polo", 2018, 2026, "1.0 TSI");
        yield return new("Volkswagen", "Amarok", 2010, 2026, "2.0 TDI");
        yield return new("Volkswagen", "Amarok", 2020, 2026, "3.0 V6 TDI");
        yield return new("Chevrolet", "Onix", 2013, 2019, "1.4");
        yield return new("Chevrolet", "Onix", 2020, 2026, "1.0 Turbo");
        yield return new("Chevrolet", "Cruze", 2011, 2016, "1.8");
        yield return new("Chevrolet", "Cruze", 2016, 2023, "1.4 Turbo");
        yield return new("Chevrolet", "Tracker", 2013, 2020, "1.8");
        yield return new("Chevrolet", "Tracker", 2020, 2026, "1.2 Turbo");
        yield return new("Ford", "Ka", 2008, 2013, "1.6");
        yield return new("Ford", "Ka", 2016, 2021, "1.5");
        yield return new("Ford", "Fiesta", 2010, 2019, "1.6");
        yield return new("Ford", "Ranger", 2012, 2022, "2.2 TDCi");
        yield return new("Ford", "Ranger", 2012, 2022, "3.2 TDCi");
        yield return new("Ford", "Ranger", 2023, 2026, "2.0 Bi-Turbo");
        yield return new("Ford", "Ranger", 2023, 2026, "3.0 V6");
        yield return new("Toyota", "Corolla", 2014, 2019, "1.8");
        yield return new("Toyota", "Corolla", 2020, 2026, "2.0");
        yield return new("Toyota", "Corolla", 2020, 2026, "1.8 Hybrid");
        yield return new("Toyota", "Etios", 2013, 2023, "1.5");
        yield return new("Toyota", "Hilux", 2016, 2026, "2.4 TDI");
        yield return new("Toyota", "Hilux", 2016, 2026, "2.8 TDI");
        yield return new("Renault", "Sandero", 2011, 2015, "1.6");
        yield return new("Renault", "Sandero", 2016, 2026, "1.6 SCe");
        yield return new("Renault", "Logan", 2011, 2015, "1.6");
        yield return new("Renault", "Logan", 2016, 2026, "1.6 SCe");
        yield return new("Renault", "Duster", 2011, 2020, "2.0");
        yield return new("Renault", "Duster", 2021, 2026, "1.3 TCe");
        yield return new("Peugeot", "208", 2013, 2020, "1.6");
        yield return new("Peugeot", "208", 2020, 2026, "1.2");
        yield return new("Peugeot", "208", 2020, 2026, "1.6");
        yield return new("Peugeot", "308", 2012, 2021, "1.6");
        yield return new("Peugeot", "308", 2012, 2021, "2.0");
        yield return new("Peugeot", "Partner", 2010, 2026, "1.6 HDi");
        yield return new("Fiat", "Cronos", 2018, 2026, "1.3");
        yield return new("Fiat", "Cronos", 2018, 2026, "1.8");
        yield return new("Fiat", "Palio", 2004, 2018, "1.4");
        yield return new("Fiat", "Palio", 2004, 2018, "1.6");
        yield return new("Fiat", "Toro", 2016, 2026, "1.8");
        yield return new("Fiat", "Toro", 2016, 2026, "2.0 TDI");
        yield return new("Fiat", "Toro", 2022, 2026, "1.3 Turbo");
    }

    private static IEnumerable<Repuesto> ConstruirCatalogo()
    {
        // --- Frenos ---
        var pastillasGol = new Repuesto("PAST-VW-GOL-DEL", "Pastillas de freno delanteras",
            SistemaVehiculo.Frenos, "Juego de 4 pastillas para eje delantero.");
        pastillasGol.AgregarEquivalencia("5U0698151");
        pastillasGol.AgregarEquivalencia("FDB1636");
        pastillasGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019));
        pastillasGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Voyage", 2009, 2019));
        yield return pastillasGol;

        var discosGol = new Repuesto("DISC-VW-GOL-256", "Discos de freno delanteros 256mm",
            SistemaVehiculo.Frenos, "Par de discos ventilados de 256 mm.");
        discosGol.AgregarEquivalencia("5U0615301");
        discosGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019));
        yield return discosGol;

        var pastillas208 = new Repuesto("PAST-PSA-208-DEL", "Pastillas de freno delanteras",
            SistemaVehiculo.Frenos, "Juego para Peugeot 208 y 2008.");
        pastillas208.AgregarEquivalencia("425467");
        pastillas208.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "208", 2013, 2023));
        pastillas208.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "2008", 2016, 2023));
        yield return pastillas208;

        // --- Motor ---
        var filtroAceiteGol = new Repuesto("FILT-ACE-VW-16", "Filtro de aceite motor 1.6",
            SistemaVehiculo.Motor, "Filtro roscado para motores VW 1.6 8v.");
        filtroAceiteGol.AgregarEquivalencia("030115561AN");
        filtroAceiteGol.AgregarEquivalencia("W712/52");
        filtroAceiteGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019, "1.6"));
        filtroAceiteGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Voyage", 2009, 2019, "1.6"));
        yield return filtroAceiteGol;

        var bujiasGol = new Repuesto("BUJI-VW-GOL-16", "Juego de bujias",
            SistemaVehiculo.Motor, "Cuatro bujias de encendido.");
        bujiasGol.AgregarEquivalencia("101000063AA");
        bujiasGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019, "1.6"));
        yield return bujiasGol;

        var correaCorolla = new Repuesto("CORR-TOY-COR-18", "Kit correa de distribucion",
            SistemaVehiculo.Motor, "Kit con correa, tensor y rodillos.");
        correaCorolla.AgregarAplicacion(new AplicacionVehiculo("Toyota", "Corolla", 2014, 2019, "1.8"));
        yield return correaCorolla;

        var filtroAire208 = new Repuesto("FILT-AIRE-PSA-208", "Filtro de aire motor",
            SistemaVehiculo.Motor, "Elemento filtrante de aire.");
        filtroAire208.AgregarEquivalencia("1444TV");
        filtroAire208.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "208", 2013, 2023));
        yield return filtroAire208;

        // --- Suspension ---
        var amortGol = new Repuesto("AMOR-VW-GOL-DEL", "Amortiguador delantero",
            SistemaVehiculo.Suspension, "Amortiguador hidraulico delantero, unidad.");
        amortGol.AgregarEquivalencia("5U0413031");
        amortGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019));
        yield return amortGol;

        var rotula208 = new Repuesto("ROTU-PSA-208", "Rotula de suspension inferior",
            SistemaVehiculo.Suspension, "Rotula con tuerca y chaveta.");
        rotula208.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "208", 2013, 2023));
        yield return rotula208;

        // --- Electrico ---
        var bateriaUniversal = new Repuesto("BATE-12V-60AH", "Bateria 12V 60Ah",
            SistemaVehiculo.Electrico, "Bateria de arranque libre de mantenimiento.");
        foreach (var (marca, modelo, desde, hasta) in AplicacionesBateria60())
            bateriaUniversal.AgregarAplicacion(new AplicacionVehiculo(marca, modelo, desde, hasta));
        yield return bateriaUniversal;

        var bateriaPickup = new Repuesto("BATE-12V-80AH", "Bateria 12V 80Ah",
            SistemaVehiculo.Electrico, "Bateria de arranque para camionetas.");
        bateriaPickup.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Amarok", 2010, 2026));
        bateriaPickup.AgregarAplicacion(new AplicacionVehiculo("Ford", "Ranger", 2012, 2026));
        bateriaPickup.AgregarAplicacion(new AplicacionVehiculo("Toyota", "Hilux", 2016, 2026));
        bateriaPickup.AgregarAplicacion(new AplicacionVehiculo("Fiat", "Toro", 2016, 2026));
        yield return bateriaPickup;

        // --- Refrigeracion ---
        var bombaAguaGol = new Repuesto("BOMB-AGUA-VW-16", "Bomba de agua",
            SistemaVehiculo.Refrigeracion, "Bomba de refrigerante con junta.");
        bombaAguaGol.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019, "1.6"));
        yield return bombaAguaGol;

        var radiadorCorolla = new Repuesto("RADI-TOY-COR", "Radiador de agua",
            SistemaVehiculo.Refrigeracion, "Radiador con panal de aluminio.");
        radiadorCorolla.AgregarAplicacion(new AplicacionVehiculo("Toyota", "Corolla", 2014, 2019));
        yield return radiadorCorolla;
    }

    private static IEnumerable<(string Marca, string Modelo, int Desde, int Hasta)> AplicacionesBateria60()
    {
        yield return ("Volkswagen", "Gol", 2009, 2019);
        yield return ("Volkswagen", "Polo", 2018, 2026);
        yield return ("Chevrolet", "Onix", 2013, 2026);
        yield return ("Chevrolet", "Cruze", 2011, 2023);
        yield return ("Chevrolet", "Tracker", 2013, 2026);
        yield return ("Ford", "Ka", 2008, 2021);
        yield return ("Ford", "Fiesta", 2010, 2019);
        yield return ("Toyota", "Corolla", 2014, 2026);
        yield return ("Toyota", "Etios", 2013, 2023);
        yield return ("Renault", "Sandero", 2011, 2026);
        yield return ("Renault", "Logan", 2011, 2026);
        yield return ("Renault", "Duster", 2011, 2026);
        yield return ("Peugeot", "208", 2013, 2026);
        yield return ("Peugeot", "308", 2012, 2021);
        yield return ("Peugeot", "Partner", 2010, 2026);
        yield return ("Fiat", "Cronos", 2018, 2026);
        yield return ("Fiat", "Palio", 2004, 2018);
    }
}
