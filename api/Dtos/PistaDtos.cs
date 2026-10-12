using System.ComponentModel.DataAnnotations;

namespace Immersic.Api.Dtos;

/// <summary>Datos para registrar o actualizar una pista de música.</summary>
public class PistaEntradaDto
{
    /// <summary>Nombre de la pista (1 a 150 caracteres).</summary>
    /// <example>Lluvia sobre el puerto</example>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Artista o autor de la pista (opcional).</summary>
    /// <example>Orquesta Andina</example>
    [StringLength(100, ErrorMessage = "El artista admite hasta 100 caracteres.")]
    public string? Artista { get; set; }

    /// <summary>Ruta o URL del archivo de audio (opcional).</summary>
    /// <example>file:///musica/lluvia-puerto.mp3</example>
    [StringLength(500, ErrorMessage = "La ruta del archivo admite hasta 500 caracteres.")]
    public string? ArchivoUrl { get; set; }

    /// <summary>Ids de 1 a 3 etiquetas. "intenso" no puede ir sola.</summary>
    /// <example>[3, 4]</example>
    [Required(ErrorMessage = "Las etiquetas son obligatorias.")]
    [MinLength(1, ErrorMessage = "La pista necesita al menos una etiqueta.")]
    [MaxLength(3, ErrorMessage = "La pista admite como máximo 3 etiquetas.")]
    public List<int> EtiquetaIds { get; set; } = new();
}

/// <summary>Pista de música con sus etiquetas.</summary>
/// <param name="Id">Identificador de la pista.</param>
/// <param name="Nombre">Nombre de la pista.</param>
/// <param name="Artista">Artista, si se registró.</param>
/// <param name="ArchivoUrl">Ruta o URL del archivo, si se registró.</param>
/// <param name="Etiquetas">Etiquetas de emoción de la pista.</param>
public record PistaDto(int Id, string Nombre, string? Artista, string? ArchivoUrl, List<EtiquetaResumenDto> Etiquetas);
