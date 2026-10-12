using System.ComponentModel.DataAnnotations;

namespace Immersic.Api.Dtos;

/// <summary>Datos para crear o actualizar un libro.</summary>
public class LibroEntradaDto
{
    /// <summary>Título del libro (1 a 150 caracteres).</summary>
    /// <example>El faro de las mareas</example>
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(150, ErrorMessage = "El título admite hasta 150 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Autor del libro (opcional, hasta 100 caracteres).</summary>
    /// <example>Laura Gómez</example>
    [StringLength(100, ErrorMessage = "El autor admite hasta 100 caracteres.")]
    public string? Autor { get; set; }
}

/// <summary>Libro registrado.</summary>
/// <param name="Id">Identificador del libro.</param>
/// <param name="Titulo">Título del libro.</param>
/// <param name="Autor">Autor del libro, si se registró.</param>
/// <param name="CreadoEn">Fecha de creación (UTC).</param>
/// <param name="CantidadCapitulos">Número de capítulos del libro.</param>
public record LibroDto(int Id, string Titulo, string? Autor, DateTime CreadoEn, int CantidadCapitulos);
