import { Search } from 'lucide-react';
import { RemindersPanel } from '../MeetingPanel';
import { useAppContext } from '../app/AppContext';
import { ActivityList, PageHeading } from '../components/WorkspaceViews';
import { t } from '../i18n';

export function ActivityPage() {
  const { bootstrap, page, search, setSearch, filter, setFilter, refresh, visibleHistory } =
    useAppContext();
  if (!(page === 'activity')) return null;
  return (
    <>
      <PageHeading
        title={t('Aktivitas')}
        subtitle={t('Periksa langkah, persetujuan, dan hasil pekerjaan.')}
        action={
          <button className="button secondary" onClick={() => void refresh()}>
            {t('Perbarui')}
          </button>
        }
      />
      <div className="list-toolbar">
        <label className="search-field">
          <Search size={17} />
          <input
            aria-label={t('Cari aktivitas')}
            placeholder={t('Cari instruksi…')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <select
          aria-label={t('Filter aktivitas')}
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        >
          <option value="all">{t('Semua aktivitas')}</option>
          <option value="planned">{t('Perlu persetujuan')}</option>
          <option value="completed">{t('Selesai')}</option>
          <option value="failed">{t('Perlu diperiksa')}</option>
          <option value="test">{t('Uji coba')}</option>
        </select>
      </div>
      <RemindersPanel />
      <ActivityList items={visibleHistory} />
      <p className="storage-note">
        {bootstrap.cloud
          ? t('Riwayat tersimpan pada akunmu.')
          : t('Riwayat tersedia selama server berjalan.')}
      </p>
    </>
  );
}
