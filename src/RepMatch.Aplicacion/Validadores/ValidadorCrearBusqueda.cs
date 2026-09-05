using FluentValidation;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion.Validadores;

public sealed class ValidadorVehiculo : AbstractValidator<VehiculoDto>
{
    public ValidadorVehiculo()
    {
        RuleFor(v => v.Marca).NotEmpty().WithMessage("Indica la marca del vehiculo.").MaximumLength(50);
        RuleFor(v => v.Modelo).NotEmpty().WithMessage("Indica el modelo del vehiculo.").MaximumLength(50);

        RuleFor(v => v.Anio)
            .InclusiveBetween(1950, DateTime.UtcNow.Year + 1)
            .WithMessage($"El anio debe estar entre 1950 y {DateTime.UtcNow.Year + 1}.");

        RuleFor(v => v.Motor).MaximumLength(50);
    }
}

public sealed class ValidadorCrearBusqueda : AbstractValidator<CrearBusquedaDto>
{
    public ValidadorCrearBusqueda()
    {
        RuleFor(b => b.ClienteId).NotEmpty().WithMessage("Falta identificar al cliente.");

        RuleFor(b => b.Vehiculo).NotNull().SetValidator(new ValidadorVehiculo());

        RuleFor(b => b.TextoLibre)
            .NotEmpty().WithMessage("Conta que le pasa al vehiculo.")
            .MinimumLength(5).WithMessage("Describi el problema con un poco mas de detalle.")
            .MaximumLength(1000);
    }
}
