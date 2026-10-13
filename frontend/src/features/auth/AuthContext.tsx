import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { clearAccessToken, getAccessToken, setAccessToken } from '../../shared/tokenStore';

interface AuthContextValue {
  isAuthenticated: boolean;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Authentication for the demo.
 *
 * There is no identity provider in scope for this exercise, so the credential check is simulated
 * locally and the token is a locally minted placeholder. The POINT of this file is the shape:
 * the token is held in memory, the session is explicit, and components never touch storage
 * directly. Swapping this for a real OIDC/JWT endpoint means rewriting `signIn` and nothing else.
 *
 * The simulated token must never be trusted by the API — the API has no auth yet, which is
 * tracked debt (docs/squad-standards.md §11.2 item 1).
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(() => getAccessToken() !== null);

  const signIn = useCallback(async (username: string, password: string) => {
    if (username.trim() === '' || password.trim() === '') {
      throw new Error('Username and password are required.');
    }

    // Simulated token exchange. Replace with a POST to the identity endpoint.
    await Promise.resolve();

    setAccessToken(`demo.${btoa(username)}.token`);
    setIsAuthenticated(true);
  }, []);

  const signOut = useCallback(() => {
    clearAccessToken();
    setIsAuthenticated(false);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ isAuthenticated, signIn, signOut }),
    [isAuthenticated, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth must be used inside an AuthProvider.');
  }
  return context;
}
