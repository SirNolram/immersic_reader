export interface EtiquetaCatalogo {
  nombre: string;
  descripcion: string;
}

export interface EscenaPropuesta {
  fragmentoInicio: string;
  etiquetas: string[];
}

export interface EscenaUbicada {
  orden: number;
  posicionInicio: number;
  fragmentoInicio: string;
  etiquetas: string[];
}

export interface SolicitudAnalisis {
  texto: string;
  etiquetas: EtiquetaCatalogo[];
  claveApi: string;
}

export interface ClienteGemini {
  readonly modelo: string;
  proponerEscenas(solicitud: SolicitudAnalisis): Promise<EscenaPropuesta[]>;
}
