using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("pistas")]
public class Pista
{
    [Column("id")]
    public int Id { get; set; }

    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Column("artista")]
    public string? Artista { get; set; }

    [Column("archivo_url")]
    public string? ArchivoUrl { get; set; }

    public List<PistaEtiqueta> Etiquetas { get; set; } = new();
}
