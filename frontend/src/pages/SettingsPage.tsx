import { ArrowDown } from 'lucide-react';
import { PreferencesPanel } from '../Preferences';
import { useAppContext } from '../app/AppContext';
import { PageHeading } from '../components/WorkspaceViews';
import { t } from '../i18n';

export function SettingsPage() {
  const { bootstrap, page, projects, workspace, setWorkspace, draft, setNotice, save } =
    useAppContext();
  if (!(page === 'settings')) return null;
  return (
    <>
      <PageHeading
        title={t('Pengaturan workspace')}
        subtitle={t('Atur nama ruang kerja dan unduh salinan data lokal.')}
      />
      <section className="settings-panel">
        <PreferencesPanel />
        <form
          onSubmit={(e) => {
            e.preventDefault();
            const name = String(new FormData(e.currentTarget).get('workspace') || '').trim();
            if (name && save(() => setWorkspace(name))) setNotice(t('Nama workspace disimpan.'));
          }}
        >
          <label className="field-label" htmlFor="workspace-name">
            {t('Nama workspace')}
          </label>
          <input
            id="workspace-name"
            name="workspace"
            defaultValue={workspace}
            maxLength={60}
            required
          />
          <p>{t('Nama ini tampil di navigasi dan instruksi baru.')}</p>
          <button className="button primary">{t('Simpan perubahan')}</button>
        </form>
        <div className="settings-section">
          <h2>{t('Data lokal')}</h2>
          <p>
            {bootstrap.cloud
              ? t(
                  'Project, checklist, dan draft tersimpan pada akunmu. Unduh salinan sebagai cadangan.',
                )
              : t('Project, checklist, dan draft tersimpan di browser ini.')}
          </p>
          <button
            className="button secondary"
            onClick={() => {
              const url = URL.createObjectURL(
                new Blob(
                  [
                    JSON.stringify(
                      { workspace, projects, draft, exportedAt: new Date().toISOString() },
                      null,
                      2,
                    ),
                  ],
                  { type: 'application/json' },
                ),
              );
              const link = document.createElement('a');
              link.href = url;
              link.download = 'orbit-workspace.json';
              link.click();
              setTimeout(() => URL.revokeObjectURL(url), 1000);
            }}
          >
            <ArrowDown size={15} />
            {t('Unduh data workspace')}
          </button>
        </div>
      </section>
    </>
  );
}
