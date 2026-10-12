import { leerConfiguracion } from './config.js';
import { crearClienteGemini } from './gemini.js';
import { crearServidor } from './servidor.js';

const configuracion = leerConfiguracion();

const servidor = crearServidor({
  gemini: crearClienteGemini({
    urlBase: configuracion.urlBaseGemini,
    modelo: configuracion.modelo,
    tiempoEsperaMs: configuracion.tiempoEsperaMs,
  }),
  maxCaracteres: configuracion.maxCaracteres,
});

servidor.listen(configuracion.puerto, () => {
  console.log(
    `Servicio de IA escuchando en http://localhost:${configuracion.puerto} (modelo: ${configuracion.modelo})`,
  );
});

function cerrar(): void {
  servidor.close(() => process.exit(0));
  servidor.closeAllConnections();
}

process.on('SIGINT', cerrar);
process.on('SIGTERM', cerrar);
