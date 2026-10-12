import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import { ErrorServicio, solicitudInvalida } from './errores.js';
import { ubicarEscenas } from './escenas.js';
import type { ClienteGemini, EtiquetaCatalogo } from './tipos.js';

export interface OpcionesServidor {
  gemini: ClienteGemini;
  maxCaracteres: number;
  registrar?: (linea: string) => void;
}

interface SolicitudValida {
  texto: string;
  etiquetas: EtiquetaCatalogo[];
}

const LIMITE_CUERPO_BYTES = 1_048_576;
const MAX_ETIQUETAS = 50;
const MAX_LONGITUD_NOMBRE = 30;
const MAX_LONGITUD_DESCRIPCION = 300;

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

function enviarJson(
  respuesta: ServerResponse,
  estado: number,
  cuerpo: unknown,
  encabezados: Record<string, string> = {},
): void {
  const json = JSON.stringify(cuerpo);
  respuesta.writeHead(estado, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(json).toString(),
    ...encabezados,
  });
  respuesta.end(json);
}

function obtenerRuta(url: string | undefined): string {
  try {
    const ruta = new URL(url ?? '/', 'http://localhost').pathname;
    return ruta.replace(/\/+$/, '') || '/';
  } catch {
    return '/';
  }
}

function leerEncabezado(solicitud: IncomingMessage, nombre: string): string | undefined {
  const valor = solicitud.headers[nombre];
  const texto = Array.isArray(valor) ? valor[0] : valor;
  return texto?.trim() || undefined;
}

async function leerCuerpo(solicitud: IncomingMessage): Promise<string> {
  const partes: Buffer[] = [];
  let total = 0;
  for await (const parte of solicitud) {
    const buffer = parte as Buffer;
    total += buffer.length;
    if (total > LIMITE_CUERPO_BYTES) {
      throw new ErrorServicio(413, 'CUERPO_DEMASIADO_GRANDE', 'La solicitud supera el tamaño permitido.');
    }
    partes.push(buffer);
  }
  return Buffer.concat(partes).toString('utf8');
}

function validarSolicitud(datos: unknown, maxCaracteres: number): SolicitudValida {
  if (!esObjeto(datos)) {
    throw solicitudInvalida('El cuerpo debe ser un objeto JSON con "texto" y "etiquetas".');
  }

  const { texto, etiquetas } = datos;
  if (typeof texto !== 'string' || texto.trim().length === 0) {
    throw solicitudInvalida('"texto" es obligatorio y no puede estar vacío.');
  }
  if (texto.length > maxCaracteres) {
    throw new ErrorServicio(
      400,
      'TEXTO_DEMASIADO_LARGO',
      `"texto" supera el máximo de ${maxCaracteres} caracteres.`,
    );
  }
  if (!Array.isArray(etiquetas) || etiquetas.length === 0 || etiquetas.length > MAX_ETIQUETAS) {
    throw solicitudInvalida(`"etiquetas" debe ser una lista de 1 a ${MAX_ETIQUETAS} elementos.`);
  }

  const catalogo: EtiquetaCatalogo[] = [];
  for (const etiqueta of etiquetas) {
    const nombre = esObjeto(etiqueta) && typeof etiqueta.nombre === 'string' ? etiqueta.nombre.trim().toLowerCase() : '';
    if (nombre.length === 0 || nombre.length > MAX_LONGITUD_NOMBRE) {
      throw solicitudInvalida(`Cada etiqueta necesita un "nombre" de 1 a ${MAX_LONGITUD_NOMBRE} caracteres.`);
    }
    const descripcion =
      esObjeto(etiqueta) && typeof etiqueta.descripcion === 'string'
        ? etiqueta.descripcion.slice(0, MAX_LONGITUD_DESCRIPCION)
        : '';
    if (!catalogo.some((existente) => existente.nombre === nombre)) {
      catalogo.push({ nombre, descripcion });
    }
  }

  return { texto, etiquetas: catalogo };
}

async function analizar(
  solicitud: IncomingMessage,
  respuesta: ServerResponse,
  opciones: OpcionesServidor,
): Promise<void> {
  const claveApi = leerEncabezado(solicitud, 'x-gemini-api-key');
  if (!claveApi) {
    throw new ErrorServicio(400, 'FALTA_API_KEY', 'Falta el header X-Gemini-Api-Key con la API key de Gemini.');
  }

  let datos: unknown;
  try {
    datos = JSON.parse(await leerCuerpo(solicitud));
  } catch (error) {
    if (error instanceof ErrorServicio) {
      throw error;
    }
    throw solicitudInvalida('El cuerpo de la solicitud no es un JSON válido.');
  }

  const { texto, etiquetas } = validarSolicitud(datos, opciones.maxCaracteres);
  const propuestas = await opciones.gemini.proponerEscenas({ texto, etiquetas, claveApi });
  const permitidas = new Set(etiquetas.map((etiqueta) => etiqueta.nombre));
  const escenas = ubicarEscenas(texto, propuestas, permitidas);

  if (escenas.length === 0) {
    throw new ErrorServicio(502, 'SIN_ESCENAS', 'Gemini no devolvió escenas que se pudieran ubicar en el texto.');
  }

  enviarJson(respuesta, 200, { modelo: opciones.gemini.modelo, escenas });
}

async function atender(
  solicitud: IncomingMessage,
  respuesta: ServerResponse,
  ruta: string,
  opciones: OpcionesServidor,
): Promise<void> {
  if (ruta === '/salud') {
    if (solicitud.method !== 'GET') {
      enviarJson(respuesta, 405, { codigo: 'METODO_NO_PERMITIDO', mensaje: 'Esta ruta solo acepta GET.' }, { allow: 'GET' });
      return;
    }
    enviarJson(respuesta, 200, { estado: 'ok', modelo: opciones.gemini.modelo });
    return;
  }

  if (ruta === '/api/analizar') {
    if (solicitud.method !== 'POST') {
      enviarJson(respuesta, 405, { codigo: 'METODO_NO_PERMITIDO', mensaje: 'Esta ruta solo acepta POST.' }, { allow: 'POST' });
      return;
    }
    await analizar(solicitud, respuesta, opciones);
    return;
  }

  enviarJson(respuesta, 404, { codigo: 'RUTA_NO_ENCONTRADA', mensaje: 'La ruta solicitada no existe.' });
}

export function crearServidor(opciones: OpcionesServidor): Server {
  const registrar = opciones.registrar ?? ((linea: string) => console.log(linea));

  return createServer((solicitud, respuesta) => {
    const inicio = Date.now();
    const ruta = obtenerRuta(solicitud.url);
    respuesta.on('finish', () => {
      registrar(`${solicitud.method} ${ruta} -> ${respuesta.statusCode} (${Date.now() - inicio} ms)`);
    });

    atender(solicitud, respuesta, ruta, opciones).catch((error: unknown) => {
      if (respuesta.headersSent) {
        respuesta.end();
        return;
      }
      if (error instanceof ErrorServicio) {
        enviarJson(respuesta, error.estado, { codigo: error.codigo, mensaje: error.message });
        return;
      }
      console.error(error);
      enviarJson(respuesta, 500, { codigo: 'ERROR_INTERNO', mensaje: 'Ocurrió un error inesperado en el servicio de IA.' });
    });
  });
}
