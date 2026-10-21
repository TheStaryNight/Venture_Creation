import { useEffect, useState, type FormEvent } from 'react';
import { usePreferences } from '../Preferences';
import { useWorkspace, type Bootstrap } from '../WorkspaceSession';
import { useWorkspaceStatus } from '../hooks/useWorkspaceStatus';
import { t } from '../i18n';
import { api, ApiError } from '../lib/api';
import { type Project, type Workflow } from '../types';
export function useAppController(bootstrap: Bootstrap) {
  const { preferences, ready: preferencesReady } = usePreferences();
  const [connectionTab, setConnectionTab] = useState('apps');
  const [meetingDirty, setMeetingDirty] = useState<Record<string, boolean>>({});
  const cloud = useWorkspace(bootstrap);
  const { billing, setBilling, connections, history, ai, googleConfigured, online, refresh } =
    useWorkspaceStatus();
  const [upgrade, setUpgrade] = useState(false);
  const [page, setPage] = useState('home');
  const { projects, setProjects, workspace, setWorkspace, draft, setDraft } = cloud;
  const [projectId, setProjectId] = useState<string | null>(null);
  const [active, setActive] = useState<Workflow | null>(null);
  const [mode, setMode] = useState('demo');
  const [busy, setBusy] = useState(false);
  const [confirmNewFolder, setConfirmNewFolder] = useState(false);
  useEffect(() => {
    setConfirmNewFolder(false);
    setMeetingDirty({});
  }, [active?.id]);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [showProject, setShowProject] = useState(false);
  const [detail, setDetail] = useState<string | null>(null);
  const [menu, setMenu] = useState(false);
  const [webhookDraft, setWebhookDraft] = useState('');
  const [taskText, setTaskText] = useState('');
  const project = projects.find((p) => p.id === projectId);
  const prompt = project ? project.draft : draft;
  useEffect(() => {
    const result = new URLSearchParams(window.location.search).get('google');
    if (result === 'connected') {
      setPage('connections');
      setNotice(t('Login Google selesai. Status menunjukkan izin yang diberikan.'));
    }
    if (result === 'failed') {
      setPage('connections');
      setError(
        t(
          'Google belum berhasil dihubungkan. Periksa redirect URI, test user, dan izin yang diberikan.',
        ),
      );
    }
    if (result) window.history.replaceState({}, '', window.location.pathname);
  }, []);
  const connectedCount = connections.filter((c) => c.connected).length;
  const liveHistory = history.filter((w) => w.mode === 'live');
  useEffect(() => {
    if (!active || !['queued', 'running'].includes(active.status)) return;
    let cancelled = false;
    const timer = setInterval(() => {
      api<Workflow>('/workflows/' + active.id)
        .then((w) => {
          if (!cancelled) {
            setActive(w);
            setError('');
          }
        })
        .catch(() => {
          if (!cancelled)
            setError(t('Status belum dapat diperbarui. Kami akan memeriksanya kembali.'));
        });
    }, 1000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [active?.id, active?.status]);
  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(''), 3500);
    return () => clearTimeout(timer);
  }, [notice]);
  function save(action: () => void) {
    try {
      action();
      return true;
    } catch {
      setError(t('Penyimpanan browser penuh atau tidak tersedia. Perubahan belum tersimpan.'));
      return false;
    }
  }
  function updateProject(change: Partial<Project>) {
    return save(() =>
      setProjects(projects.map((p) => (p.id === projectId ? { ...p, ...change } : p))),
    );
  }
  function setPrompt(value: string) {
    if (project) updateProject({ draft: value });
    else save(() => setDraft(value));
  }
  function navigate(next: string) {
    setPage(next);
    setProjectId(null);
    setMenu(false);
    setSearch('');
    setError('');
  }
  function openProject(id: string) {
    setPage('project');
    setProjectId(id);
    setMenu(false);
    setTaskText('');
    setError('');
  }
  async function plan() {
    if (!preferencesReady) {
      setError(t('Pengaturan akun masih dimuat. Coba lagi sebentar.'));
      return;
    }
    setMeetingDirty({});
    setBusy(true);
    setError('');
    try {
      setActive(await api<Workflow>('/workflows/plan', { prompt, mode }));
      void refresh();
    } catch (e) {
      if (e instanceof ApiError && e.code === 'upgrade_required') setUpgrade(true);
      else setError(e instanceof Error ? e.message : t('Rencana belum berhasil dibuat.'));
    } finally {
      setBusy(false);
    }
  }
  async function execute() {
    if (!active) return;
    setBusy(true);
    setError('');
    try {
      setActive(await api<Workflow>(`/workflows/${active.id}/execute`, { confirmNewFolder }));
      void refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : t('Pekerjaan belum berhasil dijalankan.'));
    } finally {
      setBusy(false);
    }
  }
  function createProject(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const name = String(data.get('name') || '').trim();
    if (!name) return;
    const p: Project = {
      id: crypto.randomUUID(),
      name,
      description: String(data.get('description') || '').trim(),
      deadline: String(data.get('deadline') || ''),
      tasks: [],
      draft: '',
    };
    if (save(() => setProjects([...projects, p]))) {
      setShowProject(false);
      openProject(p.id);
    }
  }
  const visibleHistory = history.filter(
    (w) =>
      (filter === 'all' ||
        (filter === 'test' ? w.mode === 'demo' : w.mode === 'live' && w.status === filter)) &&
      w.prompt.toLowerCase().includes(search.toLowerCase()),
  );
  const visibleProjects = projects.filter((p) =>
    p.name.toLowerCase().includes(search.toLowerCase()),
  );
  function openService(id: string) {
    if (id !== 'discord' && billing?.plan !== 'pro') setUpgrade(true);
    else setDetail(id);
  }
  return {
    bootstrap,
    preferences,
    connectionTab,
    setConnectionTab,
    meetingDirty,
    setMeetingDirty,
    cloud,
    billing,
    setBilling,
    upgrade,
    setUpgrade,
    page,
    projects,
    workspace,
    setWorkspace,
    draft,
    projectId,
    connections,
    history,
    ai,
    googleConfigured,
    online,
    active,
    setActive,
    mode,
    setMode,
    busy,
    setBusy,
    confirmNewFolder,
    setConfirmNewFolder,
    error,
    setError,
    notice,
    setNotice,
    search,
    setSearch,
    filter,
    setFilter,
    showProject,
    setShowProject,
    detail,
    setDetail,
    menu,
    setMenu,
    webhookDraft,
    setWebhookDraft,
    taskText,
    setTaskText,
    project,
    prompt,
    connectedCount,
    liveHistory,
    refresh,
    save,
    updateProject,
    setPrompt,
    navigate,
    openProject,
    plan,
    execute,
    createProject,
    visibleHistory,
    visibleProjects,
    openService,
  };
}
