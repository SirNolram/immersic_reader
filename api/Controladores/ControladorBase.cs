using Microsoft.AspNetCore.Mvc;

namespace Immersic.Api.Controladores;

public abstract class ControladorBase : ControllerBase
{
    protected const string TituloDatosInvalidos = "La solicitud tiene datos inválidos.";

    protected ObjectResult NoEncontrado(string detalle) =>
        Problem(detail: detalle, statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    protected ObjectResult Conflicto(string detalle) =>
        Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict, title: "Conflicto con los datos existentes");

    protected ActionResult DatosInvalidos(string campo, string mensaje)
    {
        ModelState.AddModelError(campo, mensaje);
        return ValidationProblem(title: TituloDatosInvalidos, modelStateDictionary: ModelState);
    }
}
