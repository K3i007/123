let accessToken: string | null = null;
export function setAccessToken(value: string | null) {
  accessToken = value;
}
export async function api<T>(path: string): Promise<T> {
  return request<T>(path);
}
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api/proxy${path}`, { ...init, headers: { ...(init?.headers ?? {}), ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) } });
  } catch {
    throw new ApiError(0, "No se pudo conectar con el servidor. Intenta nuevamente.");
  }
  if (!response.ok) {
    const problem = (await readJsonIfAvailable(response)) as {
      title?: string;
      detail?: string;
    } | null;
    const retryAfter = response.headers.get("Retry-After");
    const fallback = response.status === 429
      ? `Demasiadas solicitudes.${retryAfter ? ` Intenta nuevamente en ${retryAfter} segundos.` : " Intenta nuevamente más tarde."}`
      : response.status >= 500 ? "El servidor no pudo procesar la solicitud. Intenta nuevamente." : "No fue posible guardar los cambios.";
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? fallback,
    );
  }
  const data = await readJsonIfAvailable(response);
  if (data === null) throw new ApiError(response.status, "El servidor devolvió una respuesta inválida.");
  return data as T;
}
export async function readJsonIfAvailable(response: Response): Promise<unknown | null> {
  const contentType = response.headers.get("content-type") ?? "";
  return contentType.includes("application/json") || contentType.includes("application/problem+json") ? response.json().catch(() => null) : null;
}
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}
