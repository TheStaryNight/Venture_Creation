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
        title={t('Project')}
        subtitle={t('Checklist, tenggat, dan draft instruksi untuk setiap project.')}
        action={<NewProjectButton />}
      />
      <div className="list-toolbar">
        <label className="search-field">
          <Search size={17} />
          <input
            aria-label={t('Cari project')}
            placeholder={t('Cari project…')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <span>
          {visibleProjects.length}
          {t('project')}
        </span>
      </div>
      {visibleProjects.length || !search ? (
        <ProjectCards items={visibleProjects} />
      ) : (
        <div className="empty-row">
          {t('Tidak ada project yang cocok dengan')} “{search}”.
        </div>
      )}
      <p className="storage-note">
        {bootstrap.cloud
          ? t('Project tersimpan pada akunmu. Undangan anggota belum tersedia.')
          : t('Project tersimpan di browser ini.')}
      </p>
    </>
  );
}
