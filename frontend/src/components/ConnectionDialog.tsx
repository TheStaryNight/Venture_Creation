import { ArrowUpRight } from 'lucide-react';
import { useAppContext } from '../app/AppContext';
import { Dialog } from '../components/Dialog';
import { services } from '../config/services';
import { t } from '../i18n';
import { api } from '../lib/api';

export function ConnectionDialog() {
  const {
    bootstrap,
    connections,
    ai,
    googleConfigured,
    busy,
    setBusy,
    error,
    setError,
    setNotice,
    detail,
    setDetail,
    webhookDraft,
    setWebhookDraft,
    refresh,
  } = useAppContext();
  return (
    <>
      {detail && (
        <Dialog
          title={
            detail === 'ai' ? t('Pemahaman instruksi') : services.find((s) => s.id === detail)!.name
          }
          close={() => setDetail(null)}
        >
          <div className="connection-detail">
            <span className="detail-label">
              {detail === 'ai'
                ? ai
                  ? 'KONFIGURASI TERSEDIA'
                  : 'BELUM TERHUBUNG'
                : detail === 'discord'
                  ? 'PENGIRIMAN PESAN'
                  : 'FOLDER & JADWAL GOOGLE'}
            </span>
            <p>
              {detail === 'ai'
                ? t('Layanan AI mengubah instruksi menjadi rencana yang bisa kamu periksa.')
                : detail === 'discord'
                  ? t('Pesan dikirim ke channel yang dipilih saat membuat webhook.')
                  : 'Hubungkan ulang Google untuk memberikan izin Gmail, Drive, Docs, dan Calendar. Orbit bisa menyimpan draft, mengirim email setelah persetujuan, membuat dokumen, folder, dan meeting. Tautan folder tidak otomatis memberikan izin akses kepada anggota. Pada mode database, koneksi tersimpan terenkripsi dan token diperbarui otomatis jika izin Google masih berlaku.'}
            </p>
            {bootstrap.cloud && detail === 'discord' && (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  setBusy(true);
                  void api('/connections/discord', { url: webhookDraft.trim() })
                    .then(() => {
                      setWebhookDraft('');
                      setDetail(null);
                      setNotice(t('Webhook tersimpan untuk akunmu.'));
                      void refresh();
                    })
                    .catch((e) =>
                      setError(e instanceof Error ? e.message : t('Gagal menyimpan webhook.')),
                    )
                    .finally(() => setBusy(false));
                }}
              >
                <label className="field-label" htmlFor="discord-webhook">
                  {t('URL webhook untuk workspace ini')}
                </label>
                <input
                  id="discord-webhook"
                  type="password"
                  autoComplete="off"
                  value={webhookDraft}
                  onChange={(e) => setWebhookDraft(e.target.value)}
                  required
                />
                <button className="button primary" disabled={busy}>
                  {t('Simpan koneksi')}
                </button>
                {error && <p role="alert">{error}</p>}
              </form>
            )}
            <h3>{t('Yang diperlukan')}</h3>
            <ul>
              {(detail === 'ai'
                ? [
                    t('Akun Google AI Studio (Gemini) atau OpenAI API dengan kuota tersedia.'),
                    t('API key dan model yang mendukung Structured Outputs.'),
                    t('Konfigurasi oleh pengelola aplikasi di server.'),
                  ]
                : detail === 'discord'
                  ? [
                      t('Server dan channel Discord untuk kelompok.'),
                      t('Izin Manage Webhooks pada channel tersebut.'),
                      t('URL webhook disimpan oleh pengelola aplikasi di server.'),
                    ]
                  : [
                      t('Google Cloud project milik tim.'),
                      `${detail === 'drive' ? 'Google Drive' : detail === 'docs' ? 'Google Docs dan Google Drive' : detail === 'gmail' ? 'Gmail' : 'Google Calendar'} ${t('API diaktifkan.')}`,
                      t('OAuth client aplikasi web dan akun anggota sebagai test user.'),
                      'Redirect URI: ' + window.location.origin + '/api/google/callback',
                    ]
              ).map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            <p className="detail-footnote">
              {t(
                'API key, client secret, dan URL webhook tetap disimpan di server. Jangan bagikan melalui instruksi pekerjaan.',
              )}
            </p>
            {['drive', 'calendar', 'meet', 'gmail', 'docs'].includes(detail) && (
              <div className="workflow-notice">
                {googleConfigured
                  ? t(
                      'Konfigurasi OAuth tersedia. Login Google akan dibuka untuk memilih akun dan izin.',
                    )
                  : t(
                      'Pengelola perlu mengisi Google__ClientId dan Google__ClientSecret, lalu restart backend.',
                    )}
              </div>
            )}
            <div className="dialog-actions">
              {['drive', 'calendar', 'meet', 'gmail', 'docs'].includes(detail) && (
                <button
                  className="button primary"
                  disabled={!googleConfigured}
                  onClick={() => window.location.assign(bootstrap.loginUrl)}
                >
                  {connections.find((c) => c.id === detail)?.connected
                    ? t('Hubungkan ulang Google')
                    : t('Hubungkan Google')}
                  <ArrowUpRight size={14} />
                </button>
              )}
              <button className="button secondary" onClick={() => setDetail(null)}>
                {t('Tutup')}
              </button>
            </div>
          </div>
        </Dialog>
      )}
    </>
  );
}
