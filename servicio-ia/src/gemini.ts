import { ErrorServicio } from './errores.js';
import { construirContenidoUsuario, construirEsquemaRespuesta, construirInstruccionSistema } from './prompt.js';
import type { ClienteGemini, EscenaPropuesta, SolicitudAnalisis } from './tipos.js';

export interface OpcionesGemini {
  urlBase: string;
  modelo: string;
  tiempoEsperaMs: number;
}

interface ParteGemini {
  text?: unknown;
  thought?: unknown;
}

interface RespuestaGemini {
  candidates?: Array<{
    content?: { parts?: ParteGemini[] };
    finishReason?: string;
  }>;
  promptFeedback?: { blockReason?: string };
}

interface ErrorGemini {
  error?: {
    message?: unknown;
    details?: Array<{ reason?: unknown }>;
  };
}

const RAZONES_BLOQUEO = new Set([
  'SAFETY',
  'RECITATION',
  'BLOCKLIST',
  'PROHIBITED_CONTENT',
  'SPII',
  'IMAGE_SAFETY',
]);

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

function traducirError(estado: number, datos: unknown, modelo: string): ErrorServicio {
  const error = esObjeto(datos) ? (datos as ErrorGemini).error : undefined;
  const mensaje = typeof error?.message === 'string' ? error.message : '';
  const razones = Array.isArray(error?.details) ? error.details.map((detalle) => detalle?.reason) : [];

  if (estado === 400 && (razones.includes('API_KEY_INVALID') || /api key not valid/i.test(mensaje))) {
    return new ErrorServicio(401, 'API_KEY_INVALIDA', 'La API key de Gemini no es válida.');
  }
  if (estado === 401 || estado === 403) {
    return new ErrorServicio(403, 'API_KEY_SIN_PERMISO', 'La API key de Gemini no tiene permiso para usar este modelo.');
  }
  if (estado === 404) {
    return new ErrorServicio(502, 'MODELO_NO_ENCONTRADO', `El modelo "${modelo}" no está disponible en Gemini.`);
  }
  if (estado === 429) {
    return new ErrorServicio(429, 'CUOTA_AGOTADA', 'Se agotó la cuota de la API key de Gemini. Intenta más tarde.');
  }
  if (estado >= 400 && estado < 500) {
    const detalle = mensaje ? `: ${mensaje}` : '.';
    return new ErrorServicio(502, 'SOLICITUD_RECHAZADA', `Gemini rechazó la solicitud${detalle}`);
  }
  return new ErrorServicio(502, 'GEMINI_NO_DISPONIBLE', 'Gemini no está disponible en este momento.');
}

function leerEscenas(datos: unknown): EscenaPropuesta[] {
  const respuesta = (esObjeto(datos) ? datos : {}) as RespuestaGemini;

  const bloqueo = respuesta.promptFeedback?.blockReason;
  if (bloqueo) {
    throw new ErrorServicio(422, 'CONTENIDO_BLOQUEADO', `Gemini bloqueó el capítulo (${bloqueo}).`);
  }

  const candidato = respuesta.candidates?.[0];
  if (!candidato) {
    throw new ErrorServicio(502, 'RESPUESTA_INVALIDA', 'Gemini no devolvió resultados.');
  }

  const razon = candidato.finishReason;
  if (razon === 'MAX_TOKENS') {
    throw new ErrorServicio(502, 'RESPUESTA_INCOMPLETA', 'La respuesta de Gemini quedó incompleta.');
  }
  if (razon && RAZONES_BLOQUEO.has(razon)) {
    throw new ErrorServicio(422, 'CONTENIDO_BLOQUEADO', `Gemini bloqueó el capítulo (${razon}).`);
  }
  if (razon && razon !== 'STOP' && razon !== 'FINISH_REASON_UNSPECIFIED') {
    throw new ErrorServicio(502, 'RESPUESTA_INVALIDA', `Gemini terminó la respuesta de forma inesperada (${razon}).`);
  }

  const texto = (candidato.content?.parts ?? [])
    .filter((parte) => typeof parte.text === 'string' && parte.thought !== true)
    .map((parte) => parte.text as string)
    .join('');

  let json: unknown;
  try {
    json = JSON.parse(texto);
  } catch {
    throw new ErrorServicio(502, 'RESPUESTA_INVALIDA', 'Gemini devolvió un JSON inválido.');
  }

  const escenas = esObjeto(json) ? json.escenas : undefined;
  if (!Array.isArray(escenas)) {
    throw new ErrorServicio(502, 'RESPUESTA_INVALIDA', 'La respuesta de Gemini no contiene escenas.');
  }

  const propuestas: EscenaPropuesta[] = [];
  for (const escena of escenas) {
    if (!esObjeto(escena) || typeof escena.fragmento_inicio !== 'string' || !Array.isArray(escena.etiquetas)) {
      continue;
    }
    propuestas.push({
      fragmentoInicio: escena.fragmento_inicio,
      etiquetas: escena.etiquetas.filter((etiqueta): etiqueta is string => typeof etiqueta === 'string'),
    });
  }
  return propuestas;
}

export function crearClienteGemini(opciones: OpcionesGemini): ClienteGemini {
  const url = `${opciones.urlBase}/models/${encodeURIComponent(opciones.modelo)}:generateContent`;

  return {
    modelo: opciones.modelo,

    async proponerEscenas(solicitud: SolicitudAnalisis): Promise<EscenaPropuesta[]> {
      const cuerpo = {
        systemInstruction: { parts: [{ text: construirInstruccionSistema(solicitud.etiquetas) }] },
        contents: [{ role: 'user', parts: [{ text: construirContenidoUsuario(solicitud.texto) }] }],
        generationConfig: {
          responseMimeType: 'application/json',
          responseSchema: construirEsquemaRespuesta(solicitud.etiquetas.map((etiqueta) => etiqueta.nombre)),
        },
      };

      let respuesta: Response;
      try {
        respuesta = await fetch(url, {
          method: 'POST',
          headers: {
            'content-type': 'application/json',
            'x-goog-api-key': solicitud.claveApi,
          },
          body: JSON.stringify(cuerpo),
          signal: AbortSignal.timeout(opciones.tiempoEsperaMs),
        });
      } catch (error) {
        if (error instanceof Error && (error.name === 'TimeoutError' || error.name === 'AbortError')) {
          throw new ErrorServicio(504, 'TIEMPO_AGOTADO', 'Gemini tardó demasiado en responder.');
        }
        throw new ErrorServicio(502, 'GEMINI_NO_DISPONIBLE', 'No se pudo conectar con Gemini.');
      }

      let datos: unknown;
      try {
        datos = JSON.parse(await respuesta.text());
      } catch (error) {
        if (error instanceof Error && (error.name === 'TimeoutError' || error.name === 'AbortError')) {
          throw new ErrorServicio(504, 'TIEMPO_AGOTADO', 'Gemini tardó demasiado en responder.');
        }
        datos = undefined;
      }

      if (!respuesta.ok) {
        throw traducirError(respuesta.status, datos, opciones.modelo);
      }
      return leerEscenas(datos);
    },
  };
}
