using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("pista_etiquetas")]
public class PistaEtiqueta
{
    [Column("pista_id")]
    public int PistaId { get; set; }

    [Column("etiqueta_id")]
    public int EtiquetaId { get; set; }

    public Pista? Pista { get; set; }

    public Etiqueta? Etiqueta { get; set; }
}
