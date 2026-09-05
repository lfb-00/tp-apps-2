using FluentValidation;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion.Validadores;

public sealed class ValidadorCrearCliente : AbstractValidator<CrearClienteDto>
{
    public ValidadorCrearCliente()
    {
        RuleFor(c => c.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre es demasiado corto.")
            .MaximumLength(120);

        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .MaximumLength(200);
    }
}
