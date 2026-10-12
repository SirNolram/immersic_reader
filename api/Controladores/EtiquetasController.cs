using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Catálogo de etiquetas de emoción que usan las escenas y las pistas.</summary>
[ApiController]
[Route("api/etiquetas")]
[Produces("application/json")]
public class EtiquetasController : ControladorBase
{
    private readonly ImmersicDbContext _db;

    public EtiquetasController(ImmersicDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista todas las etiquetas con su descripción.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EtiquetaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EtiquetaDto>>> Listar(CancellationToken cancelacion)
    {
        var resultado = await _db.Etiquetas
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new EtiquetaDto(e.Id, e.Nombre, e.Descripcion))
            .ToListAsync(cancelacion);

        return resultado;
    }

    /// <summary>Obtiene una etiqueta por su id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EtiquetaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiquetaDto>> Obtener(int id, CancellationToken cancelacion)
    {
        var encontrada = await _db.Etiquetas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancelacion);
        if (encontrada is null)
        {
            return NoEncontrado($"No existe una etiqueta con id {id}.");
        }

        return Mapeos.AEtiquetaDto(encontrada);
    }

    /// <summary>Crea una etiqueta. El nombre se guarda en minúsculas y no se puede repetir.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EtiquetaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EtiquetaDto>> Crear(EtiquetaEntradaDto entrada, CancellationToken cancelacion)
    {
        var nombre = NormalizarNombre(entrada.Nombre);
        if (await _db.Etiquetas.AnyAsync(e => e.Nombre == nombre, cancelacion))
        {
            return Conflicto($"Ya existe una etiqueta llamada \"{nombre}\".");
        }

        var nueva = new Etiqueta
        {
            Nombre = nombre,
            Descripcion = entrada.Descripcion?.Trim() ?? string.Empty,
        };

        _db.Etiquetas.Add(nueva);
        await _db.SaveChangesAsync(cancelacion);

        return CreatedAtAction(nameof(Obtener), new { id = nueva.Id }, Mapeos.AEtiquetaDto(nueva));
    }

    /// <summary>Actualiza el nombre o la descripción de una etiqueta.</summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EtiquetaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EtiquetaDto>> Actualizar(int id, EtiquetaEntradaDto entrada, CancellationToken cancelacion)
    {
        var existente = await _db.Etiquetas.FirstOrDefaultAsync(e => e.Id == id, cancelacion);
        if (existente is null)
        {
            return NoEncontrado($"No existe una etiqueta con id {id}.");
        }

        var nombre = NormalizarNombre(entrada.Nombre);
        if (await _db.Etiquetas.AnyAsync(e => e.Nombre == nombre && e.Id != id, cancelacion))
        {
            return Conflicto($"Ya existe otra etiqueta llamada \"{nombre}\".");
        }

        existente.Nombre = nombre;
        existente.Descripcion = entrada.Descripcion?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync(cancelacion);

        return Mapeos.AEtiquetaDto(existente);
    }

    /// <summary>Elimina una etiqueta que no esté asignada a ninguna pista ni escena.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancelacion)
    {
        var existente = await _db.Etiquetas.FirstOrDefaultAsync(e => e.Id == id, cancelacion);
        if (existente is null)
        {
            return NoEncontrado($"No existe una etiqueta con id {id}.");
        }

        var enPistas = await _db.PistaEtiquetas.CountAsync(pe => pe.EtiquetaId == id, cancelacion);
        var enEscenas = await _db.EscenaEtiquetas.CountAsync(ee => ee.EtiquetaId == id, cancelacion);
        if (enPistas > 0 || enEscenas > 0)
        {
            return Conflicto(
                $"La etiqueta \"{existente.Nombre}\" está asignada a {enPistas} pista(s) y {enEscenas} escena(s). Quítala de ellas antes de eliminarla.");
        }

        _db.Etiquetas.Remove(existente);
        await _db.SaveChangesAsync(cancelacion);
        return NoContent();
    }

    private static string NormalizarNombre(string nombre) => nombre.Trim().ToLowerInvariant();
}
