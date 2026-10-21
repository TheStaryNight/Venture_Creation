import { useEffect, useRef, useState, type ReactNode } from 'react';
import { t } from './i18n';
import { api } from './lib/api';
import { type Project } from './types';

export type WorkspaceState = { workspace: string; projects: Project[]; draft: string };
export type Bootstrap = {
  cloud: boolean;
  owner: string | null;
  loginUrl: string;
  state?: WorkspaceState;
  revision?: number;
};
export function SessionGate({ children }: { children: (value: Bootstrap) => ReactNode }) {
  const [bootstrap, setBootstrap] = useState<Bootstrap | null>(null);
  const [session, setSession] = useState<{
    database: boolean;
    authenticated: boolean;
    owner: string | null;
    loginUrl: string;
  } | null>(null);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const current = await api<NonNullable<typeof session>>('/session');
        if (!active) return;
        setSession(current);
        if (current.database && !current.authenticated) return;
        const saved = current.database
          ? await api<{ state: WorkspaceState; revision: number }>('/workspace')
          : undefined;
        if (active)
          setBootstrap({
            cloud: current.database,
            owner: current.owner,
            loginUrl: current.loginUrl,
            ...saved,
          });
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : t('Tidak dapat membuka workspace.'));
      }
    }
    void load();
    return () => {
      active = false;
    };
  }, [attempt]);
  if (bootstrap) return children(bootstrap);
  return (
    <main className="session-screen">
      <section className="session-card">
        <span className="label">ORBIT WORKSPACE</span>
        <h1>{t('Semua pekerjaan, satu tempat.')}</h1>
        <p>{t('Masuk untuk menyimpan project dan menjalankan pekerjaan dengan akunmu.')}</p>
        {error ? (
          <>
            <p role="alert" className="banner error">
              {error}
            </p>
            <button
              className="button primary"
              onClick={() => {
                setError('');
                setAttempt(attempt + 1);
              }}
            >
              {t('Coba lagi')}
            </button>
          </>
        ) : session && !session.authenticated ? (
          <>
            <button
              className="button primary"
              onClick={() => window.location.assign(session.loginUrl)}
            >
              {t('Masuk dengan Google')}
            </button>
            <p className="storage-note">
              {t(
                'Google akan meminta izin untuk koneksi Drive dan Calendar. Satu akun memiliki satu workspace pribadi.',
              )}
            </p>
          </>
        ) : (
          <p role="status">{t('Membuka workspace…')}</p>
        )}
      </section>
    </main>
  );
}

export function localWorkspace(): WorkspaceState {
  const read = <T,>(key: string, fallback: T): T => {
    try {
      return JSON.parse(localStorage.getItem(key) || 'null') ?? fallback;
    } catch {
      return fallback;
    }
  };
  return {
    workspace: read('orbit.workspace.v1', 'Venture Creation'),
    projects: read('orbit.projects.v1', []),
    draft: read('orbit.draft.v1', ''),
  };
}
export function useWorkspace(bootstrap: Bootstrap) {
  const [state, setState] = useState<WorkspaceState>(() => bootstrap.state ?? localWorkspace());
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const latest = useRef<WorkspaceState | null>(null);
  const revision = useRef(bootstrap.revision ?? 0);
  const running = useRef(false);
  const blocked = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  async function flush() {
    if (running.current || blocked.current || !latest.current) return;
    running.current = true;
    setSaving(true);
    try {
      while (latest.current) {
        const current: WorkspaceState = latest.current;
        const result = await api<{ revision: number }>('/workspace', {
          revision: revision.current,
          state: current,
        });
        revision.current = result.revision;
        if (latest.current === current) latest.current = null;
      }
      setError('');
    } catch (e) {
      blocked.current = true;
      setError(e instanceof Error ? e.message : t('Perubahan belum tersimpan.'));
    } finally {
      running.current = false;
      setSaving(false);
    }
  }
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (latest.current) {
        e.preventDefault();
        e.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', warn);
    return () => {
      window.removeEventListener('beforeunload', warn);
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);
  function replace(next: WorkspaceState) {
    if (!bootstrap.cloud) {
      localStorage.setItem('orbit.projects.v1', JSON.stringify(next.projects));
      localStorage.setItem('orbit.workspace.v1', JSON.stringify(next.workspace));
      localStorage.setItem('orbit.draft.v1', JSON.stringify(next.draft));
    } else {
      latest.current = next;
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => void flush(), 700);
    }
    setState(next);
  }
  return {
    ...state,
    error,
    saving,
    replace,
    hasPendingChanges: () => latest.current !== null,
    setProjects: (projects: Project[]) => replace({ ...state, projects }),
    setWorkspace: (workspace: string) => replace({ ...state, workspace }),
    setDraft: (draft: string) => replace({ ...state, draft }),
    retry: () => {
      blocked.current = false;
      void flush();
    },
  };
}
