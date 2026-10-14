import type { TipoServicio, Ubicacion } from '../../shared/types';
import { ETIQUETA_SERVICIO } from '../../shared/types';

interface SinResultadosProps {
  tipoServicio: TipoServicio;
  ubicacion: Ubicacion;
  radioKm: number;
  radiosDisponibles: readonly number[];
  onAmpliarRadio: (km: number) => void;
  onCentrarEnProveedores: () => void;
}

/**
 * Estado vacío con salidas concretas.
 *
 * "No hay proveedores disponibles" es técnicamente correcto y humanamente inútil: el usuario no
 * sabe si el sistema está roto, si él se equivocó, o qué puede hacer. Este componente reemplaza ese
 * callejón sin salida por las dos acciones que realmente resuelven el problema: ampliar el radio o
 * saltar a donde sí hay proveedores.
 *
 * Es la diferencia entre reportar un error y ayudar a resolverlo.
 */
export function SinResultados({
  tipoServicio,
  ubicacion,
  radioKm,
  radiosDisponibles,
  onAmpliarRadio,
  onCentrarEnProveedores,
}: SinResultadosProps) {
  const radiosMayores = radiosDisponibles.filter((km) => km > radioKm);
  const servicio = ETIQUETA_SERVICIO[tipoServicio].toLowerCase();

  return (
    <div className="sin-resultados">
      <span className="sin-resultados-icono" aria-hidden="true">
        🔍
      </span>

      <h3>Sin proveedores de {servicio} en {radioKm} km</h3>

      <p className="sin-resultados-texto">
        Ningún proveedor de {servicio} figura disponible dentro de ese radio desde tu ubicación
        actual ({ubicacion.latitude.toFixed(4)}, {ubicacion.longitude.toFixed(4)}). No es un error:
        es el resultado de la búsqueda. Estas son las salidas:
      </p>

      <div className="sin-resultados-acciones">
        {radiosMayores.length > 0 && (
          <div className="accion-grupo">
            <span className="accion-etiqueta">Ampliá el radio de búsqueda</span>
            <div className="chips">
              {radiosMayores.map((km) => (
                <button
                  key={km}
                  type="button"
                  className="chip"
                  onClick={() => onAmpliarRadio(km)}
                >
                  {km} km
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="accion-grupo">
          <span className="accion-etiqueta">O buscá donde sí hay cobertura</span>
          <button type="button" className="chip chip-accion" onClick={onCentrarEnProveedores}>
            📍 Ir a la zona con proveedores
          </button>
        </div>
      </div>
    </div>
  );
}
