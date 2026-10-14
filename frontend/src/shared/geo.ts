import type { Ubicacion } from './types';

/** Radio terrestre medio en km (el mismo valor que usa el dominio en GeoPoint.DistanceKmTo). */
const RADIO_TIERRA_KM = 6371.0088;

/**
 * Distancia de círculo máximo entre dos puntos (haversine), en kilómetros.
 *
 * Es la MISMA fórmula que `GeoPoint.DistanceKmTo` en el dominio (src/ProviderOptimizer.Domain/
 * ValueObjects.cs). Se replica en el cliente para poder ordenar y mostrar distancias sin pedirle a
 * la API un cálculo por proveedor, no para decidir elegibilidad: la autoridad sigue siendo el
 * dominio.
 */
export function distanciaKm(desde: Ubicacion, hasta: Ubicacion): number {
  const aRad = (grados: number) => (grados * Math.PI) / 180;

  const lat1 = aRad(desde.latitude);
  const lat2 = aRad(hasta.latitude);
  const deltaLat = aRad(hasta.latitude - desde.latitude);
  const deltaLon = aRad(hasta.longitude - desde.longitude);

  const a =
    Math.sin(deltaLat / 2) ** 2 +
    Math.cos(lat1) * Math.cos(lat2) * Math.sin(deltaLon / 2) ** 2;

  return 2 * RADIO_TIERRA_KM * Math.asin(Math.sqrt(a));
}

/** Formatea una distancia con una decimal, con coma decimal en español. */
export function formatearKm(km: number): string {
  return `${km.toFixed(1).replace('.', ',')} km`;
}

/** Formatea un ETA en minutos a algo legible (min, o horas y minutos). */
export function formatearEta(minutos: number): string {
  if (minutos < 60) {
    return `${minutos} min`;
  }
  const horas = Math.floor(minutos / 60);
  const resto = minutos % 60;
  return resto === 0 ? `${horas} h` : `${horas} h ${resto} min`;
}

/** Bogotá: ubicación de demo cuando el usuario no da permiso de geolocalización. */
export const UBICACION_POR_DEFECTO: Ubicacion = { latitude: 4.711, longitude: -74.0721 };
