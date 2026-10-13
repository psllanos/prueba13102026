/**
 * Domain types mirrored from the REAL API contract
 * (src/ProviderOptimizer.Api/Contracts/Payloads.cs).
 *
 * These are DTOs, deliberately separate from any UI state. The API never leaks its persistence
 * entities, so the client must not invent fields the contract does not promise either.
 *
 * Field names match the serialized JSON exactly (camelCase over the C# record properties).
 */

export type ServiceType = 'tow-truck' | 'locksmith' | 'battery';

export const SERVICE_TYPES: readonly ServiceType[] = ['tow-truck', 'locksmith', 'battery'] as const;

export const SERVICE_TYPE_LABELS: Record<ServiceType, string> = {
  'tow-truck': 'Tow truck',
  locksmith: 'Locksmith',
  battery: 'Battery',
};

export interface GeoLocation {
  latitude: number;
  longitude: number;
}

/** Mirrors `ProviderPayload`. Note `available`, not `isAvailable`. */
export interface Provider {
  id: string;
  name: string;
  serviceType: string;
  latitude: number;
  longitude: number;
  rating: number;
  etaMinutes: number;
  available: boolean;
}

/** Mirrors `ScoreFactorPayload` — one factor of the score, so the UI can explain why a provider won. */
export interface ScoreFactor {
  factor: string;
  raw: number;
  weight: number;
  contribution: number;
}

/** Mirrors `OptimizePayload`. */
export interface OptimizeResult {
  caseId: string;
  selectedProvider: Provider;
  totalScore: number;
  breakdown: readonly ScoreFactor[];
  decidedAt: string;
}

/** Mirrors `OptimizeRequest`. The location is a nested object, not flat coordinates. */
export interface OptimizeRequestBody {
  serviceType: ServiceType;
  location: GeoLocation;
  searchRadiusKm?: number;
}
