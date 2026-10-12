using System.ComponentModel.DataAnnotations.Schema;

namespace Immersic.Api.Modelos;

[Table("etiquetas")]
public class Etiqueta
{
    [Column("id")]
    public int Id { get; set; }

    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
}
