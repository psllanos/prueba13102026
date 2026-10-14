import { useEffect, useState } from 'react';
import {
  ETIQUETA_SERVICIO,
  TIPOS_SERVICIO,
  type TipoServicio,
  type Ubicacion,
} from '../../shared/types';
import type { EstadoGeolocalizacion } from './useGeolocalizacion';

interface FormularioSolicitudProps {
  tipoServicio: TipoServicio;
  ubicacion: Ubicacion;
  radioKm: number;
  estadoUbicacion: EstadoGeolocalizacion;
  mensajeUbicacion: string | null;
  onTipoServicio: (siguiente: TipoServicio) => void;
  onUbicacion: (siguiente: Ubicacion) => void;
  onRadio: (siguiente: number) => void;
  onUsarMiUbicacion: () => void;
  onOptimizar: () => void;
  optimizando: boolean;
  cantidadProveedores: number;
}

const RADIOS = [2, 5, 10, 20, 50] as const;

/**
 * Controles de la solicitud.
 *
 * Los campos de coordenadas mantienen su propio estado de TEXTO y sólo publican una ubicación
 * parseada cuando el valor es un número válido. Atar un `<input type="number">` directamente a un
 * estado `number` hace que cada tecla intermedia ("-", "4.", "") colapse a NaN y pelee con el
 * usuario. Mientras se escribe, la verdad es el texto; en el modelo, el número validado.
 */
export function FormularioSolicitud({
  tipoServicio,
  ubicacion,
  radioKm,
  estadoUbicacion,
  mensajeUbicacion,
  onTipoServicio,
  onUbicacion,
  onRadio,
  onUsarMiUbicacion,
  onOptimizar,
  optimizando,
  cantidadProveedores,
}: FormularioSolicitudProps) {
  const [textoLatitud, setTextoLatitud] = useState(String(ubicacion.latitude));
  const [textoLongitud, setTextoLongitud] = useState(String(ubicacion.longitude));

  // Sincroniza los campos cuando la ubicación cambia por fuera (botón "usar mi ubicación").
  useEffect(() => {
    setTextoLatitud(String(ubicacion.latitude));
  }, [ubicacion.latitude]);

  useEffect(() => {
    setTextoLongitud(String(ubicacion.longitude));
  }, [ubicacion.longitude]);

  const latitud = Number.parseFloat(textoLatitud);
  const longitud = Number.parseFloat(textoLongitud);

  const latitudValida = Number.isFinite(latitud) && latitud >= -90 && latitud <= 90;
  const longitudValida = Number.isFinite(longitud) && longitud >= -180 && longitud <= 180;
  const ubicacionValida = latitudValida && longitudValida;

  // Espeja la regla del dominio (GeoPoint.Create) para que el usuario vea el error ANTES de que la
  // API lo rechace: el mensaje del servidor es correcto pero llega después del enojo.
  const errorUbicacion = ubicacionValida
    ? null
    : 'La latitud debe estar entre -90 y 90, y la longitud entre -180 y 180.';

  function publicarUbicacion(siguienteLatitud: string, siguienteLongitud: string) {
    const lat = Number.parseFloat(siguienteLatitud);
    const lon = Number.parseFloat(siguienteLongitud);

    if (Number.isFinite(lat) && Number.isFinite(lon)) {
      onUbicacion({ latitude: lat, longitude: lon });
    }
  }

  return (
    <div className="formulario">
      <div className="campo-grupo">
        <span className="campo-etiqueta">¿Qué necesitás?</span>
        <div className="chips" role="group" aria-label="Tipo de servicio">
          {TIPOS_SERVICIO.map((tipo) => (
            <button
              key={tipo}
              type="button"
              className={tipo === tipoServicio ? 'chip chip-activo' : 'chip'}
              aria-pressed={tipo === tipoServicio}
              onClick={() => onTipoServicio(tipo)}
            >
              {ETIQUETA_SERVICIO[tipo]}
            </button>
          ))}
        </div>
      </div>

      <div className="campo-grupo">
        <div className="campo-encabezado">
          <span className="campo-etiqueta">Ubicación</span>
          <button
            type="button"
            className="enlace"
            onClick={onUsarMiUbicacion}
            disabled={estadoUbicacion === 'solicitando' || estadoUbicacion === 'no-soportada'}
          >
            {estadoUbicacion === 'solicitando' ? 'Obteniendo…' : '📍 Usar mi ubicación'}
          </button>
        </div>

        <div className="coordenadas">
          <div className="campo">
            <label htmlFor="latitud">Latitud</label>
            <input
              id="latitud"
              name="latitud"
              inputMode="decimal"
              value={textoLatitud}
              aria-invalid={!latitudValida}
              onChange={(e) => {
                const siguiente = e.target.value;
                setTextoLatitud(siguiente);
                publicarUbicacion(siguiente, textoLongitud);
              }}
            />
          </div>

          <div className="campo">
            <label htmlFor="longitud">Longitud</label>
            <input
              id="longitud"
              name="longitud"
              inputMode="decimal"
              value={textoLongitud}
              aria-invalid={!longitudValida}
              onChange={(e) => {
                const siguiente = e.target.value;
                setTextoLongitud(siguiente);
                publicarUbicacion(textoLatitud, siguiente);
              }}
            />
          </div>
        </div>

        {mensajeUbicacion !== null && <p className="aviso">{mensajeUbicacion}</p>}
      </div>

      <div className="campo-grupo">
        <span className="campo-etiqueta">Radio de búsqueda</span>
        <div className="chips" role="group" aria-label="Radio de búsqueda">
          {RADIOS.map((km) => (
            <button
              key={km}
              type="button"
              className={km === radioKm ? 'chip chip-activo' : 'chip'}
              aria-pressed={km === radioKm}
              onClick={() => onRadio(km)}
            >
              {km} km
            </button>
          ))}
        </div>
      </div>

      {errorUbicacion !== null && (
        <p className="error" role="alert">
          {errorUbicacion}
        </p>
      )}

      <button
        type="button"
        className="btn-primario"
        onClick={onOptimizar}
        disabled={optimizando || !ubicacionValida}
      >
        {optimizando
          ? 'Calculando…'
          : `Buscar el mejor proveedor (${cantidadProveedores} disponible${cantidadProveedores === 1 ? '' : 's'})`}
      </button>
    </div>
  );
}
