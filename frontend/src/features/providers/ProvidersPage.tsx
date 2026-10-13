import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { SERVICE_TYPE_LABELS, type GeoLocation, type ServiceType } from '../../shared/types';
import { useAvailableProviders, useOptimizeProvider } from './useProviders';
import { ProviderTable } from './ProviderTable';
import { ScoreBreakdown } from './ScoreBreakdown';
import { CoordinatesForm } from './CoordinatesForm';

/** Default demo location (Bogotá). A real client would use the device location. */
const DEFAULT_LOCATION: GeoLocation = { latitude: 4.711, longitude: -74.0721 };
const DEFAULT_RADIUS_KM = 10;

export function ProvidersPage() {
  const { signOut } = useAuth();
  const [serviceType, setServiceType] = useState<ServiceType>('tow-truck');
  const [location, setLocation] = useState<GeoLocation>(DEFAULT_LOCATION);
  const [radiusKm, setRadiusKm] = useState(DEFAULT_RADIUS_KM);

  const providersQuery = useAvailableProviders(serviceType, location, radiusKm);
  const optimize = useOptimizeProvider();

  function handleOptimize() {
    optimize.mutate({ serviceType, location, searchRadiusKm: radiusKm });
  }

  return (
    <main className="page">
      <header className="topbar">
        <h1>Assistance Console</h1>
        <button type="button" className="ghost" onClick={signOut}>
          Sign out
        </button>
      </header>

      <section className="card">
        <CoordinatesForm
          serviceType={serviceType}
          location={location}
          radiusKm={radiusKm}
          onServiceTypeChange={(next) => {
            setServiceType(next);
            // The previous decision belongs to the previous service type.
            optimize.reset();
          }}
          onLocationChange={(next) => {
            setLocation(next);
            optimize.reset();
          }}
          onRadiusChange={setRadiusKm}
          onOptimize={handleOptimize}
          isOptimizing={optimize.isPending}
        />

        {optimize.isError && (
          <p className="error" role="alert">
            {optimize.error instanceof Error
              ? optimize.error.message
              : 'Could not compute an optimal provider.'}
          </p>
        )}

        {optimize.isSuccess && <ScoreBreakdown result={optimize.data} />}
      </section>

      <section className="card">
        <h2>Providers — {SERVICE_TYPE_LABELS[serviceType]}</h2>

        {providersQuery.isPending && <p className="muted">Loading providers…</p>}

        {providersQuery.isError && (
          <p className="error" role="alert">
            {providersQuery.error instanceof Error
              ? providersQuery.error.message
              : 'Could not load providers.'}
          </p>
        )}

        {providersQuery.isSuccess && (
          <ProviderTable
            providers={providersQuery.data}
            selectedProviderId={optimize.data?.selectedProvider.id}
          />
        )}
      </section>
    </main>
  );
}
