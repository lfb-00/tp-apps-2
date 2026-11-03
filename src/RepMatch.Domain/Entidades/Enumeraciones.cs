namespace RepMatch.Domain.Entidades;

/// <summary>Sistema del vehiculo al que pertenece un repuesto. Es tambien la clase que
/// predice el componente de IA a partir del texto libre del cliente (Segunda Parte).</summary>
public enum SistemaVehiculo
{
    Desconocido = 0,
    Frenos = 1,
    Suspension = 2,
    Motor = 3,
    Transmision = 4,
    Electrico = 5,
    Refrigeracion = 6,
    Escape = 7,
    Direccion = 8,
    Carroceria = 9,
    Neumaticos = 10
}

/// <summary>Ciclo de vida de una Busqueda (el equivalente al "Pedido" de la consigna).</summary>
public enum EstadoBusqueda
{
    Borrador = 0,
    Pendiente = 1,
    Diagnosticada = 2,
    Completada = 3,
    Fallida = 4
}

/// <summary>
/// Que hace quien usa RepMatch. Es un dato de la cuenta y no dos entidades separadas: el mecanico
/// tambien tiene garage, busca y se loguea igual que un cliente; lo que cambia es lo que puede
/// hacer con las busquedas de otros. Se fija al registrarse.
/// </summary>
public enum RolUsuario
{
    Cliente = 0,
    Mecanico = 1
}

/// <summary>
/// Plan del mecanico en el modelo freemium. El cliente nunca paga: RepMatch gana por comision de
/// afiliado en cada derivacion a una tienda y por el plan Pro del profesional. El cobro del plan
/// queda fuera de alcance (lo haria un proveedor externo); aca solo se modelan sus limites.
/// </summary>
public enum PlanMecanico
{
    Gratuito = 0,
    Pro = 1
}

/// <summary>Urgencia estimada del problema descripto por el cliente.</summary>
public enum NivelUrgencia
{
    Desconocida = 0,
    Baja = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}
