using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("libros")]
public class Libro
{
    [Column("id")]
    public int Id { get; set; }

    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [Column("autor")]
    public string? Autor { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; }

    public List<Capitulo> Capitulos { get; set; } = new();
}
