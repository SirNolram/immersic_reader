using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Capítulos de cada libro.</summary>
[ApiController]
[Produces("application/json")]
public class CapitulosController : ControladorBase
{
    private readonly ImmersicDbContext _db;

    public CapitulosController(ImmersicDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista los capítulos de un libro, ordenados por número y sin su contenido.</summary>
    [HttpGet("api/libros/{libroId:int}/capitulos")]
    [ProducesResponseType(typeof(List<CapituloResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CapituloResumenDto>>> Listar(int libroId, CancellationToken cancelacion)
    {
        if (!await _db.Libros.AnyAsync(l => l.Id == libroId, cancelacion))
        {
            return NoEncontrado($"No existe un libro con id {libroId}.");
        }

        var resultado = await _db.Capitulos
            .AsNoTracking()
            .Where(c => c.LibroId == libroId)
            .OrderBy(c => c.Numero)
            .Select(c => new CapituloResumenDto(
                c.Id,
                c.LibroId,
                c.Numero,
                c.Titulo,
                c.Contenido.Length,
                c.ProcesadoIa,
                c.Escenas.Count,
                c.CreadoEn))
            .ToListAsync(cancelacion);

        return resultado;
    }

    /// <summary>Crea un capítulo dentro de un libro.</summary>
    /// <remarks>Si no se envía el número, se asigna el siguiente disponible.</remarks>
    [HttpPost("api/libros/{libroId:int}/capitulos")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CapituloDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CapituloDto>> Crear(int libroId, CapituloEntradaDto entrada, CancellationToken cancelacion)
    {
        if (!await _db.Libros.AnyAsync(l => l.Id == libroId, cancelacion))
        {
            return NoEncontrado($"No existe un libro con id {libroId}.");
        }

        int numero;
        if (entrada.Numero is int solicitado)
        {
            var ocupado = await _db.Capitulos.AnyAsync(c => c.LibroId == libroId && c.Numero == solicitado, cancelacion);
            if (ocupado)
            {
                return Conflicto($"El libro ya tiene un capítulo con el número {solicitado}.");
            }

            numero = solicitado;
        }
        else
        {
            var ultimo = await _db.Capitulos
                .Where(c => c.LibroId == libroId)
                .MaxAsync(c => (int?)c.Numero, cancelacion);
            numero = (ultimo ?? 0) + 1;
        }

        var nuevo = new Capitulo
        {
            LibroId = libroId,
            Numero = numero,
            Titulo = entrada.Titulo.Trim(),
            Contenido = entrada.Contenido ?? string.Empty,
            ProcesadoIa = false,
            CreadoEn = DateTime.UtcNow,
        };

        _db.Capitulos.Add(nuevo);
        await _db.SaveChangesAsync(cancelacion);

        return CreatedAtAction(nameof(Obtener), new { id = nuevo.Id }, Mapeos.ACapituloDto(nuevo, 0));
    }

    /// <summary>Obtiene un capítulo con su contenido completo.</summary>
    [HttpGet("api/capitulos/{id:int}")]
    [ProducesResponseType(typeof(CapituloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CapituloDto>> Obtener(int id, CancellationToken cancelacion)
    {
        var encontrado = await _db.Capitulos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancelacion);
        if (encontrado is null)
        {
            return NoEncontrado($"No existe un capítulo con id {id}.");
        }

        var cantidadEscenas = await _db.Escenas.CountAsync(e => e.CapituloId == id, cancelacion);
        return Mapeos.ACapituloDto(encontrado, cantidadEscenas);
    }

    /// <summary>Actualiza el título, el contenido o el número de un capítulo.</summary>
    /// <remarks>
    /// Si el contenido cambia, se borran las escenas del capítulo porque sus posiciones
    /// ya no coinciden con el texto, y hay que volver a generar la inmersión.
    /// </remarks>
    [HttpPut("api/capitulos/{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CapituloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CapituloDto>> Actualizar(int id, CapituloEntradaDto entrada, CancellationToken cancelacion)
    {
        var existente = await _db.Capitulos.FirstOrDefaultAsync(c => c.Id == id, cancelacion);
        if (existente is null)
        {
            return NoEncontrado($"No existe un capítulo con id {id}.");
        }

        if (entrada.Numero is int numero && numero != existente.Numero)
        {
            var libroId = existente.LibroId;
            var ocupado = await _db.Capitulos.AnyAsync(c => c.LibroId == libroId && c.Numero == numero && c.Id != id, cancelacion);
            if (ocupado)
            {
                return Conflicto($"El libro ya tiene un capítulo con el número {numero}.");
            }

            existente.Numero = numero;
        }

        existente.Titulo = entrada.Titulo.Trim();

        var contenidoCambio = entrada.Contenido is not null && entrada.Contenido != existente.Contenido;
        if (contenidoCambio)
        {
            existente.Contenido = entrada.Contenido!;
            existente.ProcesadoIa = false;
        }

        await using (var transaccion = await _db.Database.BeginTransactionAsync(cancelacion))
        {
            if (contenidoCambio)
            {
                await _db.Escenas.Where(e => e.CapituloId == id).ExecuteDeleteAsync(cancelacion);
            }

            await _db.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }

        var cantidadEscenas = await _db.Escenas.CountAsync(e => e.CapituloId == id, cancelacion);
        return Mapeos.ACapituloDto(existente, cantidadEscenas);
    }

    /// <summary>Elimina un capítulo y sus escenas.</summary>
    [HttpDelete("api/capitulos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancelacion)
    {
        var eliminados = await _db.Capitulos.Where(c => c.Id == id).ExecuteDeleteAsync(cancelacion);
        if (eliminados == 0)
        {
            return NoEncontrado($"No existe un capítulo con id {id}.");
        }

        return NoContent();
    }
}
