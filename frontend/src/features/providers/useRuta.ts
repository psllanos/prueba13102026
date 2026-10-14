import { useEffect, useState } from 'react';
import { obtenerRuta, type Ruta } from '../../shared/routing';
import type { Ubicacion } from '../../shared/types';

interface EstadoRuta {
  ruta: Ruta | null;
  cargando: boolean;
  error: string | null;
}

/**
 * Carga la ruta por calles hacia un destino.
 *
 * Detalle importante: el `AbortController` cancela la petición anterior cuando cambia el destino.
 * Sin esto, elegir proveedor A y enseguida B puede hacer que la respuesta de A llegue DESPUÉS que
 * la de B y sobrescriba el estado, mostrando la ruta al proveedor equivocado. Es un race condition
 * clásico de este tipo de UI y es silencioso: la pantalla muestra datos plausibles pero incorrectos.
 *
 * Las dependencias del efecto son valores ESCALARES, no los objetos `origen`/`destino`: un objeto
 * nuevo con los mismos números no debe disparar una petición nueva. Por eso se desestructuran antes
 * del efecto, en lugar de silenciar la regla del linter.
 */
export function useRuta(origen: Ubicacion, destino: Ubicacion | null): EstadoRuta {
  const [ruta, setRuta] = useState<Ruta | null>(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const origenLat = origen.latitude;
  const origenLon = origen.longitude;
  const destinoLat = destino?.latitude;
  const destinoLon = destino?.longitude;

  useEffect(() => {
    if (destinoLat === undefined || destinoLon === undefined) {
      setRuta(null);
      setCargando(false);
      setError(null);
      return;
    }

    const controlador = new AbortController();
    setCargando(true);
    setError(null);

    // Se reconstruyen objetos mínimos a partir de los escalares ya capturados: así el efecto no
    // depende de las referencias originales y no puede leer un valor obsoleto.
    const desde: Ubicacion = { latitude: origenLat, longitude: origenLon };
    const hasta: Ubicacion = { latitude: destinoLat, longitude: destinoLon };

    obtenerRuta(desde, hasta, controlador.signal)
      .then((resultado) => {
        setRuta(resultado);
        setCargando(false);
      })
      .catch((causa: unknown) => {
        if (causa instanceof DOMException && causa.name === 'AbortError') {
          return;
        }
        setError('No se pudo trazar la ruta por calles.');
        setCargando(false);
      });

    return () => controlador.abort();
  }, [origenLat, origenLon, destinoLat, destinoLon]);

  return { ruta, cargando, error };
}
