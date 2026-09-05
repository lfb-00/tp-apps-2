using Microsoft.AspNetCore.Mvc;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Api.Controllers;

[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public sealed class ClientesController(ServicioClientes servicio) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClienteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClienteDto>>> Listar(CancellationToken ct) =>
        Ok(await servicio.ListarAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> Obtener(Guid id, CancellationToken ct)
    {
        var cliente = await servicio.ObtenerAsync(id, ct);

        return cliente is null
            ? NotFound(new ProblemDetails { Title = $"No existe el cliente {id}." })
            : Ok(cliente);
    }

    [HttpPost]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteDto>> Registrar(
        [FromBody] CrearClienteDto dto, CancellationToken ct)
    {
        var resultado = await servicio.RegistrarAsync(dto, ct);

        if (resultado.EsFallido)
            return BadRequest(new ProblemDetails
            {
                Title = "No se pudo registrar el cliente.",
                Detail = string.Join(" ", resultado.Errores)
            });

        return CreatedAtAction(nameof(Obtener), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPost("{id:guid}/vehiculos")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClienteDto>> AgregarVehiculo(
        Guid id, [FromBody] VehiculoDto vehiculo, CancellationToken ct)
    {
        var resultado = await servicio.AgregarVehiculoAsync(id, vehiculo, ct: ct);

        return resultado.EsFallido
            ? BadRequest(new ProblemDetails
            {
                Title = "No se pudo agregar el vehiculo.",
                Detail = string.Join(" ", resultado.Errores)
            })
            : Ok(resultado.Valor);
    }
}
