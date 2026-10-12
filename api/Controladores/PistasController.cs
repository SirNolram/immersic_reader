using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Pistas de música etiquetadas por emoción.</summary>
[ApiController]
[Route("api/pistas")]
[Produces("application/json")]
public class PistasController : ControladorBase
{
    private readonly ImmersicDbContext _db;

    public PistasController(ImmersicDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista las pistas, con filtros opcionales por texto y por etiqueta.</summary>
    /// <param name="buscar">Texto a buscar en el nombre o el artista.</param>
    /// <param name="etiqueta">Nombre de una etiqueta (por ejemplo "calma") para ver solo las pistas que la tienen.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<PistaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PistaDto>>> Listar(
        [FromQuery] string? buscar,
        [FromQuery] string? etiqueta,
        CancellationToken cancelacion)
    {
        var consulta = _db.Pistas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nombre, patron)
                || (p.Artista != null && EF.Functions.Like(p.Artista, patron)));
        }

        if (!string.IsNullOrWhiteSpace(etiqueta))
        {
            var nombreEtiqueta = etiqueta.Trim().ToLowerInvariant();
            consulta = consulta.Where(p => p.Etiquetas.Any(pe => pe.Etiqueta!.Nombre == nombreEtiqueta));
        }

        var pistas = await consulta
            .Include(p => p.Etiquetas)
            .ThenInclude(pe => pe.Etiqueta)
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Id)
            .ToListAsync(cancelacion);

        return pistas.Select(p => Mapeos.APistaDto(p)).ToList();
    }

    /// <summary>Obtiene una pista por su id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PistaDto>> Obtener(int id, CancellationToken cancelacion)
    {
        var encontrada = await BuscarConEtiquetasAsync(id, cancelacion);
        if (encontrada is null)
        {
            return NoEncontrado($"No existe una pista con id {id}.");
        }

        return Mapeos.APistaDto(encontrada);
    }

    /// <summary>Registra una pista con 1 a 3 etiquetas.</summary>
    /// <remarks>La etiqueta "intenso" no puede ser la única etiqueta de una pista.</remarks>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PistaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PistaDto>> Crear(PistaEntradaDto entrada, CancellationToken cancelacion)
    {
        var validacion = await ReglasEtiquetas.ValidarAsync(_db, entrada.EtiquetaIds, cancelacion);
        if (!validacion.EsValido)
        {
            return DatosInvalidos(nameof(PistaEntradaDto.EtiquetaIds), validacion.Error!);
        }

        var nueva = new Pista
        {
            Nombre = entrada.Nombre.Trim(),
            Artista = Limpiar(entrada.Artista),
            ArchivoUrl = Limpiar(entrada.ArchivoUrl),
        };

        foreach (var seleccionada in validacion.Etiquetas)
        {
            nueva.Etiquetas.Add(new PistaEtiqueta { EtiquetaId = seleccionada.Id });
        }

        _db.Pistas.Add(nueva);
        await _db.SaveChangesAsync(cancelacion);

        return CreatedAtAction(nameof(Obtener), new { id = nueva.Id }, Mapeos.APistaDto(nueva, validacion.Etiquetas));
    }

    /// <summary>Actualiza los datos y las etiquetas de una pista.</summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PistaDto>> Actualizar(int id, PistaEntradaDto entrada, CancellationToken cancelacion)
    {
        var existente = await _db.Pistas
            .Include(p => p.Etiquetas)
            .FirstOrDefaultAsync(p => p.Id == id, cancelacion);
        if (existente is null)
        {
            return NoEncontrado($"No existe una pista con id {id}.");
        }

        var validacion = await ReglasEtiquetas.ValidarAsync(_db, entrada.EtiquetaIds, cancelacion);
        if (!validacion.EsValido)
        {
            return DatosInvalidos(nameof(PistaEntradaDto.EtiquetaIds), validacion.Error!);
        }

        existente.Nombre = entrada.Nombre.Trim();
        existente.Artista = Limpiar(entrada.Artista);
        existente.ArchivoUrl = Limpiar(entrada.ArchivoUrl);

        var idsNuevos = validacion.Etiquetas.Select(e => e.Id).ToHashSet();
        var idsActuales = existente.Etiquetas.Select(pe => pe.EtiquetaId).ToHashSet();

        var sobrantes = existente.Etiquetas.Where(pe => !idsNuevos.Contains(pe.EtiquetaId)).ToList();
        _db.PistaEtiquetas.RemoveRange(sobrantes);

        foreach (var idEtiqueta in idsNuevos.Where(nuevoId => !idsActuales.Contains(nuevoId)))
        {
            _db.PistaEtiquetas.Add(new PistaEtiqueta { PistaId = id, EtiquetaId = idEtiqueta });
        }

        await _db.SaveChangesAsync(cancelacion);

        return Mapeos.APistaDto(existente, validacion.Etiquetas);
    }

    /// <summary>Elimina una pista.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancelacion)
    {
        var eliminadas = await _db.Pistas.Where(p => p.Id == id).ExecuteDeleteAsync(cancelacion);
        if (eliminadas == 0)
        {
            return NoEncontrado($"No existe una pista con id {id}.");
        }

        return NoContent();
    }

    private Task<Pista?> BuscarConEtiquetasAsync(int id, CancellationToken cancelacion) =>
        _db.Pistas
            .AsNoTracking()
            .Include(p => p.Etiquetas)
            .ThenInclude(pe => pe.Etiqueta)
            .FirstOrDefaultAsync(p => p.Id == id, cancelacion);

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
