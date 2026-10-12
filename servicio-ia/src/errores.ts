export class ErrorServicio extends Error {
  readonly estado: number;
  readonly codigo: string;

  constructor(estado: number, codigo: string, mensaje: string) {
    super(mensaje);
    this.name = 'ErrorServicio';
    this.estado = estado;
    this.codigo = codigo;
  }
}

export function solicitudInvalida(mensaje: string): ErrorServicio {
  return new ErrorServicio(400, 'SOLICITUD_INVALIDA', mensaje);
}
