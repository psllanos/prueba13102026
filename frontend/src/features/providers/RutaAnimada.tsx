import { useEffect, useMemo, useState } from 'react';
import { Marker, Polyline, Popup } from 'react-leaflet';
import L from 'leaflet';
import type { Ruta, PuntoRuta } from '../../shared/routing';
import type { Proveedor, TipoServicio } from '../../shared/types';
import { ICONO_SERVICIO } from '../../shared/types';
import { formatearEta, formatearKm } from '../../shared/geo';

interface RutaAnimadaProps {
  ruta: Ruta;
  proveedor: Proveedor;
  tipoServicio: TipoServicio;
  /** Duración de la animación en ms. */
  duracionAnimacionMs?: number;
}

const DURACION_POR_DEFECTO = 2600;

/** Pin del proveedor en movimiento. Es más chico que el pin fijo para no tapar el destino. */
function iconoVehiculo(tipo: TipoServicio): L.DivIcon {
  return L.divIcon({
    className: 'pin-contenedor',
    html: `<div class="pin-vehiculo">${ICONO_SERVICIO[tipo] ?? '🚗'}</div>`,
    iconSize: [30, 30],
    iconAnchor: [15, 15],
  });
}

/**
 * Interpola un punto a lo largo del trazado, según una fracción de 0 a 1.
 *
 * Se indexa por longitud acumulada en lugar de por índice de vértice: OSRM devuelve muchos más
 * puntos en las curvas que en las rectas, así que avanzar por índice haría que el vehículo
 * recorriera las curvas a saltos y las rectas demasiado rápido.
 */
function puntoEnFraccion(puntos: PuntoRuta[], fraccion: number): PuntoRuta {
  if (puntos.length === 0) {
    return [0, 0];
  }
  if (puntos.length === 1 || fraccion <= 0) {
    return puntos[0]!;
  }
  if (fraccion >= 1) {
    return puntos[puntos.length - 1]!;
  }

  const acumuladas: number[] = [0];
  let total = 0;
  for (let i = 1; i < puntos.length; i += 1) {
    const [lat1, lon1] = puntos[i - 1]!;
    const [lat2, lon2] = puntos[i]!;
    total += Math.hypot(lat2 - lat1, lon2 - lon1);
    acumuladas.push(total);
  }

  const objetivo = total * fraccion;
  for (let i = 1; i < acumuladas.length; i += 1) {
    if (acumuladas[i]! >= objetivo) {
      const previo = acumuladas[i - 1]!;
      const segmento = acumuladas[i]! - previo;
      const t = segmento === 0 ? 0 : (objetivo - previo) / segmento;
      const [lat1, lon1] = puntos[i - 1]!;
      const [lat2, lon2] = puntos[i]!;
      return [lat1 + (lat2 - lat1) * t, lon1 + (lon2 - lon1) * t];
    }
  }

  return puntos[puntos.length - 1]!;
}

/**
 * Dibuja la ruta hacia el proveedor y anima el recorrido, estilo DiDi/Uber.
 *
 * La animación no es decorativa: comunica dirección y progreso. Una polilínea estática responde
 * "por dónde va", pero no "cuánto falta", que es la pregunta que el usuario realmente tiene.
 *
 * `prefers-reduced-motion` se respeta: quien pidió menos movimiento ve la ruta completa y el
 * vehículo ya en el destino, sin animación.
 */
export function RutaAnimada({
  ruta,
  proveedor,
  tipoServicio,
  duracionAnimacionMs = DURACION_POR_DEFECTO,
}: RutaAnimadaProps) {
  const [progreso, setProgreso] = useState(0);

  const prefiereMenosMovimiento = useMemo(
    () =>
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches,
    [],
  );

  useEffect(() => {
    if (prefiereMenosMovimiento) {
      setProgreso(1);
      return;
    }

    setProgreso(0);
    let cuadro = 0;
    const inicio = performance.now();

    const animar = (ahora: number) => {
      const transcurrido = ahora - inicio;
      const fraccion = Math.min(1, transcurrido / duracionAnimacionMs);
      // Easing suave al final, para que el vehículo no frene en seco.
      setProgreso(1 - (1 - fraccion) ** 3);

      if (fraccion < 1) {
        cuadro = requestAnimationFrame(animar);
      }
    };

    cuadro = requestAnimationFrame(animar);
    return () => cancelAnimationFrame(cuadro);
  }, [ruta, duracionAnimacionMs, prefiereMenosMovimiento]);

  // El trazado ya recorrido se dibuja sólido; lo que falta queda punteado, como en las apps de
  // transporte.
  const indiceCorte = Math.max(1, Math.floor(ruta.puntos.length * progreso));
  const recorrido = ruta.puntos.slice(0, indiceCorte);
  const resto = ruta.puntos.slice(indiceCorte - 1);

  const posicionVehiculo = puntoEnFraccion(ruta.puntos, progreso);

  return (
    <>
      {/* Trazado pendiente: punteado y tenue. */}
      <Polyline
        positions={resto}
        pathOptions={{ color: '#4f9dff', weight: 3, opacity: 0.35, dashArray: '2 10', lineCap: 'round' }}
      />

      {/* Trazado recorrido: sólido y brillante, con halo. */}
      <Polyline
        positions={recorrido}
        pathOptions={{ color: '#0a1830', weight: 8, opacity: 0.55 }}
      />
      {recorrido.length > 1 && (
        <Polyline
          positions={recorrido}
          pathOptions={{ color: '#4f9dff', weight: 4, opacity: 1, lineCap: 'round' }}
        />
      )}

      <Marker position={posicionVehiculo} icon={iconoVehiculo(tipoServicio)}>
        <Popup>
          <strong>{proveedor.name}</strong> en camino
          <br />
          {ruta.esAproximada ? 'Distancia en línea recta' : 'Ruta por calles'}:{' '}
          {formatearKm(ruta.distanciaKm)}
          <br />
          Llegada estimada: {formatearEta(ruta.duracionMin)}
        </Popup>
      </Marker>
    </>
  );
}
