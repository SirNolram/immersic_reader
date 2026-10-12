import type { EscenaPropuesta, EscenaUbicada } from './tipos.js';

export const ETIQUETA_MODIFICADORA = 'intenso';
export const MAX_ETIQUETAS_POR_ESCENA = 3;

const LONGITUD_FRAGMENTO = 80;
const MIN_PALABRAS_PREFIJO = 4;
const MIN_CARACTERES_PREFIJO = 12;
const MAX_RETROCESO_PUNTUACION = 5;

const REEMPLAZOS: ReadonlyArray<readonly [RegExp, string]> = [
  [/[‘’‚‛′´`]/u, "'"],
  [/[“”„‟«»″]/u, '"'],
  [/[‐-―−]/u, '-'],
  [/…/u, '...'],
];

export interface TextoNormalizado {
  texto: string;
  origen: number[];
}

function normalizarCaracter(caracter: string): string {
  if (/\s/u.test(caracter)) {
    return ' ';
  }
  for (const [patron, reemplazo] of REEMPLAZOS) {
    if (patron.test(caracter)) {
      return reemplazo;
    }
  }
  return caracter.normalize('NFD').replace(/\p{M}+/gu, '').toLowerCase();
}

/**
 * Normaliza un texto para comparar fragmentos sin depender de mayúsculas,
 * tildes, tipos de comillas o espacios repetidos. `origen[i]` guarda la
 * posición en el texto original del carácter normalizado `i`.
 */
export function normalizar(texto: string): TextoNormalizado {
  let resultado = '';
  const origen: number[] = [];
  let indice = 0;

  for (const caracter of texto) {
    const normal = normalizarCaracter(caracter);
    if (!(normal === ' ' && resultado.endsWith(' '))) {
      resultado += normal;
      for (let i = 0; i < normal.length; i++) {
        origen.push(indice);
      }
    }
    indice += caracter.length;
  }

  return { texto: resultado, origen };
}

export function normalizarEtiquetas(etiquetas: readonly string[], permitidas: ReadonlySet<string>): string[] {
  const unicas: string[] = [];
  for (const etiqueta of etiquetas) {
    const nombre = etiqueta.trim().toLowerCase();
    if (permitidas.has(nombre) && !unicas.includes(nombre)) {
      unicas.push(nombre);
    }
  }
  const recortadas = unicas.slice(0, MAX_ETIQUETAS_POR_ESCENA);
  return recortadas.some((nombre) => nombre !== ETIQUETA_MODIFICADORA) ? recortadas : [];
}

function candidatosDeBusqueda(fragmento: string): string[] {
  const base = normalizar(fragmento).texto.replace(/\.{3,}\s*$/u, '').trim();
  const variantes = [base, base.replace(/^[^\p{L}\p{N}]+/u, '')];
  const candidatos: string[] = [];

  for (const variante of variantes) {
    const palabras = variante.split(' ').filter((palabra) => palabra.length > 0);
    for (let cantidad = palabras.length; cantidad >= 1; cantidad--) {
      const candidato = palabras.slice(0, cantidad).join(' ');
      const esCompleto = cantidad === palabras.length;
      if (!esCompleto && (cantidad < MIN_PALABRAS_PREFIJO || candidato.length < MIN_CARACTERES_PREFIJO)) {
        break;
      }
      if (candidato.length >= 3 && !candidatos.includes(candidato)) {
        candidatos.push(candidato);
      }
    }
  }

  return candidatos;
}

function buscar(textoNormalizado: string, candidatos: readonly string[], desde: number): number {
  for (const candidato of candidatos) {
    const indice = textoNormalizado.indexOf(candidato, desde);
    if (indice !== -1) {
      return indice;
    }
  }
  return -1;
}

function retrocederPuntuacion(texto: string, posicion: number): number {
  let inicio = posicion;
  while (inicio > 0 && posicion - inicio < MAX_RETROCESO_PUNTUACION) {
    const anterior = texto[inicio - 1] ?? '';
    if (/[\p{L}\p{N}\s]/u.test(anterior)) {
      break;
    }
    inicio--;
  }
  return inicio;
}

export function extraerFragmento(texto: string, posicion: number, maximo = LONGITUD_FRAGMENTO): string {
  const limpio = texto
    .slice(posicion, posicion + maximo * 3)
    .replace(/\s+/gu, ' ')
    .trim();
  const caracteres = Array.from(limpio);
  if (caracteres.length <= maximo) {
    return limpio;
  }
  const recorte = caracteres.slice(0, maximo).join('');
  const ultimoEspacio = recorte.lastIndexOf(' ');
  return (ultimoEspacio > maximo / 2 ? recorte.slice(0, ultimoEspacio) : recorte).trim();
}

/**
 * Convierte las escenas propuestas por la IA en escenas con posición real
 * dentro del texto. Descarta las que no tienen etiquetas válidas, las que no
 * se encuentran en el texto y las que no respetan el orden de lectura.
 */
export function ubicarEscenas(
  texto: string,
  propuestas: readonly EscenaPropuesta[],
  permitidas: ReadonlySet<string>,
): EscenaUbicada[] {
  const normalizado = normalizar(texto);
  const ubicadas: Array<{ posicion: number; etiquetas: string[] }> = [];
  let desde = 0;

  for (const propuesta of propuestas) {
    const etiquetas = normalizarEtiquetas(propuesta.etiquetas, permitidas);
    if (etiquetas.length === 0) {
      continue;
    }

    const encontrado = buscar(normalizado.texto, candidatosDeBusqueda(propuesta.fragmentoInicio), desde);
    if (encontrado === -1) {
      continue;
    }

    const posicion = retrocederPuntuacion(texto, normalizado.origen[encontrado] ?? 0);
    const anterior = ubicadas[ubicadas.length - 1];
    if (anterior && posicion <= anterior.posicion) {
      continue;
    }

    ubicadas.push({ posicion, etiquetas });
    desde = encontrado + 1;
  }

  const primera = ubicadas[0];
  if (primera) {
    primera.posicion = 0;
  }

  return ubicadas.map((escena, indice) => ({
    orden: indice + 1,
    posicionInicio: escena.posicion,
    fragmentoInicio: extraerFragmento(texto, escena.posicion),
    etiquetas: escena.etiquetas,
  }));
}
