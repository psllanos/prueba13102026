/**
 * Token storage.
 *
 * The squad standard forbids putting the JWT in `localStorage` (docs/squad-standards.md §3.2):
 * any XSS reads it, and it survives until explicitly cleared. This demo keeps the token in memory
 * only, which is the safe default.
 *
 * TRADE-OFF, stated honestly: an in-memory token is lost on reload. A production system pairs this
 * with an httpOnly refresh cookie, so the access token stays short-lived and in memory while the
 * refresh token is unreachable from JavaScript. That refresh flow is out of scope for this demo.
 */

let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string): void {
  accessToken = token;
}

export function clearAccessToken(): void {
  accessToken = null;
}
