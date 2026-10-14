import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';
import { PaginaLogin } from './features/auth/PaginaLogin';
import { PaginaProveedores } from './features/providers/PaginaProveedores';
import type { ReactElement } from 'react';

/** Redirige al login cuando no hay sesión. */
function RequiereAuth({ children }: { children: ReactElement }) {
  const { autenticado } = useAuth();
  return autenticado ? children : <Navigate to="/login" replace />;
}

export function App() {
  const { autenticado } = useAuth();

  return (
    <Routes>
      <Route
        path="/login"
        element={autenticado ? <Navigate to="/proveedores" replace /> : <PaginaLogin />}
      />
      <Route
        path="/proveedores"
        element={
          <RequiereAuth>
            <PaginaProveedores />
          </RequiereAuth>
        }
      />
      <Route
        path="*"
        element={<Navigate to={autenticado ? '/proveedores' : '/login'} replace />}
      />
    </Routes>
  );
}
