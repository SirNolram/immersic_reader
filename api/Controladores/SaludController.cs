using Immersic.Api.Datos;
using Immersic.Api.Dtos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Immersic.Api.Controladores;

/// <summary>Estado de la API, la base de datos y el servicio de IA.</summary>
[ApiController]
[Route("api/salud")]
[Produces("application/json")]
public class SaludController : ControllerBase
{
    private readonly ImmersicDbContext _db;
    private readonly ClienteServicioIa _servicioIa;

    public SaludController(ImmersicDbContext db, ClienteServicioIa servicioIa)
    {
        _db = db;
        _servicioIa = servicioIa;
    }

    /// <summary>Comprueba la conexión con SQLite y con el servicio de IA.</summary>
    /// <remarks>Responde 503 si no hay conexión con la base de datos.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(SaludDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SaludDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SaludDto>> Obtener(CancellationToken cancelacion)
    {
        var baseDeDatos = await _db.Database.CanConnectAsync(cancelacion);
        var servicioIa = await _servicioIa.EstaDisponibleAsync(cancelacion);

        var salud = new SaludDto(
            "ok",
            baseDeDatos ? "conectada" : "sin conexión",
            servicioIa ? "disponible" : "no disponible",
            DateTime.UtcNow);

        if (!baseDeDatos)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, salud);
        }

        return salud;
    }
}
