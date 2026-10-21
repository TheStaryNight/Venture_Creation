import {
  ArrowRight,
  ArrowUpRight,
  ChevronRight,
  CircleCheck,
  Clock3,
  FileText,
  Folder,
  LoaderCircle,
  Plus,
} from 'lucide-react';
import { type ReactNode } from 'react';
import { useAppContext } from '../app/AppContext';
import { ServiceIcon } from '../components/ServiceIcon';
import { services, statuses } from '../config/services';
import { t } from '../i18n';
import { dateLabel } from '../lib/dateLabel';
import { type Project, type Workflow } from '../types';

export function WorkflowComposer() {
  const {
    workspace,
    ai,
    online,
    mode,
    setMode,
    busy,
    setDetail,
    project,
    prompt,
    setPrompt,
    plan,
  } = useAppContext();
  return (
    <section className="composer">
      <div className="composer-header">
        <span className="label">{t('PEKERJAAN BARU')}</span>
        <span className="quiet">{project?.name || workspace}</span>
      </div>
      <textarea
        aria-label={t('Instruksi pekerjaan')}
        maxLength={2000}
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        placeholder={t(
          'Apa yang perlu dikerjakan? Tulis tujuan, waktu, dan aplikasi yang ingin digunakan.',
        )}
      />
      <div className="composer-toolbar">
        <div className="composer-services">
          {services.map((s) => (
            <ServiceIcon key={s.id} id={s.id} small />
          ))}
          <span>
            {services.length}
            {t('aplikasi')}
          </span>
        </div>
        <button
          className="button primary"
          disabled={!prompt.trim() || busy || !online}
          onClick={() => void plan()}
        >
          {busy && <LoaderCircle className="spin" size={16} />}
          {t('Susun langkah')}
          <ArrowRight size={16} />
        </button>
      </div>
      <div className="composer-options">
        <label className="check-label">
          <input
            type="checkbox"
            checked={mode === 'demo'}
            onChange={(e) => setMode(e.target.checked ? 'demo' : 'live')}
          />
          {t('Uji tanpa mengirim')}
        </label>
        <span>{prompt.length}/2000</span>
      </div>
      <div className="composer-note">
        {!ai ? (
          <>
            <span className="dot amber" />
            {t('AI belum terhubung. Uji coba memakai rencana contoh tetap.')}
            <button onClick={() => setDetail('ai')}>
              {t('Lihat kebutuhan')}
              <ArrowUpRight size={13} />
            </button>
          </>
        ) : mode === 'demo' ? (
          t('Langkah diuji tanpa mengubah data di aplikasi.')
        ) : (
          t('Tinjau nama, waktu, dan isi pesan sebelum menyetujui tindakan.')
        )}
      </div>
    </section>
  );
}
export function ActivityList({ items }: { items: Workflow[] }) {
  const { setActive, setError, search, filter } = useAppContext();
  return items.length ? (
    <div className="activity-list">
      {items.map((w) => (
        <button
          className="activity-row"
          key={w.id}
          onClick={() => {
            setError('');
            setActive(w);
          }}
        >
          <span className="activity-icon">
            {w.status === 'completed' ? <CircleCheck size={18} /> : <FileText size={18} />}
          </span>
          <div className="activity-text">
            <strong>{w.prompt}</strong>
            <span>
              {dateLabel(w.createdAt)} · {w.steps.length}
              {t('langkah')}
              {w.mode === 'demo' ? t(' · Uji coba') : ''}
            </span>
          </div>
          <span className={'status ' + w.status}>{t(statuses[w.status])}</span>
          <ChevronRight size={16} />
        </button>
      ))}
    </div>
  ) : (
    <div className="empty-row">
      <Clock3 size={24} />
      <div>
        <strong>
          {t('Belum ada aktivitas')}
          {search || filter !== 'all' ? ' yang cocok' : ''}
        </strong>
        <p>
          {search || filter !== 'all'
            ? t('Ubah pencarian atau filter untuk melihat pekerjaan lain.')
            : t('Rencana dan hasil pekerjaan akan muncul di sini.')}
        </p>
      </div>
    </div>
  );
}
export function ProjectCards({ items }: { items: Project[] }) {
  const { setShowProject, openProject } = useAppContext();
  return items.length ? (
    <div className="project-grid">
      {items.map((p, i) => {
        const done = p.tasks.filter((t) => t.done).length;
        return (
          <button className="project-card" onClick={() => openProject(p.id)} key={p.id}>
            <div className="project-card-top">
              <span className={'project-mark color-' + (i % 4)}>
                <Folder size={19} />
              </span>
              <ArrowUpRight size={16} />
            </div>
            <h3>{p.name}</h3>
            <p>{p.description || t('Tambahkan pekerjaan dan atur kebutuhan proyek.')}</p>
            <div className="progress-track">
              <span
                style={{ width: p.tasks.length ? `${(done / p.tasks.length) * 100}%` : '0%' }}
              />
            </div>
            <div className="project-card-bottom">
              <span>
                {done}/{p.tasks.length}
                {t('tugas selesai')}
              </span>
              <span>{p.deadline ? dateLabel(p.deadline + 'T12:00:00') : t('Tanpa tenggat')}</span>
            </div>
          </button>
        );
      })}
    </div>
  ) : (
    <button className="project-empty" onClick={() => setShowProject(true)}>
      <span className="empty-plus">
        <Plus size={23} />
      </span>
      <div>
        <strong>{t('Buat proyek pertama')}</strong>
        <p>{t('Kumpulkan checklist dan instruksi untuk satu tujuan.')}</p>
      </div>
      <ArrowRight size={18} />
    </button>
  );
}
export function PageHeading({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle: string;
  action?: ReactNode;
}) {
  const { page, workspace } = useAppContext();
  return (
    <div className="page-title">
      <div>
        <span className="overline">{page === 'home' ? 'RUANG KERJA' : workspace}</span>
        <h1>{t(title)}</h1>
        <p>{subtitle}</p>
      </div>
      {action}
    </div>
  );
}
export function NewProjectButton() {
  const { setShowProject } = useAppContext();
  return (
    <button className="button secondary" onClick={() => setShowProject(true)}>
      <Plus size={16} />
      {t('Proyek baru')}
    </button>
  );
}
