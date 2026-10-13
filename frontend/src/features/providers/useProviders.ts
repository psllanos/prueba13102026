import { useMutation, useQuery } from '@tanstack/react-query';
import { apiFetch } from '../../shared/apiClient';
import type {
  GeoLocation,
  OptimizeRequestBody,
  OptimizeResult,
  Provider,
  ServiceType,
} from '../../shared/types';

/**
 * Available providers for a service type within a radius of a location.
 *
 * The endpoint is query-string based and REQUIRES the coordinates: eligibility is a function of
 * location, so there is no sensible coordinate-free answer (the API returns 400 without them).
 */
export function useAvailableProviders(serviceType: ServiceType, location: GeoLocation, radiusKm: number) {
  const query = new URLSearchParams({
    serviceType,
    latitude: String(location.latitude),
    longitude: String(location.longitude),
    radiusKm: String(radiusKm),
  });

  return useQuery({
    queryKey: ['providers', 'available', serviceType, location.latitude, location.longitude, radiusKm],
    queryFn: () => apiFetch<Provider[]>(`/providers/available?${query.toString()}`),
    // The list is a snapshot of live availability; keep it fresh but do not hammer the API.
    staleTime: 15_000,
  });
}

/** Runs the optimizer and selects the optimal provider for a request. */
export function useOptimizeProvider() {
  return useMutation({
    mutationFn: (body: OptimizeRequestBody) =>
      apiFetch<OptimizeResult>('/optimize', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
  });
}
