import { useEffect, useMemo } from 'react';
import { Circle, MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import type { Proveedor, TipoServicio, Ubicacion } from '../../shared/types';
import { ETIQUETA_SERVICIO, ICONO_SERVICIO } from '../../shared/types';
import { distanciaKm, formatearEta, formatearKm } from '../../shared/geo';
import { RutaAnimada } from './RutaAnimada';
import { useRuta } from './useRuta';

interface MapaProveedoresProps {
  ubicacion: Ubicacion;
  radioKm: number;
  proveedores: readonly Proveedor[];
  proveedorElegidoId?: string | undefined;
  tipoServicio: TipoServicio;
  /** Cuando hay un proveedor elegido, se dibuja la ruta hacia él. */
  proveedorRuta: Proveedor | null;
}

/**
 * Leaflet arma sus íconos por defecto con rutas de imagen relativas que Vite no resuelve, y los
 * marcadores aparecen rotos. Se reemplazan por íconos de tipo `divIcon`, que además permiten mostrar
 * el ícono del servicio (🚚 🔑 🔋) dentro del pin.
 */
function iconoProveedor(tipo: TipoServicio, elegido: boolean, disponible: boolean): L.DivIcon {
  const clases = ['pin-proveedor', elegido ? 'pin-elegido' : '', disponible ? '' : 'pin-no-disponible']
    .filter(Boolean)
    .join(' ');

  return L.divIcon({
    className: 'pin-contenedor',
    html: `<div class="${clases}">${ICONO_SERVICIO[tipo] ?? '📍'}</div>`,
    iconSize: [34, 34],
    iconAnchor: [17, 17],
    popupAnchor: [0, -18],
  });
}

/** Pin del usuario: círculo azul con pulso, distinto de los proveedores. */
const ICONO_USUARIO = L.divIcon({
  className: 'pin-contenedor',
  html: '<div class="pin-usuario"><span></span></div>',
  iconSize: [28, 28],
  iconAnchor: [14, 14],
  popupAnchor: [0, -14],
});

/**
 * Recentra el mapa cuando cambia la ubicación del usuario.
 *
 * Sin esto, el mapa queda donde el usuario lo dejó y el botón "usar mi ubicación" cambia el centro
 * de búsqueda sin que el mapa lo refleje: el usuario ve datos que no coinciden con lo que mira.
 */
function RecentrarAlCambiarUbicacion({ ubicacion }: { ubicacion: Ubicacion }) {
  const mapa = useMap();

  useEffect(() => {
    mapa.setView([ubicacion.latitude, ubicacion.longitude], mapa.getZoom(), { animate: true });
  }, [mapa, ubicacion.latitude, ubicacion.longitude]);

  return null;
}

/**
 * Ajusta el encuadre para que entren el usuario y todos los proveedores.
 *
 * Se recalcula sólo cuando cambia el CONJUNTO de proveedores (por sus ids), no en cada render: si
 * dependiera del array, el mapa se reencuadraría solo y pelearía con el usuario que hizo zoom.
 */
function EncuadrarResultados({
  ubicacion,
  proveedores,
}: {
  ubicacion: Ubicacion;
  proveedores: readonly Proveedor[];
}) {
  const mapa = useMap();

  // Los puntos se derivan a valores primitivos SERIALIZADOS. Depender del array `proveedores` haría
  // que el mapa se reencuadre en cada render y pelee con el zoom del usuario; depende de la
  // identidad del conjunto, que es lo que realmente debe disparar un reencuadre.
  const puntosSerializados = useMemo(
    () =>
      proveedores
        .map((p) => `${p.latitude},${p.longitude}`)
        .concat(`${ubicacion.latitude},${ubicacion.longitude}`)
        .join('|'),
    [proveedores, ubicacion.latitude, ubicacion.longitude],
  );

  useEffect(() => {
    if (proveedores.length === 0) {
      return;
    }

    const puntos: L.LatLngExpression[] = puntosSerializados
      .split('|')
      .map((par) => {
        const [lat, lon] = par.split(',');
        return [Number(lat), Number(lon)] as L.LatLngExpression;
      });

    mapa.fitBounds(L.latLngBounds(puntos), { padding: [48, 48], maxZoom: 15 });
  }, [puntosSerializados, proveedores.length, mapa]);

  return null;
}

export function MapaProveedores({
  ubicacion,
  radioKm,
  proveedores,
  proveedorElegidoId,
  tipoServicio,
  proveedorRuta,
}: MapaProveedoresProps) {
  const centro: L.LatLngExpression = [ubicacion.latitude, ubicacion.longitude];

  const destino = proveedorRuta === null
    ? null
    : { latitude: proveedorRuta.latitude, longitude: proveedorRuta.longitude };

  // Ruta por calles hacia el proveedor elegido. `useRuta` aborta la petición anterior si el
  // destino cambia, así que una respuesta lenta no puede sobrescribir a una más reciente.
  const { ruta, cargando: cargandoRuta } = useRuta(ubicacion, destino);

  return (
    <div className="mapa-contenedor">
      <MapContainer
        center={centro}
        zoom={13}
        scrollWheelZoom
        className="mapa"
        // Se desactiva el zoom con doble clic porque interfiere con el doble clic para copiar texto.
        doubleClickZoom={false}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        <RecentrarAlCambiarUbicacion ubicacion={ubicacion} />
        <EncuadrarResultados ubicacion={ubicacion} proveedores={proveedores} />

        {/* Círculo del radio de búsqueda: el usuario ve hasta dónde se está buscando. */}
        <Circle
          center={centro}
          radius={radioKm * 1000}
          pathOptions={{
            color: '#4f9dff',
            fillColor: '#4f9dff',
            fillOpacity: 0.08,
            weight: 1.5,
            dashArray: '6 6',
          }}
        />

        <Marker position={centro} icon={ICONO_USUARIO}>
          <Popup>
            <strong>Tu ubicación</strong>
            <br />
            Radio de búsqueda: {radioKm} km
          </Popup>
        </Marker>

        {/* Ruta animada hacia el proveedor elegido, estilo app de transporte. */}
        {ruta !== null && proveedorRuta !== null && (
          <RutaAnimada ruta={ruta} proveedor={proveedorRuta} tipoServicio={tipoServicio} />
        )}

        {proveedores.map((proveedor) => {
          const elegido = proveedor.id === proveedorElegidoId;
          const km = distanciaKm(ubicacion, {
            latitude: proveedor.latitude,
            longitude: proveedor.longitude,
          });

          return (
            <Marker
              key={proveedor.id}
              position={[proveedor.latitude, proveedor.longitude]}
              icon={iconoProveedor(tipoServicio, elegido, proveedor.available)}
              zIndexOffset={elegido ? 1000 : 0}
            >
              <Popup>
                <strong>{proveedor.name}</strong>
                {elegido && <span className="popup-elegido"> ✓ elegido</span>}
                <br />
                {ETIQUETA_SERVICIO[proveedor.serviceType as TipoServicio] ?? proveedor.serviceType}
                <br />
                ETA: {formatearEta(proveedor.etaMinutes)} · {formatearKm(km)} en línea recta
                <br />
                Calificación: {proveedor.rating.toFixed(1)} ★
                <br />
                {proveedor.available ? 'Disponible' : 'Ocupado'}
              </Popup>
            </Marker>
          );
        })}
      </MapContainer>

      {cargandoRuta && (
        <div className="mapa-aviso" role="status">
          <span className="spinner" aria-hidden="true" />
          Trazando la ruta hasta el proveedor…
        </div>
      )}

      {ruta !== null && !cargandoRuta && (
        <div className="mapa-aviso mapa-aviso-ruta">
          <span>
            <strong>{formatearKm(ruta.distanciaKm)}</strong> ·{' '}
            {formatearEta(ruta.duracionMin)} de llegada
          </span>
          {ruta.esAproximada && (
            <span className="mapa-aviso-nota">
              Ruteo no disponible: se muestra la distancia en línea recta.
            </span>
          )}
        </div>
      )}

      <div className="mapa-leyenda">
        <span className="leyenda-item">
          <span className="leyenda-punto leyenda-usuario" /> Tu ubicación
        </span>
        <span className="leyenda-item">
          <span className="leyenda-punto leyenda-proveedor" /> Proveedor disponible
        </span>
        <span className="leyenda-item">
          <span className="leyenda-punto leyenda-elegido" /> Proveedor elegido
        </span>
      </div>
    </div>
  );
}
