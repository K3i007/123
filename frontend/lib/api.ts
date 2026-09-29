let accessToken: string | null = null;
export function setAccessToken(value: string | null) {
  accessToken = value;
}
export async function api<T>(path: string): Promise<T> {
  return request<T>(path);
}
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/proxy${path}`, {
    ...init,
    headers: {
      ...(init?.headers ?? {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    },
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      title?: string;
      detail?: string;
    } | null;
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? "No fue posible guardar los cambios.",
    );
  }
  return response.json() as Promise<T>;
}
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}
