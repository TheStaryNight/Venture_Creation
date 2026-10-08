import { useState } from 'react';
export type Step = { id: string; agent: string; title: string; detail: string; status: string; result: string | null; name: string; start: string; end: string; includeLinks: boolean; createMeet: boolean; meetUrl: string | null; resultUrl: string | null };
export type Workflow = { id: string; prompt: string; mode: string; source: string; createdAt: string; status: string; steps: Step[] };
export type Connection = { id: string; name: string; connected: boolean };
export type Project = { id: string; name: string; description: string; deadline: string; tasks: { id: string; title: string; done: boolean }[]; draft: string };
export const statuses: Record<string, string> = { planned: 'Perlu persetujuan', queued: 'Dalam antrean', running: 'Berjalan', completed: 'Selesai', failed: 'Perlu diperiksa', pending: 'Menunggu', skipped: 'Dilewati' };
export const services = [
  { id: 'drive', name: 'Google Drive', action: 'Folder proyek', description: 'Simpan dokumen proyek dalam satu tempat.', available: true },
  { id: 'calendar', name: 'Google Calendar', action: 'Jadwal pertemuan', description: 'Buat jadwal diskusi dari instruksi pekerjaan.', available: true },
  { id: 'meet', name: 'Google Meet', action: 'Tautan rapat video', description: 'Buat tautan Meet bersama jadwal di Google Calendar.', available: true },
  { id: 'discord', name: 'Discord', action: 'Pesan ke kelompok', description: 'Kirim pembagian tugas dan update ke channel kelompok.', available: true },
];
export type Billing = { plan: string; developmentAccess: boolean; simulationAccess: boolean; checkoutEnabled: boolean; monthlyPrice: number; currency: string };
export class ApiError extends Error { constructor(message: string, public code?: string) { super(message); } }
export async function api<T>(path: string, body?: object): Promise<T> {
  const response = await fetch('/api' + path, body ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : undefined);
  if (!response.ok) { const result = await response.json().catch(() => ({})); throw new ApiError(result.error || result.title || 'Permintaan belum berhasil. Coba lagi.', result.code); }
  return response.json();
}
export function useStored<T>(key: string, initial: T): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => { try { const raw = localStorage.getItem(key); return raw ? JSON.parse(raw) : initial; } catch { return initial; } });
  return [value, next => { localStorage.setItem(key, JSON.stringify(next)); setValue(next); }];
}
export const dateLabel = (value: string) => new Date(value).toLocaleDateString('id-ID', { day: 'numeric', month: 'short' });
