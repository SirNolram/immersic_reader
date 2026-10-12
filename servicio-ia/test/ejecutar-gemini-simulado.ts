import { iniciarGeminiSimulado, responderPorParrafos } from './gemini-simulado.js';

const puerto = Number.parseInt(process.argv[2] ?? '4010', 10);
const simulado = await iniciarGeminiSimulado(responderPorParrafos, puerto);
console.log(`Gemini simulado en ${simulado.url}`);

process.on('SIGTERM', () => {
  void simulado.cerrar().then(() => process.exit(0));
});
process.on('SIGINT', () => {
  void simulado.cerrar().then(() => process.exit(0));
});
