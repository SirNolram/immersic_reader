using System.Net.Http.Json;
using System.Text.Json;
using Immersic.Api.Modelos;

namespace Immersic.Api.Servicios;

/// <summary>Escena propuesta por el servicio de IA, ya ubicada en el texto.</summary>
public sealed record EscenaAnalizada(int Orden, int PosicionInicio, string FragmentoInicio, List<string> Etiquetas);

/// <summary>Respuesta del servicio de IA.</summary>
public sealed record ResultadoAnalisis(string Modelo, List<EscenaAnalizada> Escenas);

/// <summary>Error del servicio de IA traducido a un código HTTP para el cliente.</summary>
public sealed class ExcepcionServicioIa : Exception
{
    public ExcepcionServicioIa(int estado, string titulo, string detalle)
        : base(detalle)
    {
        Estado = estado;
        Titulo = titulo;
    }

    public int Estado { get; }

    public string Titulo { get; }
}

/// <summary>Cliente HTTP del servicio de IA (Node.js), que a su vez consulta Gemini.</summary>
public class ClienteServicioIa
{
    public const string EncabezadoApiKey = "X-Gemini-Api-Key";

    private static readonly int[] EstadosQueSeConservan = { 400, 401, 403, 422, 429, 502, 504 };

    private readonly HttpClient _http;
    private readonly ILogger<ClienteServicioIa> _logger;

    public ClienteServicioIa(HttpClient http, ILogger<ClienteServicioIa> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ResultadoAnalisis> AnalizarAsync(
        string texto,
        IEnumerable<Etiqueta> etiquetas,
        string claveGemini,
        CancellationToken cancelacion)
    {
        var cuerpo = new
        {
            texto,
            etiquetas = etiquetas
                .Select(etiqueta => new { nombre = etiqueta.Nombre, descripcion = etiqueta.Descripcion })
                .ToList(),
        };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "api/analizar")
        {
            Content = JsonContent.Create(cuerpo),
        };
        solicitud.Headers.TryAddWithoutValidation(EncabezadoApiKey, claveGemini);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(solicitud, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar con el servicio de IA en {Url}.", _http.BaseAddress);
            throw new ExcepcionServicioIa(
                StatusCodes.Status503ServiceUnavailable,
                "Servicio de IA no disponible",
                "No se pudo conectar con el servicio de IA. Verifica que esté en ejecución.");
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            throw new ExcepcionServicioIa(
                StatusCodes.Status504GatewayTimeout,
                "Tiempo de espera agotado",
                "El servicio de IA tardó demasiado en responder.");
        }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                ResultadoAnalisis? resultado = null;
                try
                {
                    resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoAnalisis>(cancellationToken: cancelacion);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "El servicio de IA devolvió un JSON inválido.");
                }

                if (resultado?.Escenas is null)
                {
                    throw new ExcepcionServicioIa(
                        StatusCodes.Status502BadGateway,
                        "Respuesta inválida del servicio de IA",
                        "El servicio de IA devolvió una respuesta que no se pudo interpretar.");
                }

                return resultado;
            }

            var estado = (int)respuesta.StatusCode;
            var estadoFinal = EstadosQueSeConservan.Contains(estado) ? estado : StatusCodes.Status502BadGateway;
            var mensaje = await LeerMensajeErrorAsync(respuesta, cancelacion);
            throw new ExcepcionServicioIa(
                estadoFinal,
                TituloPara(estadoFinal),
                mensaje ?? "El servicio de IA respondió con un error.");
        }
    }

    public async Task<bool> EstaDisponibleAsync(CancellationToken cancelacion)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var respuesta = await _http.GetAsync("salud", limite.Token);
            return respuesta.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<string?> LeerMensajeErrorAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<ErrorServicioIa>(cancellationToken: cancelacion);
            var mensaje = error?.Mensaje;
            return string.IsNullOrWhiteSpace(mensaje) ? null : mensaje;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string TituloPara(int estado) => estado switch
    {
        StatusCodes.Status400BadRequest => "Solicitud rechazada por el servicio de IA",
        StatusCodes.Status401Unauthorized => "API key de Gemini inválida",
        StatusCodes.Status403Forbidden => "API key de Gemini sin permiso",
        StatusCodes.Status422UnprocessableEntity => "Contenido bloqueado por Gemini",
        StatusCodes.Status429TooManyRequests => "Cuota de Gemini agotada",
        StatusCodes.Status504GatewayTimeout => "Tiempo de espera agotado",
        _ => "Error en el servicio de IA",
    };

    private sealed record ErrorServicioIa(string? Codigo, string? Mensaje);
}
