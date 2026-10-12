using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("escenas")]
public class Escena
{
    [Column("id")]
    public int Id { get; set; }

    [Column("capitulo_id")]
    public int CapituloId { get; set; }

    [Column("orden")]
    public int Orden { get; set; }

    [Column("posicion_inicio")]
    public int PosicionInicio { get; set; }

    [Column("fragmento_inicio")]
    public string FragmentoInicio { get; set; } = string.Empty;

    public Capitulo? Capitulo { get; set; }

    public List<EscenaEtiqueta> Etiquetas { get; set; } = new();
}
