using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("capitulos")]
public class Capitulo
{
    [Column("id")]
    public int Id { get; set; }

    [Column("libro_id")]
    public int LibroId { get; set; }

    [Column("numero")]
    public int Numero { get; set; }

    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [Column("contenido")]
    public string Contenido { get; set; } = string.Empty;

    [Column("procesado_ia")]
    public bool ProcesadoIa { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; }

    public Libro? Libro { get; set; }

    public List<Escena> Escenas { get; set; } = new();
}
