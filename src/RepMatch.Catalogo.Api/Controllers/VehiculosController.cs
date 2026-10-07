using Microsoft.AspNetCore.Mvc;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Api.Controllers;

[ApiController]
[Route("api/vehiculos")]
[Produces("application/json")]
public sealed class VehiculosController(ICatalogoVehiculos catalogo) : ControllerBase
{
    [HttpGet("opciones")]
    [ProducesResponseType<IReadOnlyList<OpcionVehiculoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpcionVehiculoDto>>> ListarOpciones(CancellationToken ct) =>
        Ok(await catalogo.ListarOpcionesAsync(ct));
}
