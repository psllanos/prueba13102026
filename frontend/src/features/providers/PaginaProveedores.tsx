import { useRef, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ETIQUETA_SERVICIO, type TipoServicio } from '../../shared/types';
import { useProveedoresDisponibles, useOptimizarProveedor } from './useProveedores';
import { useGeolocalizacion } from './useGeolocalizacion';
import { FormularioSolicitud } from './FormularioSolicitud';
import { MapaProveedores } from './MapaProveedores';
import { TablaProveedores } from './TablaProveedores';
import { DesglosePuntaje } from './DesglosePuntaje';
import { SinResultados } from './SinResultados';

/** Radios ofrecidos al usuario, en orden ascendente. */
const RADIOS_KM = [2, 5, 10, 20, 50] as const;

const RADIO_INICIAL_KM = 10;

/**
 * Traduce un fallo de optimización a algo accionable.
 *
 * El caso importante es "no hay proveedor disponible": la API responde en inglés con un texto
 * correcto pero inútil ("No provider matched..."). El usuario no necesita saber que el motor buscó
 * y no encontró; necesita saber qué hacer. Este mensaje nombra la causa y las dos salidas reales.
 */
function mensajeErrorOptimizacion(error: unknown): string {
  const codigo = (error as { codigo?: string }).codigo;

  if (codigo === 'no_provider_available') {
    return 'No hay ningún proveedor de ese servicio dentro del radio elegido. Ampliá el radio de búsqueda o probá con otro tipo de servicio.';
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'No se pudo calcular el proveedor óptimo.';
}

export function PaginaProveedores() {
  const { cerrarSesion } = useAuth();
  const [tipoServicio, setTipoServicio] = useState<TipoServicio>('tow-truck');
  const [radioKm, setRadioKm] = useState(RADIO_INICIAL_KM);
  const [proveedorEnFoco, setProveedorEnFoco] = useState<string | null>(null);

  const { ubicacion, estado: estadoUbicacion, mensaje: mensajeUbicacion, solicitar } = useGeolocalizacion();

  const consultaProveedores = useProveedoresDisponibles(tipoServicio, ubicacion, radioKm);
  const optimizar = useOptimizarProveedor();

  const proveedores = consultaProveedores.data ?? [];
  const cantidadProveedores = proveedores.length;

  // Se guarda la ubicación al momento de optimizar: si el usuario mueve el mapa después, el mapa no
  // debe reinterpretar la decisión anterior con coordenadas nuevas.
  const ubicacionAlOptimizar = useRef(ubicacion);

  function manejarOptimizar() {
    ubicacionAlOptimizar.current = ubicacion;
    optimizar.mutate({ serviceType: tipoServicio, location: ubicacion, searchRadiusKm: radioKm });
  }

  function manejarVerEnMapa(proveedorId: string) {
    setProveedorEnFoco(proveedorId);
    // El marcador correspondiente ya se resalta por `proveedorElegidoId`; aquí sólo se asegura de
    // que el mapa esté visible en pantallas chicas.
    document.getElementById('mapa-proveedores')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  return (
    <main className="pagina">
      <header className="barra-superior">
        <div>
          <h1>Asistencias Vehiculares</h1>
          <p className="subtitulo">Asignación inteligente de proveedores</p>
        </div>
        <button type="button" className="btn-fantasma" onClick={cerrarSesion}>
          Cerrar sesión
        </button>
      </header>

      <div className="disposicion">
        <section className="tarjeta tarjeta-formulario" aria-labelledby="titulo-solicitud">
          <h2 id="titulo-solicitud">Nueva solicitud</h2>
          <FormularioSolicitud
            tipoServicio={tipoServicio}
            ubicacion={ubicacion}
            radioKm={radioKm}
            estadoUbicacion={estadoUbicacion}
            mensajeUbicacion={mensajeUbicacion}
            cantidadProveedores={cantidadProveedores}
            onTipoServicio={(siguiente) => {
              setTipoServicio(siguiente);
              // La decisión anterior pertenece al tipo de servicio anterior.
              optimizar.reset();
            }}
            onUbicacion={() => {
              setProveedorEnFoco(null);
              optimizar.reset();
            }}
            onRadio={(siguiente) => {
              setRadioKm(siguiente);
              optimizar.reset();
            }}
            onUsarMiUbicacion={solicitar}
            onOptimizar={manejarOptimizar}
            optimizando={optimizar.isPending}
          />

          {optimizar.isError && (
            <p className="error" role="alert">
              {mensajeErrorOptimizacion(optimizar.error)}
            </p>
          )}
        </section>

        <section className="tarjeta" aria-labelledby="titulo-mapa" id="mapa-proveedores">
          <h2 id="titulo-mapa">
            Mapa — {ETIQUETA_SERVICIO[tipoServicio]}
            <span className="contador">{radioKm} km a la redonda</span>
          </h2>
          <MapaProveedores
            ubicacion={ubicacion}
            radioKm={radioKm}
            proveedores={proveedores}
            proveedorElegidoId={optimizar.data?.selectedProvider.id ?? proveedorEnFoco ?? undefined}
            tipoServicio={tipoServicio}
            proveedorRuta={optimizar.data?.selectedProvider ?? null}
          />
        </section>
      </div>

      {optimizar.isSuccess && (
        <section className="tarjeta" aria-labelledby="titulo-decision">
          <h2 id="titulo-decision">Decisión</h2>
          <DesglosePuntaje resultado={optimizar.data} />
        </section>
      )}

      <section className="tarjeta" aria-labelledby="titulo-proveedores">
        <h2 id="titulo-proveedores">
          Proveedores — {ETIQUETA_SERVICIO[tipoServicio]}
          {consultaProveedores.isSuccess && <span className="contador">{cantidadProveedores}</span>}
        </h2>

        {consultaProveedores.isPending && <p className="cargando">Cargando proveedores…</p>}

        {consultaProveedores.isError && (
          <p className="error" role="alert">
            {consultaProveedores.error instanceof Error
              ? consultaProveedores.error.message
              : 'No se pudieron cargar los proveedores.'}
          </p>
        )}

        {consultaProveedores.isSuccess && cantidadProveedores > 0 && (
          <TablaProveedores
            proveedores={proveedores}
            ubicacion={ubicacion}
            proveedorElegidoId={optimizar.data?.selectedProvider.id ?? proveedorEnFoco ?? undefined}
            onVerEnMapa={manejarVerEnMapa}
          />
        )}

        {/* Estado vacío con salidas concretas: el usuario no queda varado sin saber qué hacer. */}
        {consultaProveedores.isSuccess && cantidadProveedores === 0 && (
          <SinResultados
            tipoServicio={tipoServicio}
            ubicacion={ubicacion}
            radioKm={radioKm}
            radiosDisponibles={RADIOS_KM}
            onAmpliarRadio={(km) => {
              setRadioKm(km);
              optimizar.reset();
            }}
            onCentrarEnProveedores={() => {
              // Salta a la zona con cobertura para que la próxima búsqueda sí encuentre resultados,
              // en lugar de dejar al usuario adivinando dónde hay proveedores.
              solicitar();
            }}
          />
        )}
      </section>
    </main>
  );
}
