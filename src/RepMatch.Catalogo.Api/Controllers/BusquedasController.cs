using Microsoft.AspNetCore.Mvc;
using RepMatch.Aplicacion.Fachadas;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Api.Controllers;

/// <summary>
/// Busquedas (el "Pedido" de la consigna). Las consultas van directo al servicio; la creacion
/// entra por <see cref="FachadaBusqueda"/>, igual que desde la Web, para que los dos hosts
/// ejecuten exactamente la misma orquestacion del caso de uso.
///
/// En la Segunda Parte este controlador pasa a publicar el evento BusquedaCreada en RabbitMQ en
/// vez de resolver la compatibilidad en linea.
/// </summary>
[ApiController]
[Route("api/busquedas")]
[Produces("application/json")]
public sealed class BusquedasController(
    ServicioBusquedas servicio,
    FachadaBusqueda fachada) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BusquedaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BusquedaDto>>> ListarRecientes(
        [FromQuery] int cantidad = 20, CancellationToken ct = default) =>
        Ok(await servicio.ListarRecientesAsync(cantidad, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BusquedaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusquedaDto>> Obtener(Guid id, CancellationToken ct)
    {
        var busqueda = await servicio.ObtenerAsync(id, ct);

        return busqueda is null
            ? NotFound(new ProblemDetails { Title = $"No existe la busqueda {id}." })
            : Ok(busqueda);
    }

    [HttpGet("cliente/{clienteId:guid}")]
    [ProducesResponseType<IReadOnlyList<BusquedaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BusquedaDto>>> ListarPorCliente(
        Guid clienteId, CancellationToken ct) =>
        Ok(await servicio.ListarPorClienteAsync(clienteId, ct));

    [HttpPost]
    [ProducesResponseType<BusquedaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusquedaDto>> Crear(
        [FromBody] CrearBusquedaDto dto, CancellationToken ct)
    {
        var resultado = await fachada.ResolverBusquedaAsync(dto, ct);

        if (resultado.EsFallido)
            return BadRequest(new ProblemDetails
            {
                Title = "No se pudo crear la busqueda.",
                Detail = string.Join(" ", resultado.Errores)
            });

        return CreatedAtAction(nameof(Obtener), new { id = resultado.Valor.Id }, resultado.Valor);
    }
}
