using Immersic.Api.Datos;
using Immersic.Api.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Servicios;

/// <summary>Resultado de validar las etiquetas de una pista o escena.</summary>
public sealed record ResultadoEtiquetas(List<Etiqueta> Etiquetas, string? Error)
{
    public bool EsValido => Error is null;

    public static ResultadoEtiquetas Fallo(string error) => new(new List<Etiqueta>(), error);
}

/// <summary>Reglas de negocio de las etiquetas de emoción.</summary>
public static class ReglasEtiquetas
{
    /// <summary>Etiqueta que solo refuerza a otra: no puede ir sola ni cuenta sola para recomendar.</summary>
    public const string Modificadora = "intenso";

    public const int Minimo = 1;

    public const int Maximo = 3;

    public static bool EsModificadora(string nombre) =>
        string.Equals(nombre, Modificadora, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifica que haya entre 1 y 3 etiquetas, sin repetir, que todas existan
    /// y que "intenso" no sea la única.
    /// </summary>
    public static async Task<ResultadoEtiquetas> ValidarAsync(
        ImmersicDbContext db,
        IReadOnlyCollection<int> ids,
        CancellationToken cancelacion)
    {
        var distintos = ids.Distinct().ToList();
        if (distintos.Count != ids.Count)
        {
            return ResultadoEtiquetas.Fallo("No se puede repetir una etiqueta.");
        }

        if (distintos.Count < Minimo || distintos.Count > Maximo)
        {
            return ResultadoEtiquetas.Fallo($"Se necesitan entre {Minimo} y {Maximo} etiquetas.");
        }

        var etiquetas = await db.Etiquetas
            .Where(etiqueta => distintos.Contains(etiqueta.Id))
            .OrderBy(etiqueta => etiqueta.Id)
            .ToListAsync(cancelacion);

        var inexistentes = distintos.Except(etiquetas.Select(etiqueta => etiqueta.Id)).ToList();
        if (inexistentes.Count > 0)
        {
            return ResultadoEtiquetas.Fallo($"No existen etiquetas con id: {string.Join(", ", inexistentes)}.");
        }

        if (etiquetas.All(etiqueta => EsModificadora(etiqueta.Nombre)))
        {
            return ResultadoEtiquetas.Fallo($"La etiqueta \"{Modificadora}\" no puede ir sola; acompáñala de otra etiqueta.");
        }

        return new ResultadoEtiquetas(etiquetas, null);
    }
}
