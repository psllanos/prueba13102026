import { useMutation, useQuery } from '@tanstack/react-query';
import { apiFetch } from '../../shared/apiClient';
import type {
  CuerpoOptimizacion,
  Proveedor,
  ResultadoOptimizacion,
  TipoServicio,
  Ubicacion,
} from '../../shared/types';

/**
 * Proveedores disponibles para un tipo de servicio dentro de un radio de una ubicación.
 *
 * El endpoint es por query string y REQUIERE las coordenadas: la elegibilidad es función de la
 * ubicación, así que no hay una respuesta sensata sin ellas (la API devuelve 400 si faltan).
 */
export function useProveedoresDisponibles(
  tipoServicio: TipoServicio,
  ubicacion: Ubicacion,
  radioKm: number,
) {
  const query = new URLSearchParams({
    serviceType: tipoServicio,
    latitude: String(ubicacion.latitude),
    longitude: String(ubicacion.longitude),
    radiusKm: String(radioKm),
  });

  return useQuery({
    queryKey: [
      'proveedores',
      'disponibles',
      tipoServicio,
      // Se redondea a 4 decimales (~11 m) para que mover el mapa unos metros no dispare una
      // petición nueva por cada píxel: la caché seguiría siendo inútil.
      ubicacion.latitude.toFixed(4),
      ubicacion.longitude.toFixed(4),
      radioKm,
    ],
    queryFn: () => apiFetch<Proveedor[]>(`/providers/available?${query.toString()}`),
    // La lista es una foto de la disponibilidad en vivo; se mantiene fresca sin golpear la API.
    staleTime: 15_000,
  });
}

/** Ejecuta el optimizador y elige el proveedor óptimo para una solicitud. */
export function useOptimizarProveedor() {
  return useMutation({
    mutationFn: (cuerpo: CuerpoOptimizacion) =>
      apiFetch<ResultadoOptimizacion>('/optimize', {
        method: 'POST',
        body: JSON.stringify(cuerpo),
      }),
  });
}
