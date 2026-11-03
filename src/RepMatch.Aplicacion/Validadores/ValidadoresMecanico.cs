using FluentValidation;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;

namespace RepMatch.Aplicacion.Validadores;

public sealed class ValidadorActualizarTaller : AbstractValidator<ActualizarTallerDto>
{
    public ValidadorActualizarTaller()
    {
        RuleFor(t => t.NombreTaller)
            .MaximumLength(Cliente.LongitudMaximaNombreTaller)
            .WithMessage("El nombre del taller admite hasta 120 caracteres.");

        RuleForEach(t => t.Especialidades)
            .Must(e => Mapeadores.ASistema(e) is not null)
            .WithMessage("Elegí especialidades de la lista de sistemas.");
    }
}

public sealed class ValidadorRevisarBusqueda : AbstractValidator<RevisarBusquedaDto>
{
    public ValidadorRevisarBusqueda()
    {
        RuleFor(r => r.CodigosRecomendados)
            .Must(c => c.Any(codigo => !string.IsNullOrWhiteSpace(codigo)))
            .WithMessage("Para corregir el diagnóstico indicá al menos un repuesto.")
            .When(r => !r.ConfirmaDiagnostico);

        RuleFor(r => r.Nota)
            .MaximumLength(Busqueda.LongitudMaximaNota)
            .WithMessage("La nota admite hasta 1000 caracteres.");
    }
}
