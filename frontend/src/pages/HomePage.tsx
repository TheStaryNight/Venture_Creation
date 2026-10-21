import {
  ArrowRight,
  ArrowUpRight,
  Check,
  ChevronRight,
  Circle,
  Layers2,
  SlidersHorizontal,
} from 'lucide-react';
import { useAppContext } from '../app/AppContext';
import { shortcuts } from '../app/navigation';
import { ServiceIcon } from '../components/ServiceIcon';
import {
  ActivityList,
  NewProjectButton,
  PageHeading,
  ProjectCards,
  WorkflowComposer,
} from '../components/WorkspaceViews';
import { services } from '../config/services';
import { t } from '../i18n';

export function HomePage() {
  const {
    billing,
    page,
    projects,
    connections,
    history,
    setFilter,
    liveHistory,
    setPrompt,
    navigate,
    openService,
  } = useAppContext();
  if (!(page === 'home')) return null;
  return (
    <>
      <PageHeading
        title={t('Semua pekerjaan, satu tempat.')}
        subtitle={t('Atur proyek dan jalankan pekerjaan di aplikasi yang kamu gunakan.')}
        action={<NewProjectButton />}
      />
      <div className="overview-metrics">
        <button onClick={() => navigate('projects')}>
          <span>{t('Proyek')}</span>
          <strong>{String(projects.length).padStart(2, '0')}</strong>
          <Layers2 size={17} />
        </button>
        <button
          onClick={() => {
            navigate('activity');
            setFilter('planned');
          }}
        >
          <span>{t('Perlu persetujuan')}</span>
          <strong>
            {String(liveHistory.filter((w) => w.status === 'planned').length).padStart(2, '0')}
          </strong>
          <Circle size={17} />
        </button>
        <button
          onClick={() => {
            navigate('activity');
            setFilter('completed');
          }}
        >
          <span>{t('Pekerjaan selesai')}</span>
          <strong>
            {String(liveHistory.filter((w) => w.status === 'completed').length).padStart(2, '0')}
          </strong>
          <Check size={17} />
        </button>
      </div>
      <div className="work-layout">
        <div className="work-main">
          <WorkflowComposer />
          <div className="shortcut-list">
            {shortcuts.map(({ title, icon: Icon, prompt: value }) => (
              <button
                key={title}
                onClick={() => {
                  setPrompt(value);
                  document.querySelector('textarea')?.focus();
                }}
              >
                <Icon size={15} />
                {t(title)}
                <ArrowUpRight size={13} />
              </button>
            ))}
          </div>
          <div className="section-title">
            <h2>{t('Proyek kamu')}</h2>
            <button className="text-button" onClick={() => navigate('projects')}>
              {t('Lihat semua')}
              <ArrowRight size={14} />
            </button>
          </div>
          <ProjectCards items={projects.slice(0, 2)} />
          <div className="section-title">
            <h2>{t('Aktivitas terakhir')}</h2>
            <button
              className="text-button"
              onClick={() => {
                navigate('activity');
                setFilter('all');
              }}
            >
              {t('Lihat semua')}
              <ArrowRight size={14} />
            </button>
          </div>
          <ActivityList items={history.slice(0, 3)} />
        </div>
        <aside className="context-panel">
          <div className="context-heading">
            <h2>{t('Aplikasi')}</h2>
            <button
              className="icon-button"
              aria-label={t('Kelola koneksi')}
              onClick={() => navigate('connections')}
            >
              <SlidersHorizontal size={16} />
            </button>
          </div>
          <p className="context-description">
            {t('Akses dan status aplikasi yang digunakan untuk pekerjaanmu.')}
          </p>
          <div className="connected-list">
            {services.map((s) => (
              <button key={s.id} onClick={() => openService(s.id)}>
                <ServiceIcon id={s.id} />
                <div>
                  <strong>{s.name}</strong>
                  <span>
                    {s.id !== 'discord' && billing?.plan !== 'pro'
                      ? t('Orbit Pro · Terkunci')
                      : connections.find((c) => c.id === s.id)?.connected
                        ? t('Dikonfigurasi')
                        : s.available
                          ? t('Belum terhubung')
                          : t('Hubungkan akun')}
                  </span>
                </div>
                <ChevronRight size={14} />
              </button>
            ))}
          </div>
        </aside>
      </div>
    </>
  );
}
