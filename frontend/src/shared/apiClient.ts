import { clearAccessToken, getAccessToken } from './tokenStore';

/** Error shape the API returns: `{ error, detail }` (ProblemPayload). */
export interface ProblemPayload {
  error: string;
  detail: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, payload: ProblemPayload) {
    super(payload.detail);
    this.name = 'ApiError';
    this.status = status;
    this.code = payload.error;
  }
}

/**
 * Single place that knows how to talk to the API: base path, auth header, error contract.
 * Components never call `fetch` directly — otherwise every component grows its own error handling.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getAccessToken();

  const response = await fetch(`/api${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  });

  if (response.status === 401) {
    // The session is gone: drop the token so the app falls back to the login screen.
    clearAccessToken();
    throw new ApiError(401, { error: 'unauthorized', detail: 'Your session expired. Sign in again.' });
  }

  if (!response.ok) {
    const payload = await safeProblem(response);
    throw new ApiError(response.status, payload);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function safeProblem(response: Response): Promise<ProblemPayload> {
  try {
    const body = (await response.json()) as Partial<ProblemPayload>;
    return {
      error: body.error ?? 'unknown_error',
      detail: body.detail ?? `Request failed with status ${response.status}.`,
    };
  } catch {
    // A non-JSON error body must not crash the UI with a parse exception.
    return { error: 'unknown_error', detail: `Request failed with status ${response.status}.` };
  }
}
