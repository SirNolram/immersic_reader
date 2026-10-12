using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Escenas de cada capítulo y recomendación de música por escena.</summary>
[ApiController]
[Produces("application/json")]
public class EscenasController : ControladorBase
{
    private const int LimiteRecomendacionesPorDefecto = 5;
    private const int LimiteRecomendacionesMaximo = 50;

    private readonly ImmersicDbContext _db;

    public EscenasController(ImmersicDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista las escenas de un capítulo en orden de lectura.</summary>
    [HttpGet("api/capitulos/{capituloId:int}/escenas")]
    [ProducesResponseType(typeof(List<EscenaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EscenaDto>>> Listar(int capituloId, CancellationToken cancelacion)
    {
        var contenido = await _db.Capitulos
            .AsNoTracking()
            .Where(c => c.Id == capituloId)
            .Select(c => c.Contenido)
            .FirstOrDefaultAsync(cancelacion);
        if (contenido is null)
        {
            return NoEncontrado($"No existe un capítulo con id {capituloId}.");
        }

        var escenas = await CargarEscenasAsync(capituloId, cancelacion);
        return Mapeos.AEscenasDto(escenas, contenido.Length);
    }

    /// <summary>Registra una escena manualmente, sin usar la IA.</summary>
    /// <remarks>
    /// Sirve para probar las recomendaciones sin una API key de Gemini. El fragmento de inicio
    /// se toma del contenido del capítulo y el orden de las escenas se recalcula según su posición.
    /// </remarks>
    [HttpPost("api/capitulos/{capituloId:int}/escenas")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EscenaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EscenaDto>> Crear(int capituloId, EscenaEntradaDto entrada, CancellationToken cancelacion)
    {
        var capitulo = await _db.Capitulos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == capituloId, cancelacion);
        if (capitulo is null)
        {
            return NoEncontrado($"No existe un capítulo con id {capituloId}.");
        }

        var posicion = entrada.PosicionInicio ?? 0;
        if (posicion >= capitulo.Contenido.Length)
        {
            return DatosInvalidos(
                nameof(EscenaEntradaDto.PosicionInicio),
                $"La posición debe ser menor que la longitud del capítulo ({capitulo.Contenido.Length} caracteres).");
        }

        var validacion = await ReglasEtiquetas.ValidarAsync(_db, entrada.EtiquetaIds, cancelacion);
        if (!validacion.EsValido)
        {
            return DatosInvalidos(nameof(EscenaEntradaDto.EtiquetaIds), validacion.Error!);
        }

        var existentes = await _db.Escenas.Where(e => e.CapituloId == capituloId).ToListAsync(cancelacion);
        if (existentes.Any(e => e.PosicionInicio == posicion))
        {
            return Conflicto($"Ya existe una escena que empieza en la posición {posicion}.");
        }

        var nueva = new Escena
        {
            CapituloId = capituloId,
            PosicionInicio = posicion,
            FragmentoInicio = Texto.ExtraerFragmento(capitulo.Contenido, posicion),
        };
        foreach (var seleccionada in validacion.Etiquetas)
        {
            nueva.Etiquetas.Add(new EscenaEtiqueta { EtiquetaId = seleccionada.Id });
        }

        var todas = existentes.Append(nueva).OrderBy(e => e.PosicionInicio).ToList();
        for (var i = 0; i < todas.Count; i++)
        {
            todas[i].Orden = i + 1;
        }

        _db.Escenas.Add(nueva);
        await _db.SaveChangesAsync(cancelacion);

        var indice = todas.IndexOf(nueva);
        var fin = indice + 1 < todas.Count ? todas[indice + 1].PosicionInicio : capitulo.Contenido.Length;
        var etiquetas = validacion.Etiquetas.Select(Mapeos.AEtiquetaResumen).ToList();
        var dto = new EscenaDto(nueva.Id, nueva.Orden, nueva.PosicionInicio, fin, nueva.FragmentoInicio, etiquetas);

        return CreatedAtAction(nameof(Listar), new { capituloId }, dto);
    }

    /// <summary>Elimina todas las escenas de un capítulo y lo marca como no procesado.</summary>
    [HttpDelete("api/capitulos/{capituloId:int}/escenas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarTodas(int capituloId, CancellationToken cancelacion)
    {
        var capitulo = await _db.Capitulos.FirstOrDefaultAsync(c => c.Id == capituloId, cancelacion);
        if (capitulo is null)
        {
            return NoEncontrado($"No existe un capítulo con id {capituloId}.");
        }

        await using (var transaccion = await _db.Database.BeginTransactionAsync(cancelacion))
        {
            await _db.Escenas.Where(e => e.CapituloId == capituloId).ExecuteDeleteAsync(cancelacion);
            capitulo.ProcesadoIa = false;
            await _db.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }

        return NoContent();
    }

    /// <summary>Recomienda pistas para una escena según las etiquetas que comparten.</summary>
    /// <remarks>
    /// La afinidad es la cantidad de etiquetas en común. Una pista solo es compatible si
    /// comparte al menos una etiqueta distinta de "intenso".
    /// </remarks>
    /// <param name="id">Id de la escena.</param>
    /// <param name="limite">Cantidad máxima de pistas a devolver (1 a 50, por defecto 5).</param>
    [HttpGet("api/escenas/{id:int}/recomendaciones")]
    [ProducesResponseType(typeof(List<RecomendacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RecomendacionDto>>> Recomendaciones(
        int id,
        [FromQuery] int? limite,
        CancellationToken cancelacion)
    {
        var escena = await _db.Escenas
            .AsNoTracking()
            .Include(e => e.Etiquetas)
            .ThenInclude(ee => ee.Etiqueta)
            .FirstOrDefaultAsync(e => e.Id == id, cancelacion);
        if (escena is null)
        {
            return NoEncontrado($"No existe una escena con id {id}.");
        }

        var etiquetasEscena = escena.Etiquetas
            .Select(ee => ee.Etiqueta?.Nombre)
            .OfType<string>()
            .ToList();

        var pistas = await _db.Pistas
            .AsNoTracking()
            .Include(p => p.Etiquetas)
            .ThenInclude(pe => pe.Etiqueta)
            .ToListAsync(cancelacion);

        var cantidad = Math.Clamp(limite ?? LimiteRecomendacionesPorDefecto, 1, LimiteRecomendacionesMaximo);
        return Recomendador.Recomendar(etiquetasEscena, pistas, cantidad);
    }

    private Task<List<Escena>> CargarEscenasAsync(int capituloId, CancellationToken cancelacion) =>
        _db.Escenas
            .AsNoTracking()
            .Where(e => e.CapituloId == capituloId)
            .Include(e => e.Etiquetas)
            .ThenInclude(ee => ee.Etiqueta)
            .OrderBy(e => e.PosicionInicio)
            .ToListAsync(cancelacion);
}
