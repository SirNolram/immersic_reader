using System.Text.RegularExpressions;

namespace Immersic.Api.Servicios;

public static class Texto
{
    private const int LongitudFragmento = 80;

    /// <summary>
    /// Devuelve las primeras palabras del texto a partir de una posición,
    /// sin saltos de línea y con un máximo de 80 caracteres.
    /// </summary>
    public static string ExtraerFragmento(string texto, int posicion)
    {
        if (posicion < 0 || posicion >= texto.Length)
        {
            return string.Empty;
        }

        var fin = Math.Min(texto.Length, posicion + (LongitudFragmento * 3));
        var limpio = Regex.Replace(texto[posicion..fin], @"\s+", " ").Trim();
        if (limpio.Length <= LongitudFragmento)
        {
            return limpio;
        }

        var recorte = limpio[..LongitudFragmento];
        if (char.IsHighSurrogate(recorte[^1]))
        {
            recorte = recorte[..^1];
        }

        var ultimoEspacio = recorte.LastIndexOf(' ');
        return (ultimoEspacio > LongitudFragmento / 2 ? recorte[..ultimoEspacio] : recorte).Trim();
    }
}
