using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("escena_etiquetas")]
public class EscenaEtiqueta
{
    [Column("escena_id")]
    public int EscenaId { get; set; }

    [Column("etiqueta_id")]
    public int EtiquetaId { get; set; }

    public Escena? Escena { get; set; }

    public Etiqueta? Etiqueta { get; set; }
}
