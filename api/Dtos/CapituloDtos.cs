using System.ComponentModel.DataAnnotations;

namespace Immersic.Api.Dtos;

/// <summary>Datos para crear o actualizar un capítulo.</summary>
public class CapituloEntradaDto
{
    /// <summary>Título del capítulo (1 a 150 caracteres).</summary>
    /// <example>La tormenta</example>
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(150, ErrorMessage = "El título admite hasta 150 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Texto del capítulo (hasta 60.000 caracteres). Al crear, si se omite queda vacío.
    /// Al actualizar, si se omite no cambia; si cambia, se borran las escenas generadas.
    /// </summary>
    /// <example>El viento golpeaba las ventanas del faro mientras Elena encendía la lámpara.</example>
    [StringLength(60000, ErrorMessage = "El contenido admite hasta 60.000 caracteres.")]
    public string? Contenido { get; set; }

    /// <summary>
    /// Número del capítulo dentro del libro. Al crear, si se omite se asigna el siguiente.
    /// Al actualizar, si se omite no cambia.
    /// </summary>
    /// <example>1</example>
    [Range(1, 10000, ErrorMessage = "El número debe estar entre 1 y 10.000.")]
    public int? Numero { get; set; }
}

/// <summary>Capítulo sin su contenido, para listados.</summary>
/// <param name="Id">Identificador del capítulo.</param>
/// <param name="LibroId">Libro al que pertenece.</param>
/// <param name="Numero">Número del capítulo dentro del libro.</param>
/// <param name="Titulo">Título del capítulo.</param>
/// <param name="Caracteres">Longitud del contenido.</param>
/// <param name="ProcesadoIa">Indica si la IA ya generó sus escenas.</param>
/// <param name="CantidadEscenas">Número de escenas registradas.</param>
/// <param name="CreadoEn">Fecha de creación (UTC).</param>
public record CapituloResumenDto(
    int Id,
    int LibroId,
    int Numero,
    string Titulo,
    int Caracteres,
    bool ProcesadoIa,
    int CantidadEscenas,
    DateTime CreadoEn);

/// <summary>Capítulo completo, con su contenido.</summary>
/// <param name="Id">Identificador del capítulo.</param>
/// <param name="LibroId">Libro al que pertenece.</param>
/// <param name="Numero">Número del capítulo dentro del libro.</param>
/// <param name="Titulo">Título del capítulo.</param>
/// <param name="Contenido">Texto del capítulo.</param>
/// <param name="Caracteres">Longitud del contenido.</param>
/// <param name="ProcesadoIa">Indica si la IA ya generó sus escenas.</param>
/// <param name="CantidadEscenas">Número de escenas registradas.</param>
/// <param name="CreadoEn">Fecha de creación (UTC).</param>
public record CapituloDto(
    int Id,
    int LibroId,
    int Numero,
    string Titulo,
    string Contenido,
    int Caracteres,
    bool ProcesadoIa,
    int CantidadEscenas,
    DateTime CreadoEn);
