import { useState } from 'react';
export type Step = { id: string; agent: string; title: string; detail: string; status: string; result: string | null };
export type Workflow = { id: string; prompt: string; mode: string; source: string; createdAt: string; status: string; steps: Step[] };
export type Connection = { id: string; name: string; connected: boolean };
export type Project = { id: string; name: string; description: string; deadline: string; tasks: { id: string; title: string; done: boolean }[]; draft: string };
export const statuses: Record<string, string> = { planned: 'Perlu persetujuan', queued: 'Dalam antrean', running: 'Berjalan', completed: 'Selesai', failed: 'Perlu diperiksa', pending: 'Menunggu', skipped: 'Dilewati' };
export const services = [
  { id: 'drive', name: 'Google Drive', action: 'Folder & dokumen', description: 'Simpan dokumen proyek dalam satu tempat.', available: false },
  { id: 'calendar', name: 'Google Calendar', action: 'Jadwal pertemuan', description: 'Buat jadwal diskusi dari instruksi pekerjaan.', available: false },
  { id: 'discord', name: 'Discord', action: 'Pesan ke kelompok', description: 'Kirim pembagian tugas dan update ke channel kelompok.', available: true },
];
export async function api<T>(path: string, body?: object): Promise<T> {
  const response = await fetch('/api' + path, body ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : undefined);
  if (!response.ok) { const result = await response.json().catch(() => ({})); throw new Error(result.error || result.title || 'Permintaan belum berhasil. Coba lagi.'); }
  return response.json();
}
export function useStored<T>(key: string, initial: T): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => { try { const raw = localStorage.getItem(key); return raw ? JSON.parse(raw) : initial; } catch { return initial; } });
  return [value, next => { localStorage.setItem(key, JSON.stringify(next)); setValue(next); }];
}
export const dateLabel = (value: string) => new Date(value).toLocaleDateString('id-ID', { day: 'numeric', month: 'short' });
