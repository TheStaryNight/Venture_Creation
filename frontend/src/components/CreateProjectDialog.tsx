import { ArrowRight } from 'lucide-react';
import { useAppContext } from '../app/AppContext';
import { Dialog } from '../components/Dialog';
import { t } from '../i18n';

export function CreateProjectDialog() {
  const { bootstrap, error, showProject, setShowProject, createProject } = useAppContext();
  return (
    <>
      {showProject && (
        <Dialog title={t('Project baru')} close={() => setShowProject(false)}>
          <form onSubmit={createProject} className="project-form">
            <label className="field-label" htmlFor="project-name">
              {t('Nama project')}
            </label>
            <input
              id="project-name"
              name="name"
              placeholder={t('Contoh: Riset pasar kelompok')}
              required
              maxLength={80}
              autoFocus
            />
            <label className="field-label" htmlFor="project-description">
              {t('Deskripsi')}
              <span>{t('opsional')}</span>
            </label>
            <textarea
              id="project-description"
              name="description"
              placeholder={t('Apa yang ingin diselesaikan?')}
              maxLength={300}
            />
            <label className="field-label" htmlFor="project-deadline">
              {t('Tenggat')}
              <span>{t('opsional')}</span>
            </label>
            <input type="date" id="project-deadline" name="deadline" />
            <p className="storage-note">
              {bootstrap.cloud ? t('Tersimpan pada akunmu.') : t('Tersimpan di browser ini.')}
            </p>
            {error && (
              <div role="alert" className="banner error">
                {error}
              </div>
            )}
            <div className="dialog-actions">
              <button
                type="button"
                className="button secondary"
                onClick={() => setShowProject(false)}
              >
                {t('Batal')}
              </button>
              <button className="button primary">
                {t('Buat project')}
                <ArrowRight size={15} />
              </button>
            </div>
          </form>
        </Dialog>
      )}
    </>
  );
}
