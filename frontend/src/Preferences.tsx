import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
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
  const [error, setError] = useState('');
  useEffect(() => { let active = true; api<Preferences>('/preferences').then(async p => {
    if (!p.timeZone) p = await api<Preferences>('/preferences', { ...p, timeZone: defaults().timeZone });
    if (active) { setPreferences(p); setReady(true); }
  }).catch(() => { if (active) setError('Pengaturan belum bisa dimuat. Muat ulang halaman untuk mencoba lagi.'); }); return () => { active = false; }; }, [owner]);
  useEffect(() => {
    try { localStorage.setItem(key, JSON.stringify(preferences)); } catch { /* Preferences still saved on server. */ }
    document.documentElement.lang = preferences.language;
    const media = matchMedia('(prefers-color-scheme: dark)');
    const apply = () => { document.documentElement.dataset.theme = preferences.theme === 'system' ? media.matches ? 'dark' : 'light' : preferences.theme; };
    apply(); media.addEventListener('change', apply); return () => media.removeEventListener('change', apply);
  }, [preferences, key]);
  async function save(value: Preferences) { const saved = await api<Preferences>('/preferences', value); setPreferences(saved); }
  return <Context.Provider value={{ preferences, save, ready }}>{error && <div className="banner error">{error}</div>}{children}</Context.Provider>;
}
export const usePreferences = () => useContext(Context);
export function PreferencesPanel() {
  const { preferences, save, ready } = usePreferences();
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const en = preferences.language === 'en';
  async function update(p: Preferences) { setBusy(true); setError(''); try { await save(p); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <section className="settings-section"><h2>{en ? 'Appearance & language' : 'Tampilan & bahasa'}</h2>
    <label className="field-label">{en ? 'Theme' : 'Tema'}<select disabled={!ready || busy} value={preferences.theme} onChange={e => void update({ ...preferences, theme: e.target.value as Preferences['theme'] })}>
      <option value="system">{en ? 'Use device setting' : 'Ikuti perangkat'}</option><option value="light">{en ? 'Light' : 'Terang'}</option><option value="dark">{en ? 'Dark' : 'Gelap'}</option></select></label>
    <label className="field-label">{en ? 'Language' : 'Bahasa'}<select disabled={!ready || busy} value={preferences.language} onChange={e => void update({ ...preferences, language: e.target.value as Preferences['language'] })}><option value="id">Indonesia</option><option value="en">English</option></select></label>
    <label className="field-label">{en ? 'Time zone' : 'Zona waktu'}<select disabled={!ready || busy} value={preferences.timeZone} onChange={e => void update({ ...preferences, timeZone: e.target.value })}>
      {[...new Set([preferences.timeZone, ...Intl.supportedValuesOf('timeZone')])].map(zone => <option key={zone}>{zone}</option>)}</select></label>
    <p>{en ? 'Meeting times and reminders use this time zone. AI responses follow your language choice.' : 'Waktu meeting dan reminder mengikuti zona waktu ini. Bahasa jawaban AI mengikuti pilihanmu.'}</p>
    {error && <p role="alert">{error}</p>}
  </section>;
}
