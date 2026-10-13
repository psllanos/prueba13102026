import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';
import { LoginPage } from './features/auth/LoginPage';
import { ProvidersPage } from './features/providers/ProvidersPage';
import type { ReactElement } from 'react';

/** Redirects to the login screen when there is no session. */
function RequireAuth({ children }: { children: ReactElement }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? children : <Navigate to="/login" replace />;
}

export function App() {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/providers" replace /> : <LoginPage />}
      />
      <Route
        path="/providers"
        element={
          <RequireAuth>
            <ProvidersPage />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to={isAuthenticated ? '/providers' : '/login'} replace />} />
    </Routes>
  );
}
