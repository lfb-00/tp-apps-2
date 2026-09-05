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

/// <summary>Urgencia estimada del problema descripto por el cliente.</summary>
public enum NivelUrgencia
{
    Desconocida = 0,
    Baja = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}
