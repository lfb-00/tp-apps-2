using FluentValidation;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;

namespace RepMatch.Aplicacion.Validadores;

public sealed class ValidadorActualizarPerfil : AbstractValidator<ActualizarPerfilDto>
{
    public ValidadorActualizarPerfil()
    {
        RuleFor(p => p.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .Must(n => n.Trim().Length >= 2).WithMessage("El nombre es demasiado corto.")
            .MaximumLength(120);

        RuleFor(p => p.Telefono)
            .Matches(@"^(?=.{8,20}$)\+?[0-9 ()-]+$")
            .WithMessage("El teléfono solo admite dígitos, espacios, guiones, paréntesis y un + inicial, entre 8 y 20 caracteres.")
            .When(p => !string.IsNullOrWhiteSpace(p.Telefono));

        RuleFor(p => p.Provincia)
            .Must(p => Provincias.Todas.Contains(p))
            .WithMessage("Elegí una provincia de la lista.")
            .When(p => !string.IsNullOrWhiteSpace(p.Provincia));

        RuleFor(p => p.Localidad)
            .MaximumLength(100).WithMessage("La localidad admite hasta 100 caracteres.");
    }
}

public sealed class ValidadorCambiarContrasena : AbstractValidator<CambiarContrasenaDto>
{
    public ValidadorCambiarContrasena()
    {
        RuleFor(c => c.ContrasenaActual)
            .NotEmpty().WithMessage("Ingresá tu contraseña actual.");

        RuleFor(c => c.ContrasenaNueva)
            .NotEmpty().WithMessage("La contraseña nueva es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100);

        RuleFor(c => c.RepetirContrasena)
            .Equal(c => c.ContrasenaNueva).WithMessage("Las contraseñas no coinciden.");
    }
}

public sealed class ValidadorCambiarFotoPerfil : AbstractValidator<CambiarFotoPerfilDto>
{
    public ValidadorCambiarFotoPerfil()
    {
        RuleFor(f => f.Datos)
            .NotEmpty().WithMessage("La imagen está vacía.")
            .Must(d => d.Length <= Cliente.TamanoMaximoFotoBytes)
            .WithMessage("La imagen supera el máximo de 5 MB.");

        RuleFor(f => f.TipoContenido)
            .Must(t => Cliente.TiposFotoPermitidos.Contains(t))
            .WithMessage("Formato no permitido. Usá JPG, PNG o WEBP.");
    }
}

public sealed class ValidadorEliminarCuenta : AbstractValidator<EliminarCuentaDto>
{
    public ValidadorEliminarCuenta()
    {
        RuleFor(e => e.Contrasena)
            .NotEmpty().WithMessage("Ingresá tu contraseña para confirmar.");
    }
}
