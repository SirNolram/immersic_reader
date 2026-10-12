using Microsoft.EntityFrameworkCore;

namespace Immersic.Api.Datos;

/// <summary>
/// Ejecuta el script database/schema.sql (embebido en el ensamblado) para
/// crear las tablas que falten y cargar el catálogo inicial de etiquetas.
/// </summary>
public static class InicializadorBaseDatos
{
    private const string RecursoEsquema = "schema.sql";

    public static async Task AplicarEsquemaAsync(ImmersicDbContext db, CancellationToken cancelacion = default)
    {
        var ensamblado = typeof(InicializadorBaseDatos).Assembly;
        await using var recurso = ensamblado.GetManifestResourceStream(RecursoEsquema)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{RecursoEsquema}'.");

        using var lector = new StreamReader(recurso);
        var script = await lector.ReadToEndAsync(cancelacion);

        var conexion = db.Database.GetDbConnection();
        await conexion.OpenAsync(cancelacion);
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = script;
            await comando.ExecuteNonQueryAsync(cancelacion);
        }
        finally
        {
            await conexion.CloseAsync();
        }
    }
}
