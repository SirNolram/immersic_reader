using System.ComponentModel.DataAnnotations;

namespace Immersic.Api.Dtos;

/// <summary>Datos para registrar una escena manualmente, sin usar la IA.</summary>
public class EscenaEntradaDto
{
    /// <summary>Posición (índice de carácter) del contenido donde empieza la escena.</summary>
    /// <example>0</example>
    [Required(ErrorMessage = "La posición de inicio es obligatoria.")]
    [Range(0, 59999, ErrorMessage = "La posición debe estar entre 0 y 59.999.")]
    public int? PosicionInicio { get; set; }

    /// <summary>Ids de 1 a 3 etiquetas. "intenso" no puede ir sola.</summary>
    /// <example>[1, 7]</example>
    [Required(ErrorMessage = "Las etiquetas son obligatorias.")]
    [MinLength(1, ErrorMessage = "La escena necesita al menos una etiqueta.")]
    [MaxLength(3, ErrorMessage = "La escena admite como máximo 3 etiquetas.")]
    public List<int> EtiquetaIds { get; set; } = new();
}

/// <summary>Escena de un capítulo.</summary>
/// <param name="Id">Identificador de la escena.</param>
/// <param name="Orden">Posición de la escena dentro del capítulo (1, 2, 3...).</param>
/// <param name="PosicionInicio">Índice del carácter donde empieza la escena.</param>
/// <param name="PosicionFin">Índice donde termina (inicio de la siguiente escena o fin del capítulo).</param>
/// <param name="FragmentoInicio">Primeras palabras de la escena.</param>
/// <param name="Etiquetas">Etiquetas de emoción de la escena.</param>
public record EscenaDto(
    int Id,
    int Orden,
    int PosicionInicio,
    int PosicionFin,
    string FragmentoInicio,
    List<EtiquetaResumenDto> Etiquetas);

/// <summary>Resultado de generar la inmersión de un capítulo con la IA.</summary>
/// <param name="CapituloId">Capítulo procesado.</param>
/// <param name="Modelo">Modelo de Gemini que se usó.</param>
/// <param name="CantidadEscenas">Número de escenas generadas.</param>
/// <param name="Escenas">Escenas generadas, en orden de lectura.</param>
public record InmersionGeneradaDto(int CapituloId, string Modelo, int CantidadEscenas, List<EscenaDto> Escenas);

/// <summary>Pista recomendada para una escena.</summary>
/// <param name="Pista">Pista recomendada.</param>
/// <param name="Afinidad">Cantidad de etiquetas que comparte con la escena.</param>
/// <param name="EtiquetasCompartidas">Nombres de las etiquetas en común.</param>
public record RecomendacionDto(PistaDto Pista, int Afinidad, List<string> EtiquetasCompartidas);

/// <summary>Escena dentro del plan de inmersión, con la pista que le corresponde.</summary>
/// <param name="EscenaId">Identificador de la escena.</param>
/// <param name="Orden">Posición de la escena dentro del capítulo.</param>
/// <param name="PosicionInicio">Índice del carácter donde empieza la escena.</param>
/// <param name="PosicionFin">Índice donde termina la escena.</param>
/// <param name="FragmentoInicio">Primeras palabras de la escena.</param>
/// <param name="Etiquetas">Etiquetas de la escena.</param>
/// <param name="PistaRecomendada">Pista con mayor afinidad, o null si ninguna coincide.</param>
/// <param name="Afinidad">Etiquetas compartidas con la pista recomendada.</param>
public record EscenaPlanDto(
    int EscenaId,
    int Orden,
    int PosicionInicio,
    int PosicionFin,
    string FragmentoInicio,
    List<EtiquetaResumenDto> Etiquetas,
    PistaDto? PistaRecomendada,
    int Afinidad);

/// <summary>Plan de inmersión de un capítulo: qué pista suena en cada escena.</summary>
/// <param name="CapituloId">Identificador del capítulo.</param>
/// <param name="TituloCapitulo">Título del capítulo.</param>
/// <param name="ProcesadoIa">Indica si las escenas fueron generadas por la IA.</param>
/// <param name="Escenas">Escenas con su pista recomendada.</param>
public record PlanInmersionDto(int CapituloId, string TituloCapitulo, bool ProcesadoIa, List<EscenaPlanDto> Escenas);

/// <summary>Estado de la API y de sus dependencias.</summary>
/// <param name="Api">Estado de la API.</param>
/// <param name="BaseDeDatos">Estado de la conexión con SQLite.</param>
/// <param name="ServicioIa">Estado del servicio de IA.</param>
/// <param name="Fecha">Fecha de la consulta (UTC).</param>
public record SaludDto(string Api, string BaseDeDatos, string ServicioIa, DateTime Fecha);
