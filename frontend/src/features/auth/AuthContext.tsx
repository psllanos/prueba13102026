import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { borrarToken, guardarToken, obtenerToken } from '../../shared/tokenStore';

interface ValorContextoAuth {
  autenticado: boolean;
  iniciarSesion: (usuario: string, contrasena: string) => Promise<void>;
  cerrarSesion: () => void;
}

const ContextoAuth = createContext<ValorContextoAuth | null>(null);

/**
 * Autenticación para la demo.
 *
 * No hay proveedor de identidad en el alcance de este ejercicio, así que la validación de
 * credenciales es simulada localmente y el token es un placeholder generado en el cliente. Lo
 * importante de este archivo es la FORMA: el token vive en memoria, la sesión es explícita y los
 * componentes nunca tocan el almacenamiento directamente. Cambiar esto por un endpoint real de
 * OIDC/JWT implica reescribir `iniciarSesion` y nada más.
 *
 * El token simulado NUNCA debe ser confiado por la API: la API todavía no tiene autenticación, lo
 * cual está declarado como deuda en el README.
 */
export function ProveedorAuth({ children }: { children: ReactNode }) {
  const [autenticado, setAutenticado] = useState(() => obtenerToken() !== null);

  const iniciarSesion = useCallback(async (usuario: string, contrasena: string) => {
    if (usuario.trim() === '' || contrasena.trim() === '') {
      throw new Error('El usuario y la contraseña son obligatorios.');
    }

    // Intercambio de token simulado. Reemplazar por un POST al endpoint de identidad.
    await Promise.resolve();

    guardarToken(`demo.${btoa(usuario)}.token`);
    setAutenticado(true);
  }, []);

  const cerrarSesion = useCallback(() => {
    borrarToken();
    setAutenticado(false);
  }, []);

  const valor = useMemo<ValorContextoAuth>(
    () => ({ autenticado, iniciarSesion, cerrarSesion }),
    [autenticado, iniciarSesion, cerrarSesion],
  );

  return <ContextoAuth.Provider value={valor}>{children}</ContextoAuth.Provider>;
}

export function useAuth(): ValorContextoAuth {
  const contexto = useContext(ContextoAuth);
  if (contexto === null) {
    throw new Error('useAuth debe usarse dentro de un ProveedorAuth.');
  }
  return contexto;
}
