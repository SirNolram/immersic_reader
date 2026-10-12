using Immersic.Api.Dtos;
using Immersic.Api.Modelos;

namespace Immersic.Api.Servicios;

public static class Mapeos
{
    public static EtiquetaDto AEtiquetaDto(Etiqueta etiqueta) =>
        new(etiqueta.Id, etiqueta.Nombre, etiqueta.Descripcion);

    public static EtiquetaResumenDto AEtiquetaResumen(Etiqueta etiqueta) =>
        new(etiqueta.Id, etiqueta.Nombre);

    public static CapituloDto ACapituloDto(Capitulo capitulo, int cantidadEscenas) =>
        new(
            capitulo.Id,
            capitulo.LibroId,
            capitulo.Numero,
            capitulo.Titulo,
            capitulo.Contenido,
            capitulo.Contenido.Length,
            capitulo.ProcesadoIa,
            cantidadEscenas,
            capitulo.CreadoEn);

    /// <summary>Requiere que las etiquetas de la pista estén cargadas (Include).</summary>
    public static PistaDto APistaDto(Pista pista) =>
        new(
            pista.Id,
            pista.Nombre,
            pista.Artista,
            pista.ArchivoUrl,
            pista.Etiquetas
                .Select(relacion => relacion.Etiqueta)
                .OfType<Etiqueta>()
                .OrderBy(etiqueta => etiqueta.Id)
                .Select(AEtiquetaResumen)
                .ToList());

    public static PistaDto APistaDto(Pista pista, IEnumerable<Etiqueta> etiquetas) =>
        new(
            pista.Id,
            pista.Nombre,
            pista.Artista,
            pista.ArchivoUrl,
            etiquetas.OrderBy(etiqueta => etiqueta.Id).Select(AEtiquetaResumen).ToList());

    /// <summary>
    /// Convierte escenas ordenadas por posición. La posición final de cada una es
    /// el inicio de la siguiente, o el final del capítulo para la última.
    /// Las etiquetas se toman de la navegación cargada (Include) o, si no está
    /// cargada, del catálogo recibido.
    /// </summary>
    public static List<EscenaDto> AEscenasDto(
        IReadOnlyList<Escena> escenasOrdenadas,
        int longitudContenido,
        IReadOnlyDictionary<int, Etiqueta>? catalogo = null)
    {
        Etiqueta? Resolver(EscenaEtiqueta relacion)
        {
            if (relacion.Etiqueta is not null)
            {
                return relacion.Etiqueta;
            }

            return catalogo is not null && catalogo.TryGetValue(relacion.EtiquetaId, out var encontrada) ? encontrada : null;
        }

        var resultado = new List<EscenaDto>(escenasOrdenadas.Count);
        for (var i = 0; i < escenasOrdenadas.Count; i++)
        {
            var escena = escenasOrdenadas[i];
            var fin = i + 1 < escenasOrdenadas.Count ? escenasOrdenadas[i + 1].PosicionInicio : longitudContenido;
            var etiquetas = escena.Etiquetas
                .Select(Resolver)
                .OfType<Etiqueta>()
                .OrderBy(etiqueta => etiqueta.Id)
                .Select(AEtiquetaResumen)
                .ToList();

            resultado.Add(new EscenaDto(escena.Id, escena.Orden, escena.PosicionInicio, fin, escena.FragmentoInicio, etiquetas));
        }

        return resultado;
    }
}
