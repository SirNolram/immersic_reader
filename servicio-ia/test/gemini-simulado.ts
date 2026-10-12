import { createServer, type IncomingHttpHeaders } from 'node:http';
import type { AddressInfo } from 'node:net';

export interface SolicitudRecibida {
  metodo: string;
  ruta: string;
  encabezados: IncomingHttpHeaders;
  cuerpo: unknown;
}

export interface RespuestaSimulada {
  estado: number;
  cuerpo: unknown;
  demoraMs?: number;
}

export type Responder = (solicitud: SolicitudRecibida) => RespuestaSimulada;

export interface GeminiSimulado {
  url: string;
  solicitudes: SolicitudRecibida[];
  cerrar(): Promise<void>;
}

interface CuerpoGenerateContent {
  contents?: Array<{ parts?: Array<{ text?: string }> }>;
  generationConfig?: {
    responseSchema?: {
      properties?: {
        escenas?: { items?: { properties?: { etiquetas?: { items?: { enum?: string[] } } } } };
      };
    };
  };
}

export async function iniciarGeminiSimulado(responder: Responder, puerto = 0): Promise<GeminiSimulado> {
  const solicitudes: SolicitudRecibida[] = [];

  const servidor = createServer(async (solicitud, respuesta) => {
    const partes: Buffer[] = [];
    for await (const parte of solicitud) {
      partes.push(parte as Buffer);
    }
    const texto = Buffer.concat(partes).toString('utf8');
    let cuerpo: unknown;
    try {
      cuerpo = JSON.parse(texto);
    } catch {
      cuerpo = texto;
    }

    const recibida: SolicitudRecibida = {
      metodo: solicitud.method ?? '',
      ruta: solicitud.url ?? '',
      encabezados: solicitud.headers,
      cuerpo,
    };
    solicitudes.push(recibida);

    const simulada = responder(recibida);
    const enviar = (): void => {
      if (respuesta.destroyed) {
        return;
      }
      respuesta.writeHead(simulada.estado, { 'content-type': 'application/json' });
      respuesta.end(JSON.stringify(simulada.cuerpo));
    };

    if (simulada.demoraMs) {
      setTimeout(enviar, simulada.demoraMs);
    } else {
      enviar();
    }
  });

  await new Promise<void>((resolver) => servidor.listen(puerto, '127.0.0.1', resolver));
  const { port } = servidor.address() as AddressInfo;

  return {
    url: `http://127.0.0.1:${port}/v1beta`,
    solicitudes,
    cerrar: () =>
      new Promise<void>((resolver) => {
        servidor.closeAllConnections();
        servidor.close(() => resolver());
      }),
  };
}

export function respuestaConEscenas(
  escenas: Array<{ fragmento_inicio: string; etiquetas: string[] }>,
  razon = 'STOP',
): RespuestaSimulada {
  return {
    estado: 200,
    cuerpo: {
      candidates: [
        {
          content: { role: 'model', parts: [{ text: JSON.stringify({ escenas }) }] },
          finishReason: razon,
        },
      ],
    },
  };
}

/**
 * Genera una respuesta coherente con cualquier capítulo: una escena por
 * párrafo, rotando las etiquetas recibidas en el esquema.
 */
export function responderPorParrafos(solicitud: SolicitudRecibida): RespuestaSimulada {
  const cuerpo = solicitud.cuerpo as CuerpoGenerateContent;
  const contenido = cuerpo.contents?.[0]?.parts?.[0]?.text ?? '';
  const capitulo = contenido.replace(/^<capitulo>\n/u, '').replace(/\n<\/capitulo>$/u, '');
  const enumEtiquetas =
    cuerpo.generationConfig?.responseSchema?.properties?.escenas?.items?.properties?.etiquetas?.items?.enum ?? [];
  const principales = enumEtiquetas.filter((nombre) => nombre !== 'intenso');
  const disponibles = principales.length > 0 ? principales : ['calma'];

  const escenas = capitulo
    .split(/\n\s*\n/u)
    .map((parrafo) => parrafo.trim())
    .filter((parrafo) => parrafo.length > 0)
    .map((parrafo, indice) => ({
      fragmento_inicio: parrafo.split(/\s+/u).slice(0, 10).join(' '),
      etiquetas: [disponibles[indice % disponibles.length] ?? 'calma'],
    }));

  return respuestaConEscenas(escenas);
}
