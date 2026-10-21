import { Search } from 'lucide-react';
import { useAppContext } from '../app/AppContext';
import { NewProjectButton, PageHeading, ProjectCards } from '../components/WorkspaceViews';
import { t } from '../i18n';

export function ProjectsPage() {
  const { bootstrap, page, search, setSearch, visibleProjects } = useAppContext();
  if (!(page === 'projects')) return null;
  return (
    <>
      <PageHeading
        title={t('Proyek')}
        subtitle={t('Checklist, tenggat, dan draft instruksi untuk setiap proyek.')}
        action={<NewProjectButton />}
      />
      <div className="list-toolbar">
        <label className="search-field">
          <Search size={17} />
          <input
            aria-label={t('Cari proyek')}
            placeholder={t('Cari proyek…')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <span>
          {visibleProjects.length}
          {t('proyek')}
        </span>
      </div>
      {visibleProjects.length || !search ? (
        <ProjectCards items={visibleProjects} />
      ) : (
        <div className="empty-row">
          {t('Tidak ada proyek yang cocok dengan')} “{search}”.
        </div>
      )}
      <p className="storage-note">
        {bootstrap.cloud
          ? t('Proyek tersimpan pada akunmu. Undangan anggota belum tersedia.')
          : t('Proyek tersimpan di browser ini.')}
      </p>
    </>
  );
}
