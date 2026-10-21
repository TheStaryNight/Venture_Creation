import {
  AlertCircle,
  ArrowUpRight,
  ChevronDown,
  ChevronRight,
  CircleCheck,
  Layers2,
  Menu,
  Plug,
  Plus,
  Settings2,
  X,
} from 'lucide-react';
import { UpgradePanel } from '../UpgradePanel';
import { useAppContext } from '../app/AppContext';
import { navigation } from '../app/navigation';
import { ConnectionDialog } from '../components/ConnectionDialog';
import { CreateProjectDialog } from '../components/CreateProjectDialog';
import { Dialog } from '../components/Dialog';
import { WorkflowDialog } from '../components/WorkflowDialog';
import { t } from '../i18n';
import { api } from '../lib/api';
import { ActivityPage } from '../pages/ActivityPage';
import { ConnectionsPage } from '../pages/ConnectionsPage';
import { HomePage } from '../pages/HomePage';
import { ProjectPage } from '../pages/ProjectPage';
import { ProjectsPage } from '../pages/ProjectsPage';
import { SettingsPage } from '../pages/SettingsPage';
export function AppShell() {
  const {
    bootstrap,
    cloud,
    billing,
    setBilling,
    upgrade,
    setUpgrade,
    page,
    projects,
    workspace,
    projectId,
    online,
    active,
    error,
    setError,
    notice,
    showProject,
    setShowProject,
    menu,
    setMenu,
    project,
    connectedCount,
    refresh,
    navigate,
    openProject,
  } = useAppContext();
  return (
    <div className="app-shell">
      {menu && (
        <button
          className="nav-shade"
          aria-label={t('Tutup navigasi')}
          onClick={() => setMenu(false)}
        />
      )}
      <aside className={'sidebar ' + (menu ? 'open' : '')}>
        <button className="brand" onClick={() => navigate('home')}>
          <span className="brand-symbol">
            <Layers2 size={21} />
          </span>
          orbit<span className="brand-caption">workspace</span>
        </button>
        <button className="workspace-switch" onClick={() => navigate('settings')}>
          <span className="workspace-avatar">{workspace.slice(0, 2).toUpperCase()}</span>
          <span>
            {workspace}
            <small>{t('Ruang kerja pribadi')}</small>
          </span>
          <ChevronDown size={14} />
        </button>
        <button
          className="new-work"
          onClick={() => {
            navigate('home');
            setTimeout(() => document.querySelector('textarea')?.focus(), 0);
          }}
        >
          <Plus size={17} />
          {t('Pekerjaan baru')}
          <span>↗</span>
        </button>
        <nav>
          {navigation.map(({ id, title, icon: Icon }) => (
            <button
              key={id}
              className={
                'nav-link ' +
                (page === id || (id === 'projects' && page === 'project') ? 'active' : '')
              }
              onClick={() => navigate(id)}
            >
              <Icon size={18} />
              {title}
              {id === 'projects' && projects.length > 0 && (
                <span className="nav-number">{projects.length}</span>
              )}
            </button>
          ))}
        </nav>
        <div className="sidebar-section">
          <span>{t('PROYEK')}</span>
          <button
            className="icon-button"
            aria-label={t('Buat proyek')}
            onClick={() => setShowProject(true)}
          >
            <Plus size={14} />
          </button>
        </div>
        <div className="project-nav">
          {projects.length ? (
            projects.map((p, i) => (
              <button
                key={p.id}
                onClick={() => openProject(p.id)}
                className={projectId === p.id ? 'selected' : ''}
              >
                <span className={'project-dot color-' + (i % 4)} />
                <span>{p.name}</span>
              </button>
            ))
          ) : (
            <p>{t('Proyekmu akan muncul di sini.')}</p>
          )}
        </div>
        <div className="sidebar-bottom">
          {bootstrap.cloud && (
            <button
              className="nav-link"
              onClick={() => {
                if (cloud.error || cloud.saving || cloud.hasPendingChanges()) {
                  setError(t('Pastikan perubahan tersimpan sebelum keluar.'));
                  return;
                }
                void api('/logout', {})
                  .then(() => window.location.reload())
                  .catch(() => setError(t('Belum dapat keluar. Coba lagi.')));
              }}
            >
              {t('Keluar akun')}
            </button>
          )}
          <button className="plan-entry" onClick={() => setUpgrade(true)}>
            <span>
              <strong>Orbit {billing?.plan === 'pro' ? 'Pro' : 'Free'}</strong>
              <small>
                {billing?.trialAccess
                  ? t('Trial Pro aktif')
                  : billing?.simulationAccess
                    ? t('Pro aktif · simulasi')
                    : billing?.developmentAccess
                      ? t('Akses pengembangan')
                      : t('Discord gratis. Lebih banyak dengan Pro.')}
              </small>
            </span>
            <ArrowUpRight size={16} />
          </button>
          <button className="nav-link" onClick={() => navigate('settings')}>
            <Settings2 size={17} />
            {t('Pengaturan')}
          </button>
          <div className="server-state">
            <span className={'dot ' + (online ? 'green' : online === false ? 'red' : '')} />
            {online === null
              ? t('Menghubungkan…')
              : online
                ? t('Terhubung ke server')
                : t('Server tidak terhubung')}
          </div>
          <div className="local-account">
            <span className="user-avatar">{workspace.charAt(0).toUpperCase()}</span>
            <div>
              {workspace}
              <small>{bootstrap.cloud ? t('Tersimpan di akun') : t('Penyimpanan lokal')}</small>
            </div>
          </div>
        </div>
      </aside>
      <main className="main">
        <header className="topbar">
          <div className="breadcrumbs">
            <button
              className="icon-button mobile-only"
              aria-label={t('Buka navigasi')}
              onClick={() => setMenu(true)}
            >
              <Menu size={20} />
            </button>
            <span>{workspace}</span>
            <ChevronRight size={13} />
            <strong>
              {project?.name || navigation.find((n) => n.id === page)?.title || t('Pengaturan')}
            </strong>
          </div>
          <button className="topbar-connections" onClick={() => navigate('connections')}>
            <Plug size={14} />
            {connectedCount}
            {t('aplikasi terhubung')}
          </button>
        </header>
        <div className="page-content">
          {cloud.error && (
            <div role="alert" className="banner error">
              <span>{cloud.error}</span>
              <button className="text-button" onClick={cloud.retry}>
                {t('Coba simpan lagi')}
              </button>
            </div>
          )}
          {cloud.saving && (
            <p role="status" className="storage-note">
              {t('Menyimpan perubahan…')}
            </p>
          )}
          {error && !active && !showProject && (
            <div className="banner error" role="alert">
              <AlertCircle size={17} />
              <span>{error}</span>
              <button
                className="icon-button"
                aria-label={t('Tutup pesan error')}
                onClick={() => setError('')}
              >
                <X size={16} />
              </button>
            </div>
          )}
          {online === false && (
            <div className="banner error">
              <AlertCircle size={17} />
              <span>
                {t('Server tidak dapat dihubungi. Proyek dan draft lokal tetap bisa dibuka.')}
              </span>
              <button className="text-button" onClick={() => void refresh()}>
                {t('Coba lagi')}
              </button>
            </div>
          )}
          <HomePage />
          <ProjectsPage />
          <ProjectPage />
          <ActivityPage />
          <ConnectionsPage />
          <SettingsPage />
        </div>
      </main>
      {notice && (
        <div className="toast" role="status">
          <CircleCheck size={17} />
          {notice}
        </div>
      )}
      <CreateProjectDialog />
      <ConnectionDialog />
      {upgrade && (
        <Dialog title={t('Lebih banyak dengan Orbit Pro')} close={() => setUpgrade(false)}>
          <UpgradePanel
            billing={billing}
            activated={(value) => {
              setBilling(value);
              void refresh();
            }}
            close={() => setUpgrade(false)}
          />
        </Dialog>
      )}
      <WorkflowDialog />
    </div>
  );
}
