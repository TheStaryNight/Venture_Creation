import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { setLanguage } from './i18n';
import { api, ApiError } from './lib/api';

export type Preferences = {
  language: 'id' | 'en';
  theme: 'light' | 'dark' | 'system';
  timeZone: string;
  followDeviceTimeZone: boolean;
};
const zones = [
  { value: 'UTC', label: 'UTC+00:00' },
  { value: 'Etc/GMT-7', label: 'UTC+07:00 (WIB)' },
  { value: 'Etc/GMT-8', label: 'UTC+08:00 (WITA)' },
  { value: 'Etc/GMT-9', label: 'UTC+09:00 (WIT)' },
];
function deviceTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}
function preferenceError(e: unknown): string {
  if (e instanceof ApiError && e.status === 401)
    return 'Sesi login sudah berakhir. Masuk kembali dengan Google, lalu simpan pengaturan.';
  if (e instanceof ApiError && (e.status === 404 || e.status === 405))
    return 'Backend belum menyediakan pengaturan akun. Deploy versi backend terbaru di Railway, lalu coba simpan lagi.';
  if (e instanceof SyntaxError)
    return 'Respons backend tidak sesuai. Pastikan Railway dan Vercel memakai versi terbaru yang sama.';
  if (e instanceof Error && e.name === 'AbortError')
    return 'Server belum merespons. Pilihanmu tetap ada; coba simpan lagi.';
  return e instanceof Error ? e.message : 'Pengaturan belum tersimpan. Coba lagi.';
}
const defaults = (): Preferences => ({
  language: 'id',
  theme: 'system',
  timeZone: deviceTimeZone(),
  followDeviceTimeZone: true,
});
const Context = createContext<{
  preferences: Preferences;
  save: (value: Preferences) => Promise<void>;
  ready: boolean;
}>({ preferences: defaults(), save: async () => {}, ready: false });
export function PreferencesProvider({
  owner,
  children,
}: {
  owner: string | null;
  children: ReactNode;
}) {
  const key = 'orbit.preferences.' + (owner || 'local');
  const [preferences, setPreferences] = useState<Preferences>(() => {
    try {
      return { ...defaults(), ...JSON.parse(localStorage.getItem(key) || '{}') };
    } catch {
      return defaults();
    }
  });
  setLanguage(preferences.language);
  const [ready, setReady] = useState(false);
  const revision = useRef(0);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    const version = revision.current;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 10000);
    api<Preferences>('/preferences', undefined, controller.signal)
      .then(async (p) => {
        p = { ...defaults(), ...p };
        if (!p.timeZone || (p.followDeviceTimeZone && p.timeZone !== deviceTimeZone()))
          p = await api<Preferences>(
            '/preferences',
            { ...p, timeZone: defaults().timeZone },
            controller.signal,
          );
        if (active && version === revision.current) {
          setPreferences(p);
          setReady(true);
          setError('');
        }
      })
      .catch((e) => {
        if (active && version === revision.current) setError(preferenceError(e));
      })
      .finally(() => clearTimeout(timer));
    return () => {
      active = false;
      clearTimeout(timer);
      controller.abort();
    };
  }, [owner]);
  useEffect(() => {
    try {
      localStorage.setItem(key, JSON.stringify(preferences));
    } catch {
      /* Preferences still saved on server. */
    }
    document.documentElement.lang = preferences.language;
    const media = matchMedia('(prefers-color-scheme: dark)');
    const apply = () => {
      document.documentElement.dataset.theme =
        preferences.theme === 'system' ? (media.matches ? 'dark' : 'light') : preferences.theme;
    };
    apply();
    media.addEventListener('change', apply);
    return () => media.removeEventListener('change', apply);
  }, [preferences, key]);
  // Refresh the resolved IANA zone on focus and while the app remains open.
  useEffect(() => {
    if (!ready || !preferences.followDeviceTimeZone) return;
    let syncing = false;
    const sync = () => {
      if (syncing || preferences.timeZone === deviceTimeZone()) return;
      syncing = true;
      void save({ ...preferences, timeZone: deviceTimeZone() })
        .catch((error) => setError(preferenceError(error)))
        .finally(() => {
          syncing = false;
        });
    };
    window.addEventListener('focus', sync);
    const timer = setInterval(sync, 60000);
    return () => {
      window.removeEventListener('focus', sync);
      clearInterval(timer);
    };
  }, [preferences, ready]);
  async function save(value: Preferences) {
    revision.current++;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 10000);
    try {
      const resolved = value.followDeviceTimeZone
        ? { ...value, timeZone: deviceTimeZone() }
        : value;
      const saved = await api<Preferences>('/preferences', resolved, controller.signal);
      setPreferences(saved);
      setReady(true);
      setError('');
    } finally {
      clearTimeout(timer);
    }
  }
  return (
    <Context.Provider value={{ preferences, save, ready }}>
      {error && (
        <div className="preferences-banner" role="alert">
          <div className="banner error">{error}</div>
        </div>
      )}
      {children}
    </Context.Provider>
  );
}
export const usePreferences = () => useContext(Context);
export function PreferencesPanel() {
  const { preferences, save } = usePreferences();
  const [draft, setDraft] = useState(() => ({
    ...preferences,
    timeZone: preferences.timeZone,
  }));
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  useEffect(() => {
    if (!dirty) setDraft({ ...preferences });
  }, [preferences, dirty]);
  function change(value: Preferences) {
    setDraft(value);
    setDirty(true);
    setSaved(false);
  }
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const en = preferences.language === 'en';
  async function update(p: Preferences) {
    setBusy(true);
    setError('');
    try {
      await save(p);
      setDirty(false);
      setSaved(true);
    } catch (e) {
      setError(preferenceError(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="settings-section">
      <h2>{en ? 'Appearance & language' : 'Tampilan & bahasa'}</h2>
      <label className="field-label">
        {en ? 'Theme' : 'Tema'}
        <select
          disabled={busy}
          value={draft.theme}
          onChange={(e) => change({ ...draft, theme: e.target.value as Preferences['theme'] })}
        >
          <option value="system">{en ? 'Use device setting' : 'Ikuti perangkat'}</option>
          <option value="light">{en ? 'Light' : 'Terang'}</option>
          <option value="dark">{en ? 'Dark' : 'Gelap'}</option>
        </select>
      </label>
      <label className="field-label">
        {en ? 'Language' : 'Bahasa'}
        <select
          disabled={busy}
          value={draft.language}
          onChange={(e) =>
            change({ ...draft, language: e.target.value as Preferences['language'] })
          }
        >
          <option value="id">Indonesia</option>
          <option value="en">English</option>
        </select>
      </label>
      <label className="field-label">
        {en ? 'Time zone' : 'Zona waktu'}
        <select
          disabled={busy}
          value={draft.followDeviceTimeZone ? 'system' : draft.timeZone}
          onChange={(e) =>
            change({
              ...draft,
              followDeviceTimeZone: e.target.value === 'system',
              timeZone: e.target.value === 'system' ? deviceTimeZone() : e.target.value,
            })
          }
        >
          <option value="system">
            {en ? 'Follow device' : 'Ikuti perangkat'} ({deviceTimeZone()})
          </option>
          {!draft.followDeviceTimeZone && !zones.some((zone) => zone.value === draft.timeZone) && (
            <option value={draft.timeZone}>{draft.timeZone}</option>
          )}
          {zones.map((zone) => (
            <option key={zone.value} value={zone.value}>
              {zone.label}
            </option>
          ))}
        </select>
      </label>
      <p>
        {en
          ? 'Meeting times and reminders use this time zone. AI responses follow your language choice.'
          : 'Waktu meeting dan reminder mengikuti zona waktu ini. Bahasa jawaban AI mengikuti pilihanmu.'}
      </p>
      <div className="preferences-actions">
        <button
          type="button"
          className="button primary"
          disabled={busy}
          onClick={() => void update(draft)}
        >
          {busy
            ? en
              ? 'Saving…'
              : 'Menyimpan…'
            : en
              ? 'Save appearance & language'
              : 'Simpan tampilan & bahasa'}
        </button>
        {saved && <span role="status">{en ? 'Saved' : 'Tersimpan'}</span>}
      </div>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
