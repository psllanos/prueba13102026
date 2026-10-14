/**
 * Almacenamiento del token.
 *
 * Guardar el JWT en `localStorage` está prohibido por política del squad: cualquier XSS lo lee y
 * sobrevive hasta que se borre explícitamente. Esta demo lo mantiene sólo en memoria, que es el
 * valor por defecto seguro.
 *
 * CONTRAPARTIDA, dicha de frente: un token en memoria se pierde al recargar la página. Un sistema en
 * producción combina esto con una cookie httpOnly de refresco, para que el token de acceso sea de
 * vida corta y viva en memoria mientras el de refresco queda inalcanzable desde JavaScript. Ese
 * flujo de refresco está fuera del alcance de esta demo.
 */

let tokenAcceso: string | null = null;

export function obtenerToken(): string | null {
  return tokenAcceso;
}

export function guardarToken(token: string): void {
  tokenAcceso = token;
}

export function borrarToken(): void {
  tokenAcceso = null;
}
