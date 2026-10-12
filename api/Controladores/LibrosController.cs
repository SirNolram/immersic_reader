using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Modelos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Controladores;

/// <summary>Libros de la biblioteca.</summary>
[ApiController]
[Route("api/libros")]
[Produces("application/json")]
public class LibrosController : ControladorBase
{
    private readonly ImmersicDbContext _db;

    public LibrosController(ImmersicDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista los libros ordenados por título.</summary>
    /// <param name="buscar">Texto opcional para filtrar por título o autor.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<LibroDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LibroDto>>> Listar([FromQuery] string? buscar, CancellationToken cancelacion)
    {
        var consulta = _db.Libros.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            consulta = consulta.Where(l =>
                EF.Functions.Like(l.Titulo, patron)
                || (l.Autor != null && EF.Functions.Like(l.Autor, patron)));
        }

        var resultado = await consulta
            .OrderBy(l => l.Titulo)
            .Select(l => new LibroDto(l.Id, l.Titulo, l.Autor, l.CreadoEn, l.Capitulos.Count))
            .ToListAsync(cancelacion);

        return resultado;
    }

    /// <summary>Obtiene un libro por su id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LibroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LibroDto>> Obtener(int id, CancellationToken cancelacion)
    {
        var encontrado = await _db.Libros
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new LibroDto(l.Id, l.Titulo, l.Autor, l.CreadoEn, l.Capitulos.Count))
            .FirstOrDefaultAsync(cancelacion);

        if (encontrado is null)
        {
            return NoEncontrado($"No existe un libro con id {id}.");
        }

        return encontrado;
    }

    /// <summary>Crea un libro.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LibroDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LibroDto>> Crear(LibroEntradaDto entrada, CancellationToken cancelacion)
    {
        var nuevo = new Libro
        {
            Titulo = entrada.Titulo.Trim(),
            Autor = string.IsNullOrWhiteSpace(entrada.Autor) ? null : entrada.Autor.Trim(),
            CreadoEn = DateTime.UtcNow,
        };

        _db.Libros.Add(nuevo);
        await _db.SaveChangesAsync(cancelacion);

        var dto = new LibroDto(nuevo.Id, nuevo.Titulo, nuevo.Autor, nuevo.CreadoEn, 0);
        return CreatedAtAction(nameof(Obtener), new { id = nuevo.Id }, dto);
    }

    /// <summary>Actualiza el título y el autor de un libro.</summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LibroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LibroDto>> Actualizar(int id, LibroEntradaDto entrada, CancellationToken cancelacion)
    {
        var existente = await _db.Libros.FirstOrDefaultAsync(l => l.Id == id, cancelacion);
        if (existente is null)
        {
            return NoEncontrado($"No existe un libro con id {id}.");
        }

        existente.Titulo = entrada.Titulo.Trim();
        existente.Autor = string.IsNullOrWhiteSpace(entrada.Autor) ? null : entrada.Autor.Trim();
        await _db.SaveChangesAsync(cancelacion);

        var cantidadCapitulos = await _db.Capitulos.CountAsync(c => c.LibroId == id, cancelacion);
        return new LibroDto(existente.Id, existente.Titulo, existente.Autor, existente.CreadoEn, cantidadCapitulos);
    }

    /// <summary>Elimina un libro junto con sus capítulos y escenas.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancelacion)
    {
        var eliminados = await _db.Libros.Where(l => l.Id == id).ExecuteDeleteAsync(cancelacion);
        if (eliminados == 0)
        {
            return NoEncontrado($"No existe un libro con id {id}.");
        }

        return NoContent();
    }
}
