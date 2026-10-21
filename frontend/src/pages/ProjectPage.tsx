import { CalendarDays, Plus } from 'lucide-react';
import { useAppContext } from '../app/AppContext';
import { PageHeading, WorkflowComposer } from '../components/WorkspaceViews';
import { t } from '../i18n';
import { dateLabel } from '../lib/dateLabel';

export function ProjectPage() {
  const { bootstrap, page, taskText, setTaskText, project, updateProject } = useAppContext();
  if (!(page === 'project' && project)) return null;
  return (
    <>
      <PageHeading
        title={project.name}
        subtitle={project.description || t('Atur pekerjaan dan kebutuhan proyek ini.')}
        action={
          <span className="deadline-label">
            <CalendarDays size={15} />
            {project.deadline ? dateLabel(project.deadline + 'T12:00:00') : t('Tanpa tenggat')}
          </span>
        }
      />
      <div className="project-work-layout">
        <section className="task-panel">
          <div className="section-title">
            <h2>Checklist</h2>
            <span>
              {project.tasks.filter((t) => t.done).length}/{project.tasks.length}
              {t('selesai')}
            </span>
          </div>
          {!project.tasks.length && (
            <p className="task-empty">{t('Tambahkan pekerjaan pertama untuk proyek ini.')}</p>
          )}
          <div className="task-list">
            {project.tasks.map((task) => (
              <label key={task.id} className={task.done ? 'task done' : 'task'}>
                <input
                  type="checkbox"
                  checked={task.done}
                  onChange={() =>
                    updateProject({
                      tasks: project.tasks.map((t) =>
                        t.id === task.id ? { ...t, done: !t.done } : t,
                      ),
                    })
                  }
                />
                <span>{task.title}</span>
              </label>
            ))}
          </div>
          <form
            className="task-form"
            onSubmit={(e) => {
              e.preventDefault();
              if (
                taskText.trim() &&
                updateProject({
                  tasks: [
                    ...project.tasks,
                    { id: crypto.randomUUID(), title: taskText.trim(), done: false },
                  ],
                })
              )
                setTaskText('');
            }}
          >
            <input
              aria-label={t('Tugas baru')}
              placeholder={t('Tambahkan tugas…')}
              maxLength={200}
              value={taskText}
              onChange={(e) => setTaskText(e.target.value)}
            />
            <button
              className="icon-button"
              aria-label={t('Tambahkan tugas')}
              disabled={!taskText.trim()}
            >
              <Plus size={18} />
            </button>
          </form>
          <p className="storage-note">
            {t('Checklist dicatat di Orbit, belum dikirim ke aplikasi lain.')}
          </p>
        </section>
        <div>
          <WorkflowComposer />
          <p className="storage-note">
            {bootstrap.cloud
              ? t('Draft disimpan otomatis pada akunmu.')
              : t('Draft instruksi disimpan otomatis di browser ini.')}
          </p>
        </div>
      </div>
    </>
  );
}
