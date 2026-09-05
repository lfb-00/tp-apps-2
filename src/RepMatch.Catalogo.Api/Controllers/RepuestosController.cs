using Microsoft.AspNetCore.Mvc;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Api.Controllers;

/// <summary>
/// Expone el componente de catalogo por HTTP. Es la cara remota del mismo
/// <see cref="ICatalogoRepuestos"/> que la Web puede consumir en proceso: aca se resuelve
/// siempre con la implementacion local, porque este servicio ES el duenio de los datos.
/// </summary>
[ApiController]
[Route("api/repuestos")]
[Produces("application/json")]
public sealed class RepuestosController(ICatalogoRepuestos catalogo) : ControllerBase
{
    /// <summary>Catalogo completo de repuestos.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RepuestoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RepuestoDto>>> Listar(CancellationToken ct) =>
        Ok(await catalogo.ListarAsync(ct));

    /// <summary>Repuestos compatibles con un vehiculo.</summary>
    /// <param name="marca">Marca del vehiculo (ej. Volkswagen).</param>
    /// <param name="modelo">Modelo del vehiculo (ej. Gol).</param>
    /// <param name="anio">Anio del vehiculo.</param>
    /// <param name="motor">Motorizacion, opcional (ej. 1.6).</param>
    /// <param name="sistema">Filtro por sistema, opcional (Frenos, Motor, Suspension, ...).</param>
    [HttpGet("compatibles")]
    [ProducesResponseType<IReadOnlyList<RepuestoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RepuestoDto>>> BuscarCompatibles(
        [FromQuery] string marca,
        [FromQuery] string modelo,
        [FromQuery] int anio,
        [FromQuery] string? motor,
        [FromQuery] string? sistema,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            return BadRequest(new ProblemDetails { Title = "Marca y modelo son obligatorios." });

        if (anio is < 1950 or > 2100)
            return BadRequest(new ProblemDetails { Title = $"El anio {anio} esta fuera de rango." });

        var vehiculo = new VehiculoDto { Marca = marca, Modelo = modelo, Anio = anio, Motor = motor };

        return Ok(await catalogo.BuscarCompatiblesAsync(vehiculo, sistema, ct));
    }

    /// <summary>Un repuesto por su codigo canonico.</summary>
    [HttpGet("{codigo}")]
    [ProducesResponseType<RepuestoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RepuestoDto>> ObtenerPorCodigo(string codigo, CancellationToken ct)
    {
        var repuesto = await catalogo.ObtenerPorCodigoAsync(codigo, ct);

        return repuesto is null
            ? NotFound(new ProblemDetails { Title = $"No existe el repuesto {codigo}." })
            : Ok(repuesto);
    }
}
