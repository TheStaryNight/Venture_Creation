import { t } from '../i18n';
export class ApiError extends Error {
  constructor(
    message: string,
    public code?: string,
    public status?: number,
  ) {
    super(message);
  }
}

export async function api<T>(path: string, body?: object, signal?: AbortSignal): Promise<T> {
  const response = await fetch(
    '/api' + path,
    body
      ? {
          signal,
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Orbit-Request': '1' },
          body: JSON.stringify(body),
        }
      : { signal },
  );
  if (!response.ok) {
    const result = await response.json().catch(() => ({}));
    throw new ApiError(
      t(result.error || result.title || 'Permintaan belum berhasil. Coba lagi.'),
      result.code,
      response.status,
    );
  }
  return response.json();
}
