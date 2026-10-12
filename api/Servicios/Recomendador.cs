using Immersic.Api.Dtos;
using Immersic.Api.Modelos;

namespace Immersic.Api.Servicios;

/// <summary>Afinidad entre una escena y una pista.</summary>
public sealed record Afinidad(int Puntaje, List<string> EtiquetasCompartidas);

/// <summary>
/// Elige la música de una escena comparando etiquetas: gana la pista que comparte
/// más etiquetas con la escena. "intenso" suma cuando coincide, pero no basta por
/// sí sola para que una pista sea compatible.
/// </summary>
public static class Recomendador
{
    public static Afinidad Calcular(IReadOnlyCollection<string> etiquetasEscena, IReadOnlyCollection<string> etiquetasPista)
    {
        var compartidas = etiquetasEscena
            .Intersect(etiquetasPista, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var principales = compartidas.Count(nombre => !ReglasEtiquetas.EsModificadora(nombre));
        return principales == 0
            ? new Afinidad(0, new List<string>())
            : new Afinidad(compartidas.Count, compartidas);
    }

    /// <summary>Requiere que las etiquetas de cada pista estén cargadas (Include).</summary>
    public static List<RecomendacionDto> Recomendar(
        IReadOnlyCollection<string> etiquetasEscena,
        IEnumerable<Pista> pistas,
        int limite)
    {
        return pistas
            .Select(pista => new
            {
                Pista = pista,
                Afinidad = Calcular(
                    etiquetasEscena,
                    pista.Etiquetas.Select(relacion => relacion.Etiqueta?.Nombre).OfType<string>().ToList()),
            })
            .Where(candidata => candidata.Afinidad.Puntaje > 0)
            .OrderByDescending(candidata => candidata.Afinidad.Puntaje)
            .ThenBy(candidata => candidata.Pista.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidata => candidata.Pista.Id)
            .Take(limite)
            .Select(candidata => new RecomendacionDto(
                Mapeos.APistaDto(candidata.Pista),
                candidata.Afinidad.Puntaje,
                candidata.Afinidad.EtiquetasCompartidas))
            .ToList();
    }
}
