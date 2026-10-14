/**
 * Tipos de dominio espejados del contrato REAL de la API
 * (src/ProviderOptimizer.Api/Contracts/Payloads.cs).
 *
 * Son DTOs, deliberadamente separados del estado de la UI. La API nunca expone sus entidades de
 * persistencia, así que el cliente tampoco debe inventar campos que el contrato no promete.
 *
 * Los nombres coinciden exactamente con el JSON serializado (camelCase sobre las propiedades del
 * record de C#).
 */

export type TipoServicio = 'tow-truck' | 'locksmith' | 'battery';

export const TIPOS_SERVICIO: readonly TipoServicio[] = ['tow-truck', 'locksmith', 'battery'] as const;

export const ETIQUETA_SERVICIO: Record<TipoServicio, string> = {
  'tow-truck': 'Grúa',
  locksmith: 'Cerrajería',
  battery: 'Batería',
};

/** Ícono por tipo de servicio, para reconocerlo en el mapa de un vistazo. */
export const ICONO_SERVICIO: Record<TipoServicio, string> = {
  'tow-truck': '🚚',
  locksmith: '🔑',
  battery: '🔋',
};

export interface Ubicacion {
  latitude: number;
  longitude: number;
}

/** Espeja `ProviderPayload`. Nota: `available`, no `isAvailable`. */
export interface Proveedor {
  id: string;
  name: string;
  serviceType: string;
  latitude: number;
  longitude: number;
  rating: number;
  etaMinutes: number;
  available: boolean;
}

/** Espeja `ScoreFactorPayload` — un factor del puntaje, para poder explicar por qué ganó. */
export interface FactorPuntaje {
  factor: string;
  raw: number;
  weight: number;
  contribution: number;
}

/** Espeja `OptimizePayload`. */
export interface ResultadoOptimizacion {
  caseId: string;
  selectedProvider: Proveedor;
  totalScore: number;
  breakdown: readonly FactorPuntaje[];
  decidedAt: string;
}

/** Espeja `OptimizeRequest`. La ubicación es un objeto anidado, no coordenadas planas. */
export interface CuerpoOptimizacion {
  serviceType: TipoServicio;
  location: Ubicacion;
  searchRadiusKm?: number;
}

/** Nombre legible de cada factor del puntaje, para la tabla. */
export const ETIQUETA_FACTOR: Record<string, string> = {
  eta: 'ETA',
  distance: 'Distancia',
  availability: 'Disponibilidad',
  rating: 'Calificación',
};

/** Unidades de cada factor, para que el número crudo signifique algo. */
export const UNIDAD_FACTOR: Record<string, string> = {
  eta: 'min',
  distance: 'km',
  availability: '',
  rating: '★',
};
