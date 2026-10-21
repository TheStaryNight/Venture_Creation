import { ArrowDown } from 'lucide-react';
import { PreferencesPanel } from '../Preferences';
import { useAppContext } from '../app/AppContext';
import { PageHeading } from '../components/WorkspaceViews';
import { t } from '../i18n';

export function SettingsPage() {
  const { bootstrap, page, projects, workspace, draft } = useAppContext();
  if (!(page === 'settings')) return null;
  return (
    <>
      <PageHeading
        title={t('Pengaturan')}
        subtitle={t('Atur tampilan, bahasa, zona waktu, dan salinan datamu.')}
      />
      <section className="settings-panel">
        <PreferencesPanel />
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
