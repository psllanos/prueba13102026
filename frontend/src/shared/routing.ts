import type { Ubicacion } from './types';

/** Punto de la ruta como par [lat, lon], listo para Leaflet. */
export type PuntoRuta = [number, number];

export interface Ruta {
  /** Trazado completo, listo para dibujar la polilínea. */
  puntos: PuntoRuta[];
  /** Distancia real por calles, en km. */
  distanciaKm: number;
  /** Duración estimada por el ruteo, en minutos. */
  duracionMin: number;
  /**
   * `true` cuando el ruteo real falló y se devolvió una línea recta.
   * La UI lo muestra: presentar una recta como si fuera una ruta por calles sería mentir.
   */
  esAproximada: boolean;
}

const OSRM_BASE = 'https://router.project-osrm.org/route/v1/driving';

/**
 * Obtiene la ruta real por calles entre dos puntos usando el servicio público de OSRM.
 *
 * Por qué OSRM y no Google/Mapbox: no requiere API key, lo que mantiene la demo ejecutable por
 * cualquiera que clone el repo sin pedirle credenciales. La contrapartida es que es un servicio
 * público y puede rate-limitar o caerse.
 *
 * Por eso SIEMPRE hay fallback: si el ruteo falla por lo que sea (red, timeout, respuesta
 * inesperada), se devuelve una línea recta marcada como `esAproximada`. La UI nunca queda vacía y
 * nunca muestra una recta como si fuera una ruta real.
 *
 * OSRM espera las coordenadas en orden lon,lat (al revés de lo que se lee habitualmente).
 */
export async function obtenerRuta(desde: Ubicacion, hasta: Ubicacion, signal?: AbortSignal): Promise<Ruta> {
  const url =
    `${OSRM_BASE}/${desde.longitude},${desde.latitude};${hasta.longitude},${hasta.latitude}` +
    '?overview=full&geometries=geojson&steps=false';

  try {
    const respuesta = await fetch(url, signal === undefined ? {} : { signal });

    if (!respuesta.ok) {
      throw new Error(`OSRM respondió ${respuesta.status}`);
    }

    const datos = (await respuesta.json()) as {
      code?: string;
      routes?: { distance: number; duration: number; geometry: { coordinates: [number, number][] } }[];
    };

    const ruta = datos.routes?.[0];
    if (datos.code !== 'Ok' || ruta === undefined) {
      throw new Error('OSRM no devolvió ninguna ruta');
    }

    return {
      // GeoJSON entrega [lon, lat]; Leaflet quiere [lat, lon].
      puntos: ruta.geometry.coordinates.map(([lon, lat]) => [lat, lon] as PuntoRuta),
      distanciaKm: ruta.distance / 1000,
      duracionMin: Math.round(ruta.duration / 60),
      esAproximada: false,
    };
  } catch (error) {
    // Un abort es intencional (el usuario cambió de proveedor), no un fallo del ruteo.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    return lineaRecta(desde, hasta);
  }
}

/** Fallback: distancia en línea recta, sin pretender que sea una ruta por calles. */
function lineaRecta(desde: Ubicacion, hasta: Ubicacion): Ruta {
  const km = distanciaHaversineKm(desde, hasta);

  return {
    puntos: [
      [desde.latitude, desde.longitude],
      [hasta.latitude, hasta.longitude],
    ],
    distanciaKm: km,
    // A 25 km/h promedio en tráfico urbano denso; es una estimación gruesa, y se rotula como tal.
    duracionMin: Math.max(1, Math.round((km / 25) * 60)),
    esAproximada: true,
  };
}

function distanciaHaversineKm(a: Ubicacion, b: Ubicacion): number {
  const RADIO_TIERRA_KM = 6371.0088;
  const aRad = (g: number) => (g * Math.PI) / 180;

  const lat1 = aRad(a.latitude);
  const lat2 = aRad(b.latitude);
  const deltaLat = aRad(b.latitude - a.latitude);
  const deltaLon = aRad(b.longitude - a.longitude);

  const h =
    Math.sin(deltaLat / 2) ** 2 + Math.cos(lat1) * Math.cos(lat2) * Math.sin(deltaLon / 2) ** 2;

  return 2 * RADIO_TIERRA_KM * Math.asin(Math.sqrt(h));
}
