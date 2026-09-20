using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion.Fachadas;

/// <summary>
/// Fachada del caso de uso "resolver una busqueda" (patron Facade).
///
/// La capa de presentacion tiene una sola puerta de entrada al caso de uso:
/// <see cref="ResolverBusquedaAsync"/>. Detras de ella, la fachada coordina a los colaboradores en
/// el orden correcto —admision de la solicitud, resolucion de los codigos objetivo contra el
/// catalogo, registro— y traduce las fallas de infraestructura a un
/// <see cref="ResultadoOperacion{T}"/> que la pantalla puede mostrar sin try/catch.
///
/// No contiene reglas de negocio propias: las de validacion viven en los validadores, las del
/// agregado en <see cref="Domain.Entidades.Busqueda"/> y las de registro en
/// <see cref="ServicioBusquedas"/>. La fachada solo decide el orden de los pasos y que hacer si
/// uno falla. Los pasos que mutan estado son <c>internal</c> en el servicio, asi que desde fuera
/// de esta capa la unica forma de crear una busqueda es pasar por aca.
///
/// Hoy orquesta tres colaboradores. En la Segunda Parte pasan a ser seis —diagnostico por IA,
/// compatibilidad SOAP, agregador de ofertas— y ni los componentes Razor ni el controlador REST
/// se enteran: siguen llamando a esta unica operacion. Los lugares exactos donde se enchufan
/// estan marcados abajo como PUNTO DE EXTENSION.
/// </summary>
public sealed class FachadaBusqueda(
    ServicioBusquedas busquedas,
    ServicioClientes clientes,
    ICatalogoRepuestos catalogo,
    ILogger<FachadaBusqueda> log)
{
    /// <summary>Como esta resolviendo el host el catalogo ("Local" o "Remoto"). La pantalla lo
    /// muestra junto al boton de registrar, como evidencia del modo de acceso.</summary>
    public string ModoCatalogo => catalogo.Modo;

    /// <summary>
    /// Unica operacion de alto nivel del caso de uso: recibe la solicitud del cliente y devuelve
    /// la busqueda registrada con sus codigos objetivo, o los motivos por los que no se pudo.
    /// </summary>
    public async Task<ResultadoOperacion<BusquedaDto>> ResolverBusquedaAsync(
        CrearBusquedaDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // ── Paso 1 · Admision ──────────────────────────────────────────────────────────────
        // Solicitud bien formada y cliente existente. Va antes de tocar el catalogo para no gastar
        // una llamada remota (ni, manana, una llamada a la IA) en algo que no se va a registrar.
        var admision = await busquedas.ValidarAsync(dto, ct);
        if (admision.EsFallido)
            return ResultadoOperacion<BusquedaDto>.Falla(admision.Errores);

        // ── Paso 2 · Codigos objetivo ──────────────────────────────────────────────────────
        // Hoy: los repuestos compatibles con el vehiculo segun el catalogo propio. Da igual si el
        // catalogo esta en proceso o detras de HTTP: eso ya lo decidio FabricaCatalogo.
        //
        // PUNTO DE EXTENSION (Segunda Parte) · Diagnostico por IA — issues #15 y #16
        //   IDiagnosticoIA.ProponerCodigosAsync(dto.TextoLibre, dto.Vehiculo) reemplaza esta
        //   consulta: la IA propone los codigos a partir del texto libre y el catalogo pasa a ser
        //   el fallback deterministico cuando el servicio no responde.
        //
        // PUNTO DE EXTENSION (Segunda Parte) · Compatibilidad SOAP — issues #10 y #11
        //   El servicio ConsultaCompatibilidad (CoreWCF) se enchufa aqui de dos maneras: como
        //   tercera implementacion de ICatalogoRepuestos elegida por la fabrica, o como filtro
        //   que verifica que los codigos propuestos por la IA realmente apliquen al vehiculo.
        IReadOnlyList<RepuestoDto> candidatos;
        try
        {
            candidatos = await catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, ct);
        }
        catch (Exception ex) when (EsFallaDeInfraestructura(ex, ct))
        {
            log.LogWarning(ex, "No se pudo consultar el catalogo[{Modo}] para {Vehiculo}",
                catalogo.Modo, dto.Vehiculo);

            return ResultadoOperacion<BusquedaDto>.Falla(
                $"No se pudo consultar el catalogo ({catalogo.Modo}): {ex.Message}");
        }

        // ── Paso 3 · Registro ──────────────────────────────────────────────────────────────
        // El servicio arma el agregado, le asigna los codigos (o lo marca Fallida si no hay
        // ninguno) y confirma la unidad de trabajo. Esas reglas son suyas y del dominio.
        var registro = await busquedas.RegistrarAsync(
            dto, candidatos.Select(r => r.CodigoCanonico), ct);

        if (registro.EsFallido)
            return registro;

        // ── Paso 4 · Ofertas ───────────────────────────────────────────────────────────────
        // PUNTO DE EXTENSION (Segunda Parte) · Agregador de ofertas — issues #4 y #19
        //   Con la busqueda ya persistida, el agregador consulta las tiendas externas (eBay,
        //   VTEX) en paralelo para cada codigo objetivo y ServicioOfertas registra el ranking.
        //   En el TP Inicial este paso no existe: la busqueda queda Diagnosticada, a la espera de
        //   ofertas. Cuando llegue la mensajeria (issue #13) el paso se vuelve asincronico: la
        //   fachada devuelve la busqueda y BusquedaCreada dispara la agregacion desde la cola.

        log.LogInformation(
            "Busqueda {BusquedaId} resuelta via catalogo[{Modo}]: {Codigos} codigos objetivo, estado {Estado}",
            registro.Valor.Id, catalogo.Modo, registro.Valor.CodigosObjetivo.Count, registro.Valor.Estado);

        return registro;
    }

    // ── Consultas de apoyo para la pantalla ────────────────────────────────────────────────
    // Delegacion pura, sin logica. Existen para que los componentes Razor dependan solo de la
    // fachada y no tengan que conocer los servicios que hay detras.

    public Task<IReadOnlyList<BusquedaDto>> ListarRecientesAsync(
        int cantidad = 20, CancellationToken ct = default) =>
        busquedas.ListarRecientesAsync(cantidad, ct);

    public Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct = default) =>
        clientes.ListarAsync(ct);

    /// <summary>Un timeout, un DNS que no resuelve o un 5xx del catalogo remoto son errores
    /// esperables del salto entre procesos y se devuelven como resultado. Una cancelacion pedida
    /// por el llamador (el circuito Blazor que se cierra) no lo es: se propaga.</summary>
    private static bool EsFallaDeInfraestructura(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
