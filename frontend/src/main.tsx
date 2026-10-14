import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProveedorAuth } from './features/auth/AuthContext';
import { App } from './App';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // No se reintenta un error de cliente: un 400/409 no va a funcionar al segundo intento.
      retry: (intentos, error) => {
        const status = (error as { status?: number }).status;
        if (typeof status === 'number' && status >= 400 && status < 500) {
          return false;
        }
        return intentos < 2;
      },
      refetchOnWindowFocus: false,
    },
  },
});

const elementoRaiz = document.getElementById('root');
if (elementoRaiz === null) {
  throw new Error('Falta el elemento raíz #root en index.html.');
}

createRoot(elementoRaiz).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ProveedorAuth>
          <App />
        </ProveedorAuth>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
