import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await signIn(username, password);
      navigate('/providers', { replace: true });
    } catch (cause) {
      // Surface the message, never the raw exception object.
      setError(cause instanceof Error ? cause.message : 'Sign in failed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="centered">
      <form className="card login" onSubmit={handleSubmit} noValidate>
        <h1>Assistance Console</h1>
        <p className="muted">Sign in to review provider availability and dispatch status.</p>

        <label htmlFor="username">Username</label>
        <input
          id="username"
          name="username"
          autoComplete="username"
          value={username}
          onChange={(event) => setUsername(event.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        {/* role="alert" so a screen reader announces a failed sign in. */}
        {error !== null && (
          <p className="error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>

        <p className="muted small">
          Demo build: any non-empty credentials are accepted locally. No identity provider is wired.
        </p>
      </form>
    </main>
  );
}
