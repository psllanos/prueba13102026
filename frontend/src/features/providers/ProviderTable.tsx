import type { Provider } from '../../shared/types';
import { SERVICE_TYPE_LABELS, type ServiceType } from '../../shared/types';

interface ProviderTableProps {
  providers: readonly Provider[];
  /** Id of the provider the optimizer selected, highlighted so the decision is visible. */
  selectedProviderId?: string | undefined;
}

/** The API returns the service type as a string; fall back to the raw value if it is unknown. */
function label(serviceType: string): string {
  return SERVICE_TYPE_LABELS[serviceType as ServiceType] ?? serviceType;
}

export function ProviderTable({ providers, selectedProviderId }: ProviderTableProps) {
  if (providers.length === 0) {
    return <p className="muted">No providers are available for this service type and location.</p>;
  }

  return (
    <table className="table">
      <caption className="sr-only">Available assistance providers with simulated ETA</caption>
      <thead>
        <tr>
          <th scope="col">Provider</th>
          <th scope="col">Service</th>
          <th scope="col">ETA</th>
          <th scope="col">Rating</th>
          <th scope="col">Status</th>
        </tr>
      </thead>
      <tbody>
        {providers.map((provider) => {
          const isSelected = provider.id === selectedProviderId;
          return (
            <tr
              // A stable domain id, never the array index.
              key={provider.id}
              className={isSelected ? 'row selected' : 'row'}
            >
              <td data-label="Provider">
                {provider.name}
                {isSelected && <span className="badge">optimal</span>}
              </td>
              <td data-label="Service">{label(provider.serviceType)}</td>
              <td data-label="ETA">{provider.etaMinutes} min</td>
              <td data-label="Rating">{provider.rating.toFixed(1)} ★</td>
              <td data-label="Status">
                <span className={provider.available ? 'pill ok' : 'pill busy'}>
                  {provider.available ? 'Available' : 'Busy'}
                </span>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
