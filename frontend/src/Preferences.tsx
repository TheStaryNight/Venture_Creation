import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { setLanguage } from './i18n';
import { api } from './data';

export type Preferences = { language: 'id' | 'en'; theme: 'light' | 'dark' | 'system'; timeZone: string };
const defaults = (): Preferences => ({ language: 'id', theme: 'system', timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'Asia/Jakarta' });
const Context = createContext<{ preferences: Preferences; save: (value: Preferences) => Promise<void>; ready: boolean }>({ preferences: defaults(), save: async () => {}, ready: false });
export function PreferencesProvider({ owner, children }: { owner: string | null; children: ReactNode }) {
  const key = 'orbit.preferences.' + (owner || 'local');
  const [preferences, setPreferences] = useState<Preferences>(() => { try { return { ...defaults(), ...JSON.parse(localStorage.getItem(key) || '{}') }; } catch { return defaults(); } });
  setLanguage(preferences.language);
  const [ready, setReady] = useState(false);
  const revision = useRef(0);
  const [error, setError] = useState('');
  useEffect(() => { let active = true; const version = revision.current; const controller = new AbortController(); const timer = setTimeout(() => controller.abort(), 10000); api<Preferences>('/preferences', undefined, controller.signal).then(async p => {
    if (!p.timeZone) p = await api<Preferences>('/preferences', { ...p, timeZone: defaults().timeZone }, controller.signal);
    if (active && version === revision.current) { setPreferences(p); setReady(true); setError(''); }
  }).catch(() => { if (active && version === revision.current) setError('Pengaturan akun belum dimuat. Kamu tetap bisa memilih pengaturan di halaman Pengaturan, lalu menyimpannya lagi.'); }).finally(() => clearTimeout(timer)); return () => { active = false; clearTimeout(timer); controller.abort(); }; }, [owner]);
  useEffect(() => {
    try { localStorage.setItem(key, JSON.stringify(preferences)); } catch { /* Preferences still saved on server. */ }
    document.documentElement.lang = preferences.language;
    const media = matchMedia('(prefers-color-scheme: dark)');
    const apply = () => { document.documentElement.dataset.theme = preferences.theme === 'system' ? media.matches ? 'dark' : 'light' : preferences.theme; };
    apply(); media.addEventListener('change', apply); return () => media.removeEventListener('change', apply);
  }, [preferences, key]);
  async function save(value: Preferences) { revision.current++; const controller = new AbortController(); const timer = setTimeout(() => controller.abort(), 10000); try { const saved = await api<Preferences>('/preferences', value, controller.signal); setPreferences(saved); setReady(true); setError(''); } finally { clearTimeout(timer); } }
  return <Context.Provider value={{ preferences, save, ready }}>{error && <div className="banner error">{error}</div>}{children}</Context.Provider>;
}
export const usePreferences = () => useContext(Context);
export function PreferencesPanel() {
  const { preferences, save } = usePreferences();
  const [draft, setDraft] = useState(preferences);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  useEffect(() => { if (!dirty) setDraft(preferences); }, [preferences, dirty]);
  function change(value: Preferences) { setDraft(value); setDirty(true); setSaved(false); }
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const en = preferences.language === 'en';
  async function update(p: Preferences) { setBusy(true); setError(''); try { await save(p); setDirty(false); setSaved(true); } catch (e) { setError((e as Error).name === 'AbortError' ? (en ? 'Saving timed out. Your choices are still here; try again.' : 'Penyimpanan terlalu lama. Pilihanmu tetap ada; coba simpan lagi.') : (e as Error).message); } finally { setBusy(false); } }
  return <section className="settings-section"><h2>{en ? 'Appearance & language' : 'Tampilan & bahasa'}</h2>
    <label className="field-label">{en ? 'Theme' : 'Tema'}<select disabled={busy} value={draft.theme} onChange={e => change({ ...draft, theme: e.target.value as Preferences['theme'] })}>
      <option value="system">{en ? 'Use device setting' : 'Ikuti perangkat'}</option><option value="light">{en ? 'Light' : 'Terang'}</option><option value="dark">{en ? 'Dark' : 'Gelap'}</option></select></label>
    <label className="field-label">{en ? 'Language' : 'Bahasa'}<select disabled={busy} value={draft.language} onChange={e => change({ ...draft, language: e.target.value as Preferences['language'] })}><option value="id">Indonesia</option><option value="en">English</option></select></label>
    <label className="field-label">{en ? 'Time zone' : 'Zona waktu'}<select disabled={busy} value={draft.timeZone} onChange={e => change({ ...draft, timeZone: e.target.value })}>
      {[...new Set([preferences.timeZone, ...(typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : ['Asia/Jakarta', 'Asia/Makassar', 'Asia/Jayapura', 'Asia/Bangkok', 'UTC'])])].map(zone => <option key={zone}>{zone}</option>)}</select></label>
    <p>{en ? 'Meeting times and reminders use this time zone. AI responses follow your language choice.' : 'Waktu meeting dan reminder mengikuti zona waktu ini. Bahasa jawaban AI mengikuti pilihanmu.'}</p>
    <div className="preferences-actions"><button type="button" className="button primary" disabled={busy} onClick={() => void update(draft)}>{busy ? (en ? 'Saving…' : 'Menyimpan…') : (en ? 'Save appearance & language' : 'Simpan tampilan & bahasa')}</button>{saved && <span role="status">{en ? 'Saved' : 'Tersimpan'}</span>}</div>
    {error && <p role="alert">{error}</p>}
  </section>;
}
