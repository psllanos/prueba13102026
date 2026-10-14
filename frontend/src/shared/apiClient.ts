import { borrarToken, obtenerToken } from './tokenStore';

/** Forma del error que devuelve la API: `{ error, detail }` (ProblemPayload). */
export interface CargaProblema {
  error: string;
  detail: string;
}

/** Traduce los códigos de error de la API a mensajes en español para el usuario. */
const MENSAJES_ERROR: Record<string, string> = {
  invalid_request: 'Los datos enviados no son válidos.',
  no_provider_available: 'No hay ningún proveedor disponible para ese servicio en esa zona.',
  unauthorized: 'Tu sesión expiró. Volvé a iniciar sesión.',
  unknown_error: 'Ocurrió un error inesperado.',
};

export class ErrorApi extends Error {
  readonly status: number;
  readonly codigo: string;

  constructor(status: number, carga: CargaProblema) {
    // El detalle de la API es más específico cuando existe; si no, cae al mensaje por código.
    super(carga.detail || MENSAJES_ERROR[carga.error] || MENSAJES_ERROR.unknown_error!);
    this.name = 'ErrorApi';
    this.status = status;
    this.codigo = carga.error;
  }
}

/**
 * Único lugar que sabe hablar con la API: ruta base, cabecera de auth y contrato de error.
 * Los componentes nunca llaman a `fetch` directamente: si no, cada componente inventa su propio
 * manejo de errores y las traducciones se dispersan.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = obtenerToken();

  const respuesta = await fetch(`/api${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  });

  if (respuesta.status === 401) {
    // La sesión se perdió: se descarta el token para que la app vuelva al login.
    borrarToken();
    throw new ErrorApi(401, {
      error: 'unauthorized',
      detail: MENSAJES_ERROR.unauthorized!,
    });
  }

  if (!respuesta.ok) {
    const carga = await problemaSeguro(respuesta);
    throw new ErrorApi(respuesta.status, carga);
  }

  if (respuesta.status === 204) {
    return undefined as T;
  }

  return (await respuesta.json()) as T;
}

async function problemaSeguro(respuesta: Response): Promise<CargaProblema> {
  try {
    const cuerpo = (await respuesta.json()) as Partial<CargaProblema>;
    return {
      error: cuerpo.error ?? 'unknown_error',
      detail: cuerpo.detail ?? `La petición falló con estado ${respuesta.status}.`,
    };
  } catch {
    // Un cuerpo de error que no es JSON no debe romper la UI con una excepción de parseo.
    return { error: 'unknown_error', detail: `La petición falló con estado ${respuesta.status}.` };
  }
}
