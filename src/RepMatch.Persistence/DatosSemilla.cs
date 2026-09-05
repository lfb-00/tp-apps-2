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
        if (await contexto.Repuestos.AnyAsync(ct)) return;   // idempotente

        foreach (var repuesto in ConstruirCatalogo())
            await contexto.Repuestos.AddAsync(repuesto, ct);

        var cliente = new Cliente("Bruno Lo Faro", "blofaro@uade.edu.ar");
        cliente.AgregarVehiculo(new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"), alias: "El Gol");
        cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019, "1.6"), alias: "El 208");
        await contexto.Clientes.AddAsync(cliente, ct);

        var otro = new Cliente("Taller San Martin", "contacto@tallersanmartin.com.ar");
        otro.AgregarVehiculo(new DatosVehiculo("Toyota", "Corolla", 2018, "1.8"));
        await contexto.Clientes.AddAsync(otro, ct);

        await contexto.SaveChangesAsync(ct);
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
        bateriaUniversal.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019));
        bateriaUniversal.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "208", 2013, 2023));
        bateriaUniversal.AgregarAplicacion(new AplicacionVehiculo("Toyota", "Corolla", 2014, 2019));
        yield return bateriaUniversal;

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
}
