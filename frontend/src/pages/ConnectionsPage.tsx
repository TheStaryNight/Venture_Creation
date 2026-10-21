import { ArrowRight, ArrowUpRight, Check, Command, LockKeyhole } from 'lucide-react';
import { ContactsPanel } from '../ContactsPanel';
import { useAppContext } from '../app/AppContext';
import { ServiceIcon } from '../components/ServiceIcon';
import { PageHeading } from '../components/WorkspaceViews';
import { services } from '../config/services';
import { t } from '../i18n';

export function ConnectionsPage() {
  const {
    preferences,
    connectionTab,
    setConnectionTab,
    billing,
    page,
    connections,
    ai,
    setDetail,
    refresh,
    openService,
  } = useAppContext();
  if (!(page === 'connections')) return null;
  return (
    <>
      <PageHeading
        title={t('Koneksi')}
        subtitle={t('Kelola aplikasi yang digunakan untuk menjalankan pekerjaan.')}
        action={
          <button className="button secondary" onClick={() => void refresh()}>
            {t('Perbarui status')}
          </button>
        }
      />
      <div className="connection-tabs">
        <button
          className={connectionTab === 'apps' ? 'button primary' : 'button secondary'}
          onClick={() => setConnectionTab('apps')}
        >
          {preferences.language === 'en' ? 'Apps' : t('Aplikasi')}
        </button>
        <button
          className={connectionTab === 'contacts' ? 'button primary' : 'button secondary'}
          onClick={() => setConnectionTab('contacts')}
        >
          {preferences.language === 'en' ? 'Contacts & Groups' : 'Kontak & Grup'}
        </button>
      </div>
      {connectionTab === 'contacts' ? (
        <ContactsPanel />
      ) : (
        <>
          <section className="ai-connection">
            <span className="ai-icon">
              <Command size={24} />
            </span>
            <div>
              <h2>{t('Pemahaman instruksi')}</h2>
              <p>
                {ai
                  ? t('Konfigurasi AI tersedia. Koneksi diverifikasi saat membuat rencana.')
                  : t('Hubungkan layanan AI untuk menyusun langkah dari instruksi kamu.')}
              </p>
            </div>
            <button className="button secondary" onClick={() => setDetail('ai')}>
              {t('Lihat kebutuhan')}
              <ArrowUpRight size={14} />
            </button>
          </section>
          <div className="connections-grid">
            {services.map((s) => {
              const connected = connections.find((c) => c.id === s.id)?.connected;
              return (
                <section className="connection-card" key={s.id}>
                  <div className="connection-card-top">
                    <ServiceIcon id={s.id} />
                    <span className="connection-status">
                      <span className={'dot ' + (connected ? 'green' : '')} />
                      {s.id !== 'discord' && billing?.plan !== 'pro'
                        ? 'Orbit Pro'
                        : connected
                          ? t('Dikonfigurasi')
                          : s.available
                            ? t('Belum terhubung')
                            : t('Hubungkan akun')}
                    </span>
                  </div>
                  <h2>{s.name}</h2>
                  <p>{t(s.description)}</p>
                  <div className="connection-scope">
                    <Check size={14} />
                    {t(s.action)}
                  </div>
                  <button className="button secondary full" onClick={() => openService(s.id)}>
                    {s.id !== 'discord' && billing?.plan !== 'pro' ? (
                      <>
                        <LockKeyhole size={14} />
                        {t('Buka dengan Pro')}
                      </>
                    ) : (
                      t('Lihat kebutuhan')
                    )}
                    <ArrowRight size={14} />
                  </button>
                </section>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}
