using System.ComponentModel.DataAnnotations;

namespace Immersic.Api.Dtos;

/// <summary>Datos para crear o actualizar una etiqueta de emoción.</summary>
public class EtiquetaEntradaDto
{
    /// <summary>Nombre de la etiqueta: una sola palabra de hasta 30 caracteres. Se guarda en minúsculas.</summary>
    /// <example>nostalgia</example>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(30, ErrorMessage = "El nombre admite hasta 30 caracteres.")]
    [RegularExpression(@"^\s*[\p{L}\p{N}_-]+\s*$", ErrorMessage = "El nombre debe ser una sola palabra (letras, números, guion o guion bajo).")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Descripción que la IA usa para decidir cuándo aplicar la etiqueta (hasta 300 caracteres).</summary>
    /// <example>Recuerdos del pasado, melodías suaves y añoranza.</example>
    [StringLength(300, ErrorMessage = "La descripción admite hasta 300 caracteres.")]
    public string? Descripcion { get; set; }
}

/// <summary>Etiqueta de emoción.</summary>
/// <param name="Id">Identificador de la etiqueta.</param>
/// <param name="Nombre">Nombre de la etiqueta.</param>
/// <param name="Descripcion">Descripción de la emoción.</param>
public record EtiquetaDto(int Id, string Nombre, string Descripcion);

/// <summary>Referencia corta a una etiqueta.</summary>
/// <param name="Id">Identificador de la etiqueta.</param>
/// <param name="Nombre">Nombre de la etiqueta.</param>
public record EtiquetaResumenDto(int Id, string Nombre);
