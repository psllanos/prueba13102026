import { useEffect, useState } from 'react';
import { SERVICE_TYPES, SERVICE_TYPE_LABELS, type GeoLocation, type ServiceType } from '../../shared/types';

interface CoordinatesFormProps {
  serviceType: ServiceType;
  location: GeoLocation;
  radiusKm: number;
  onServiceTypeChange: (next: ServiceType) => void;
  onLocationChange: (next: GeoLocation) => void;
  onRadiusChange: (next: number) => void;
  onOptimize: () => void;
  isOptimizing: boolean;
}

/**
 * Request controls.
 *
 * The coordinate inputs keep their own TEXT state and only publish a parsed location when the value
 * is a valid number. Binding an `<input type="number">` straight to a `number` state makes every
 * intermediate keystroke ("-", "4.", "") collapse to NaN and fight the user. The text is the truth
 * while typing; the validated number is the truth in the model.
 */
export function CoordinatesForm({
  serviceType,
  location,
  radiusKm,
  onServiceTypeChange,
  onLocationChange,
  onRadiusChange,
  onOptimize,
  isOptimizing,
}: CoordinatesFormProps) {
  const [latitudeText, setLatitudeText] = useState(String(location.latitude));
  const [longitudeText, setLongitudeText] = useState(String(location.longitude));

  // Keep the fields in sync when the parent resets the location (e.g. "use my location").
  useEffect(() => {
    setLatitudeText(String(location.latitude));
  }, [location.latitude]);

  useEffect(() => {
    setLongitudeText(String(location.longitude));
  }, [location.longitude]);

  const parsedLatitude = Number.parseFloat(latitudeText);
  const parsedLongitude = Number.parseFloat(longitudeText);

  const latitudeInRange = Number.isFinite(parsedLatitude) && parsedLatitude >= -90 && parsedLatitude <= 90;
  const longitudeInRange = Number.isFinite(parsedLongitude) && parsedLongitude >= -180 && parsedLongitude <= 180;
  const isLocationValid = latitudeInRange && longitudeInRange;

  // Mirrors the domain rule (GeoPoint.Create) so the user sees the error before the API rejects it.
  const locationError = isLocationValid
    ? null
    : 'Latitude must be between -90 and 90 and longitude between -180 and 180.';

  function publishLocation(nextLatitudeText: string, nextLongitudeText: string) {
    const latitude = Number.parseFloat(nextLatitudeText);
    const longitude = Number.parseFloat(nextLongitudeText);

    if (Number.isFinite(latitude) && Number.isFinite(longitude)) {
      onLocationChange({ latitude, longitude });
    }
  }

  return (
    <div className="request-form">
      <div className="controls">
        <div className="field">
          <label htmlFor="serviceType">Service type</label>
          <select
            id="serviceType"
            value={serviceType}
            onChange={(event) => onServiceTypeChange(event.target.value as ServiceType)}
          >
            {SERVICE_TYPES.map((type) => (
              <option key={type} value={type}>
                {SERVICE_TYPE_LABELS[type]}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="latitude">Latitude</label>
          <input
            id="latitude"
            name="latitude"
            inputMode="decimal"
            value={latitudeText}
            aria-invalid={!latitudeInRange}
            onChange={(event) => {
              const next = event.target.value;
              setLatitudeText(next);
              publishLocation(next, longitudeText);
            }}
          />
        </div>

        <div className="field">
          <label htmlFor="longitude">Longitude</label>
          <input
            id="longitude"
            name="longitude"
            inputMode="decimal"
            value={longitudeText}
            aria-invalid={!longitudeInRange}
            onChange={(event) => {
              const next = event.target.value;
              setLongitudeText(next);
              publishLocation(latitudeText, next);
            }}
          />
        </div>

        <div className="field">
          <label htmlFor="radius">Radius (km)</label>
          <input
            id="radius"
            name="radius"
            type="number"
            min={1}
            max={100}
            value={radiusKm}
            onChange={(event) => {
              const next = Number.parseFloat(event.target.value);
              if (Number.isFinite(next) && next > 0) {
                onRadiusChange(next);
              }
            }}
          />
        </div>
      </div>

      {locationError !== null && (
        <p className="error" role="alert">
          {locationError}
        </p>
      )}

      <button type="button" onClick={onOptimize} disabled={isOptimizing || !isLocationValid}>
        {isOptimizing ? 'Optimizing…' : 'Select optimal provider'}
      </button>
    </div>
  );
}
