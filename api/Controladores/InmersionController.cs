using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Generación de escenas con Gemini y plan de música de cada capítulo.</summary>
[ApiController]
[Produces("application/json")]
public class InmersionController : ControladorBase
{
    private readonly ImmersicDbContext _db;
    private readonly ClienteServicioIa _servicioIa;

    public InmersionController(ImmersicDbContext db, ClienteServicioIa servicioIa)
    {
        _db = db;
        _servicioIa = servicioIa;
    }

    /// <summary>Genera la inmersión de un capítulo: la IA lo divide en escenas y etiqueta cada una.</summary>
    /// <remarks>
    /// Cada usuario usa su propia API key de Gemini, enviada en el header X-Gemini-Api-Key.
    /// La API no guarda la key. Si el capítulo ya tenía escenas, se reemplazan.
    /// </remarks>
    /// <param name="id">Id del capítulo.</param>
    /// <param name="claveGemini">API key de Gemini del usuario.</param>
    [HttpPost("api/capitulos/{id:int}/inmersion")]
    [ProducesResponseType(typeof(InmersionGeneradaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<InmersionGeneradaDto>> Generar(
        int id,
        [FromHeader(Name = ClienteServicioIa.EncabezadoApiKey)] string? claveGemini,
        CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(claveGemini))
        {
            return Problem(
                detail: $"Envía tu API key de Gemini en el header {ClienteServicioIa.EncabezadoApiKey}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Falta la API key de Gemini");
        }

        var capitulo = await _db.Capitulos.FirstOrDefaultAsync(c => c.Id == id, cancelacion);
        if (capitulo is null)
        {
            return NoEncontrado($"No existe un capítulo con id {id}.");
        }

        if (string.IsNullOrWhiteSpace(capitulo.Contenido))
        {
            return Problem(
                detail: "El capítulo no tiene contenido para analizar.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Capítulo vacío");
        }

        var catalogo = await _db.Etiquetas.AsNoTracking().OrderBy(e => e.Id).ToListAsync(cancelacion);
        if (catalogo.Count == 0)
        {
            return Conflicto("No hay etiquetas registradas para clasificar las escenas.");
        }

        ResultadoAnalisis analisis;
        try
        {
            analisis = await _servicioIa.AnalizarAsync(capitulo.Contenido, catalogo, claveGemini.Trim(), cancelacion);
        }
        catch (ExcepcionServicioIa error)
        {
            return Problem(detail: error.Message, statusCode: error.Estado, title: error.Titulo);
        }

        var porNombre = catalogo.ToDictionary(e => e.Nombre, StringComparer.OrdinalIgnoreCase);
        var porId = catalogo.ToDictionary(e => e.Id);
        var nuevas = ConstruirEscenas(capitulo, analisis, porNombre, porId);

        if (nuevas.Count == 0)
        {
            return Problem(
                detail: "La IA no devolvió escenas válidas para este capítulo. Intenta de nuevo.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Sin escenas");
        }

        await using (var transaccion = await _db.Database.BeginTransactionAsync(cancelacion))
        {
            await _db.Escenas.Where(e => e.CapituloId == id).ExecuteDeleteAsync(cancelacion);
            _db.Escenas.AddRange(nuevas);
            capitulo.ProcesadoIa = true;
            await _db.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }

        var escenasDto = Mapeos.AEscenasDto(nuevas, capitulo.Contenido.Length, porId);
        return new InmersionGeneradaDto(id, analisis.Modelo, escenasDto.Count, escenasDto);
    }

    /// <summary>Devuelve el plan de inmersión: cada escena con la pista que mejor le corresponde.</summary>
    [HttpGet("api/capitulos/{id:int}/inmersion")]
    [ProducesResponseType(typeof(PlanInmersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanInmersionDto>> ObtenerPlan(int id, CancellationToken cancelacion)
    {
        var capitulo = await _db.Capitulos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancelacion);
        if (capitulo is null)
        {
            return NoEncontrado($"No existe un capítulo con id {id}.");
        }

        var escenas = await _db.Escenas
            .AsNoTracking()
            .Where(e => e.CapituloId == id)
            .Include(e => e.Etiquetas)
            .ThenInclude(ee => ee.Etiqueta)
            .OrderBy(e => e.PosicionInicio)
            .ToListAsync(cancelacion);

        var pistas = await _db.Pistas
            .AsNoTracking()
            .Include(p => p.Etiquetas)
            .ThenInclude(pe => pe.Etiqueta)
            .ToListAsync(cancelacion);

        var plan = Mapeos.AEscenasDto(escenas, capitulo.Contenido.Length)
            .Select(escena =>
            {
                var nombres = escena.Etiquetas.Select(etiqueta => etiqueta.Nombre).ToList();
                var mejor = Recomendador.Recomendar(nombres, pistas, 1).FirstOrDefault();
                return new EscenaPlanDto(
                    escena.Id,
                    escena.Orden,
                    escena.PosicionInicio,
                    escena.PosicionFin,
                    escena.FragmentoInicio,
                    escena.Etiquetas,
                    mejor?.Pista,
                    mejor?.Afinidad ?? 0);
            })
            .ToList();

        return new PlanInmersionDto(capitulo.Id, capitulo.Titulo, capitulo.ProcesadoIa, plan);
    }

    private static List<Escena> ConstruirEscenas(
        Capitulo capitulo,
        ResultadoAnalisis analisis,
        IReadOnlyDictionary<string, Etiqueta> porNombre,
        IReadOnlyDictionary<int, Etiqueta> porId)
    {
        var escenas = new List<Escena>();
        foreach (var propuesta in analisis.Escenas.OrderBy(e => e.PosicionInicio))
        {
            var posicion = propuesta.PosicionInicio;
            if (posicion < 0 || posicion >= capitulo.Contenido.Length)
            {
                continue;
            }

            if (escenas.Count > 0 && posicion <= escenas[^1].PosicionInicio)
            {
                continue;
            }

            var idsEtiquetas = (propuesta.Etiquetas ?? new List<string>())
                .Select(nombre => porNombre.TryGetValue(nombre, out var etiqueta) ? etiqueta.Id : 0)
                .Where(idEtiqueta => idEtiqueta > 0)
                .Distinct()
                .Take(ReglasEtiquetas.Maximo)
                .ToList();

            if (idsEtiquetas.Count == 0 || idsEtiquetas.All(idEtiqueta => ReglasEtiquetas.EsModificadora(porId[idEtiqueta].Nombre)))
            {
                continue;
            }

            var escena = new Escena { CapituloId = capitulo.Id, PosicionInicio = posicion };
            escena.Etiquetas.AddRange(idsEtiquetas.Select(idEtiqueta => new EscenaEtiqueta { EtiquetaId = idEtiqueta }));
            escenas.Add(escena);
        }

        if (escenas.Count > 0)
        {
            escenas[0].PosicionInicio = 0;
        }

        for (var i = 0; i < escenas.Count; i++)
        {
            escenas[i].Orden = i + 1;
            escenas[i].FragmentoInicio = Texto.ExtraerFragmento(capitulo.Contenido, escenas[i].PosicionInicio);
        }

        return escenas;
    }
}
