import type { Proveedor, TipoServicio, Ubicacion } from '../../shared/types';
import { ETIQUETA_SERVICIO } from '../../shared/types';
import { distanciaKm, formatearEta, formatearKm } from '../../shared/geo';

interface TablaProveedoresProps {
  proveedores: readonly Proveedor[];
  ubicacion: Ubicacion;
  /** Id del proveedor que eligió el optimizador, resaltado para que la decisión se vea. */
  proveedorElegidoId?: string | undefined;
  onVerEnMapa: (proveedorId: string) => void;
}

/** La API devuelve el tipo de servicio como string; si es desconocido se muestra crudo. */
function etiqueta(tipoServicio: string): string {
  return ETIQUETA_SERVICIO[tipoServicio as TipoServicio] ?? tipoServicio;
}

export function TablaProveedores({
  proveedores,
  ubicacion,
  proveedorElegidoId,
  onVerEnMapa,
}: TablaProveedoresProps) {
  if (proveedores.length === 0) {
    return (
      <p className="vacio">
        No hay proveedores disponibles para este servicio en esta zona. Probá ampliar el radio de
        búsqueda.
      </p>
    );
  }

  // Se ordenan por distancia para que la tabla y el mapa cuenten la misma historia.
  const ordenados = [...proveedores].sort(
    (a, b) => distanciaKm(ubicacion, a) - distanciaKm(ubicacion, b),
  );

  return (
    <div className="tabla-scroll">
      <table className="tabla">
        <caption className="sr-only">
          Proveedores de asistencia disponibles, con ETA simulado y distancia
        </caption>
        <thead>
          <tr>
            <th scope="col">Proveedor</th>
            <th scope="col">Servicio</th>
            <th scope="col">ETA</th>
            <th scope="col">Distancia</th>
            <th scope="col">Calificación</th>
            <th scope="col">Estado</th>
            <th scope="col"><span className="sr-only">Acciones</span></th>
          </tr>
        </thead>
        <tbody>
          {ordenados.map((proveedor) => {
            const elegido = proveedor.id === proveedorElegidoId;
            const km = distanciaKm(ubicacion, proveedor);

            return (
              <tr
                // Un id de dominio estable, nunca el índice del array.
                key={proveedor.id}
                className={elegido ? 'fila fila-elegida' : 'fila'}
              >
                <td data-etiqueta="Proveedor">
                  <span className="celda-proveedor">
                    {proveedor.name}
                    {elegido && <span className="insignia">óptimo</span>}
                  </span>
                </td>
                <td data-etiqueta="Servicio">{etiqueta(proveedor.serviceType)}</td>
                <td data-etiqueta="ETA">
                  <strong>{formatearEta(proveedor.etaMinutes)}</strong>
                </td>
                <td data-etiqueta="Distancia">{formatearKm(km)}</td>
                <td data-etiqueta="Calificación">
                  {proveedor.rating.toFixed(1)} <span aria-hidden="true">★</span>
                  <span className="sr-only">estrellas</span>
                </td>
                <td data-etiqueta="Estado">
                  <span className={proveedor.available ? 'pastilla ok' : 'pastilla ocupado'}>
                    {proveedor.available ? 'Disponible' : 'Ocupado'}
                  </span>
                </td>
                <td data-etiqueta="Acciones">
                  <button type="button" className="enlace" onClick={() => onVerEnMapa(proveedor.id)}>
                    Ver en mapa
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
