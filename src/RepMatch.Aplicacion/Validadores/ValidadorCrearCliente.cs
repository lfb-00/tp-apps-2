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

        RuleFor(c => c.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100);

        RuleFor(c => c.RepetirContrasena)
            .NotEmpty().WithMessage("Repetí la contraseña.")
            .Equal(c => c.Contrasena).WithMessage("Las contraseñas no coinciden.");
    }
}
