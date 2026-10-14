import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function PaginaLogin() {
  const { iniciarSesion } = useAuth();
  const navegar = useNavigate();

  const [usuario, setUsuario] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function manejarEnvio(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setError(null);
    setEnviando(true);

    try {
      await iniciarSesion(usuario, contrasena);
      navegar('/proveedores', { replace: true });
    } catch (causa) {
      // Se muestra el mensaje, nunca el objeto de excepción crudo.
      setError(causa instanceof Error ? causa.message : 'No se pudo iniciar sesión.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <main className="centrado">
      <form className="tarjeta login" onSubmit={manejarEnvio} noValidate>
        <div className="login-marca">
          <span aria-hidden="true">🚚</span>
          <h1>Asistencias Vehiculares</h1>
        </div>
        <p className="subtitulo">
          Iniciá sesión para ver la disponibilidad de proveedores y el estado de los despachos.
        </p>

        <label htmlFor="usuario">Usuario</label>
        <input
          id="usuario"
          name="usuario"
          autoComplete="username"
          value={usuario}
          onChange={(e) => setUsuario(e.target.value)}
          required
        />

        <label htmlFor="contrasena">Contraseña</label>
        <input
          id="contrasena"
          name="contrasena"
          type="password"
          autoComplete="current-password"
          value={contrasena}
          onChange={(e) => setContrasena(e.target.value)}
          required
        />

        {/* role="alert" para que un lector de pantalla anuncie el fallo de login. */}
        {error !== null && (
          <p className="error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="btn-primario" disabled={enviando}>
          {enviando ? 'Ingresando…' : 'Ingresar'}
        </button>

        <p className="aviso centrado-texto">
          Build de demo: se acepta cualquier usuario y contraseña no vacíos. No hay proveedor de
          identidad conectado.
        </p>
      </form>
    </main>
  );
}
