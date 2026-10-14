import { useCallback, useEffect, useState } from 'react';
import { UBICACION_POR_DEFECTO } from '../../shared/geo';
import type { Ubicacion } from '../../shared/types';

export type EstadoGeolocalizacion = 'inactiva' | 'solicitando' | 'concedida' | 'denegada' | 'no-soportada';

interface Geolocalizacion {
  ubicacion: Ubicacion;
  estado: EstadoGeolocalizacion;
  mensaje: string | null;
  solicitar: () => void;
}

/**
 * Ubicación del usuario vía la API de geolocalización del navegador.
 *
 * Decisión de UX importante: la ubicación NO se pide automáticamente al montar. Un prompt de
 * permisos apenas se abre la app es intrusivo y, si el usuario lo niega, deja la pantalla en un
 * estado confuso. Acá el usuario aprieta "Usar mi ubicación" y hasta entonces se muestra una
 * ubicación por defecto claramente etiquetada como tal.
 */
export function useGeolocalizacion(): Geolocalizacion {
  const [ubicacion, setUbicacion] = useState<Ubicacion>(UBICACION_POR_DEFECTO);
  const [estado, setEstado] = useState<EstadoGeolocalizacion>('inactiva');

  useEffect(() => {
    if (typeof navigator === 'undefined' || !('geolocation' in navigator)) {
      setEstado('no-soportada');
    }
  }, []);

  const solicitar = useCallback(() => {
    if (typeof navigator === 'undefined' || !('geolocation' in navigator)) {
      setEstado('no-soportada');
      return;
    }

    setEstado('solicitando');

    navigator.geolocation.getCurrentPosition(
      (posicion) => {
        setUbicacion({
          latitude: posicion.coords.latitude,
          longitude: posicion.coords.longitude,
        });
        setEstado('concedida');
      },
      () => {
        // El usuario negó el permiso o el dispositivo no pudo resolver la posición.
        // No es un error fatal: se sigue con la ubicación por defecto.
        setEstado('denegada');
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 60_000 },
    );
  }, []);

  const mensaje =
    estado === 'no-soportada'
      ? 'Tu navegador no soporta geolocalización. Se usa una ubicación por defecto.'
      : estado === 'denegada'
        ? 'No diste permiso de ubicación. Se usa una ubicación por defecto.'
        : estado === 'inactiva'
          ? 'Usando una ubicación por defecto (Bogotá).'
          : null;

  return { ubicacion, estado, mensaje, solicitar };
}
