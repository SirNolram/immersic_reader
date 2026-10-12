export interface Configuracion {
  puerto: number;
  modelo: string;
  urlBaseGemini: string;
  tiempoEsperaMs: number;
  maxCaracteres: number;
}

const URL_BASE_GEMINI = 'https://generativelanguage.googleapis.com/v1beta';

function leerEntero(valor: string | undefined, porDefecto: number): number {
  const numero = Number.parseInt(valor ?? '', 10);
  return Number.isFinite(numero) && numero > 0 ? numero : porDefecto;
}

export function leerConfiguracion(entorno: NodeJS.ProcessEnv = process.env): Configuracion {
  return {
    puerto: leerEntero(entorno.PORT, 3001),
    modelo: entorno.GEMINI_MODELO?.trim() || 'gemini-3.5-flash',
    urlBaseGemini: (entorno.GEMINI_URL_BASE?.trim() || URL_BASE_GEMINI).replace(/\/+$/, ''),
    tiempoEsperaMs: leerEntero(entorno.GEMINI_TIEMPO_ESPERA_MS, 150_000),
    maxCaracteres: leerEntero(entorno.MAX_CARACTERES, 60_000),
  };
}
