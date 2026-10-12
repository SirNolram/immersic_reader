using System.Reflection;
using Immersic.Api.Datos;
using Immersic.Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = ConstruirCadenaConexion(
    builder.Configuration.GetConnectionString("Immersic"),
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<ImmersicDbContext>(opciones => opciones.UseSqlite(cadenaConexion));

builder.Services.AddHttpClient<ClienteServicioIa>(cliente =>
{
    var urlBase = builder.Configuration["ServicioIa:UrlBase"] ?? "http://localhost:3001/";
    cliente.BaseAddress = new Uri(urlBase.EndsWith('/') ? urlBase : urlBase + "/");
    cliente.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("ServicioIa:TiempoEsperaSegundos", 180));
});

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
            var fabrica = contexto.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var problema = fabrica.CreateValidationProblemDetails(
                contexto.HttpContext,
                contexto.ModelState,
                StatusCodes.Status400BadRequest,
                "La solicitud tiene datos inválidos.");

            return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Immersic Reader API",
        Version = "v1",
        Description = "API REST de Immersic Reader: libros, capítulos, etiquetas de emoción, pistas de música "
            + "y generación de escenas con Gemini usando la API key de cada usuario.",
    });

    var archivoXml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(archivoXml))
    {
        opciones.IncludeXmlComments(archivoXml, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

using (var alcance = app.Services.CreateScope())
{
    var db = alcance.ServiceProvider.GetRequiredService<ImmersicDbContext>();
    await InicializadorBaseDatos.AplicarEsquemaAsync(db);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(opciones =>
{
    opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "Immersic Reader API v1");
    opciones.DocumentTitle = "Immersic Reader API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

static string ConstruirCadenaConexion(string? configurada, string rutaContenido)
{
    var constructor = new SqliteConnectionStringBuilder(
        string.IsNullOrWhiteSpace(configurada) ? "Data Source=immersic.db" : configurada);

    var archivo = constructor.DataSource;
    if (!string.IsNullOrWhiteSpace(archivo)
        && !archivo.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
        && !Path.IsPathRooted(archivo))
    {
        constructor.DataSource = Path.Combine(rutaContenido, archivo);
    }

    constructor.ForeignKeys = true;
    return constructor.ToString();
}
