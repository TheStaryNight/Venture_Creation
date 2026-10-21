import { Moon, Sun } from 'lucide-react';
import { useEffect, useState } from 'react';
import { usePreferences } from '../Preferences';

export function ThemeToggle({ onError }: { onError: (message: string) => void }) {
  const { preferences, save, ready } = usePreferences();
  const [busy, setBusy] = useState(false);
  const [dark, setDark] = useState(() => document.documentElement.dataset.theme === 'dark');
  const english = preferences.language === 'en';

  useEffect(() => {
    const root = document.documentElement;
    let previous = root.dataset.theme;
    let timer: ReturnType<typeof setTimeout> | undefined;
    setDark(previous === 'dark');
    const observer = new MutationObserver(() => {
      if (root.dataset.theme === previous) return;
      previous = root.dataset.theme;
      setDark(previous === 'dark');
      clearTimeout(timer);
      root.classList.add('theme-switching');
      timer = setTimeout(() => root.classList.remove('theme-switching'), 420);
    });
    observer.observe(root, { attributes: true, attributeFilter: ['data-theme'] });
    return () => {
      observer.disconnect();
      clearTimeout(timer);
      root.classList.remove('theme-switching');
    };
  }, []);

  async function toggle() {
    if (busy || !ready) return;
    setBusy(true);
    try {
      // PreferencesProvider owns both the saved value and the applied theme.
      await save({ ...preferences, theme: dark ? 'light' : 'dark' });
    } catch {
      onError(
        english ? 'Could not save the theme. Try again.' : 'Tema belum bisa disimpan. Coba lagi.',
      );
    } finally {
      setBusy(false);
    }
  }

  const label = english
    ? dark
      ? 'Switch to light mode'
      : 'Switch to dark mode'
    : dark
      ? 'Ganti ke mode terang'
      : 'Ganti ke mode gelap';
  return (
    <button
      type="button"
      className="icon-button theme-toggle"
      aria-label={label}
      title={label}
      aria-pressed={dark}
      disabled={busy || !ready}
      onClick={() => void toggle()}
    >
      <Sun className="icon-sun" size={17} aria-hidden="true" />
      <Moon className="icon-moon" size={17} aria-hidden="true" />
    </button>
  );
}
