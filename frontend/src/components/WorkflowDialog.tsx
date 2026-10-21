import { ArrowRight, Check, LoaderCircle } from 'lucide-react';
import { DriveFolderPicker } from '../DriveFolderPicker';
import { MeetingPanel } from '../MeetingPanel';
import { useAppContext } from '../app/AppContext';
import { Dialog } from '../components/Dialog';
import { ServiceIcon } from '../components/ServiceIcon';
import { statuses } from '../config/services';
import { t } from '../i18n';

export function WorkflowDialog() {
  const {
    bootstrap,
    meetingDirty,
    setMeetingDirty,
    upgrade,
    active,
    setActive,
    busy,
    confirmNewFolder,
    setConfirmNewFolder,
    error,
    setError,
    refresh,
    execute,
  } = useAppContext();
  return (
    <>
      {active && !upgrade && (
        <Dialog
          title={active.status === 'planned' ? t('Tinjau rencana') : t('Detail pekerjaan')}
          close={() => {
            setActive(null);
            setError('');
            void refresh();
          }}
        >
          <div className="workflow-detail">
            <div className="workflow-meta">
              <span className={'status ' + active.status}>{t(statuses[active.status])}</span>
              <span>
                {active.mode === 'demo' ? t('Uji tanpa mengirim') : t('Pengiriman nyata')}
              </span>
            </div>
            <p className="workflow-prompt">{active.prompt}</p>
            <div className="workflow-notice">
              {active.source === 'template'
                ? t('Rencana contoh tetap — belum disusun dari instruksi kamu. ')
                : t('Periksa tujuan dan isi setiap langkah. ')}
              {active.mode === 'demo'
                ? t('Uji ini tidak mengubah data pada aplikasi.')
                : t('Persetujuan akan menjalankan tindakan nyata.')}
            </div>
            <div className="workflow-steps">
              {active.steps.map((step, i) => (
                <div className="workflow-step" key={step.id}>
                  <span className={'step-index ' + step.status}>
                    {step.status === 'completed' ? (
                      <Check size={15} />
                    ) : step.status === 'running' ? (
                      <LoaderCircle className="spin" size={15} />
                    ) : (
                      i + 1
                    )}
                  </span>
                  <div>
                    <div className="step-heading">
                      <h3>{step.title}</h3>
                      <ServiceIcon id={step.agent} small />
                    </div>
                    <p style={{ whiteSpace: 'pre-wrap' }}>{step.detail}</p>
                    {step.name && (
                      <p>
                        <strong>
                          {step.agent === 'gmail'
                            ? t('Subjek email')
                            : step.agent === 'docs'
                              ? t('Judul dokumen')
                              : step.agent === 'drive'
                                ? t('Nama folder di My Drive')
                                : t('Judul acara di kalender utama')}
                          :
                        </strong>{' '}
                        {step.name}
                      </p>
                    )}
                    {step.start && (
                      <p>
                        <strong>{t('Mulai:')}</strong> {step.start}
                        <br />
                        <strong>{t('Selesai:')}</strong> {step.end}
                      </p>
                    )}
                    {step.createMeet && (
                      <p>
                        <strong>{t('Google Meet:')}</strong>
                        {t('Buat tautan rapat video untuk acara ini.')}
                      </p>
                    )}
                    {step.meetUrl && (
                      <p>
                        <a href={step.meetUrl} target="_blank" rel="noopener noreferrer">
                          {t('Gabung Google Meet ↗')}
                        </a>
                      </p>
                    )}
                    {step.includeLinks && (
                      <p>
                        {t('Tautan hasil langkah Google sebelumnya akan ditambahkan ke pesan ini.')}
                      </p>
                    )}
                    {step.agent === 'drive' && (
                      <>
                        <p>
                          <strong>{t('Tindakan:')}</strong>{' '}
                          {step.action === 'create_folder'
                            ? t('Buat folder baru')
                            : step.action === 'use_existing_folder'
                              ? t('Gunakan folder lama')
                              : step.action === 'list_folder_files'
                                ? t('Lihat daftar file (maksimal 20 file langsung)')
                                : t('Rencana lama: susun ulang')}
                          {t('. Izin berbagi folder tidak diubah.')}
                        </p>
                        {active.status === 'planned' &&
                          ['use_existing_folder', 'list_folder_files'].includes(step.action) && (
                            <DriveFolderPicker
                              step={step}
                              workflowId={active.id}
                              loginUrl={bootstrap.loginUrl}
                              selected={setActive}
                            />
                          )}
                      </>
                    )}
                    {step.agent === 'docs' && (
                      <>
                        <p>
                          {step.action === 'create_document_in_folder'
                            ? t('Simpan di folder pilihan')
                            : t('Simpan di My Drive')}
                        </p>
                        {active.status === 'planned' &&
                          step.action === 'create_document_in_folder' && (
                            <DriveFolderPicker
                              step={step}
                              workflowId={active.id}
                              loginUrl={bootstrap.loginUrl}
                              selected={setActive}
                            />
                          )}
                      </>
                    )}
                    {step.agent === 'gmail' && (
                      <p>
                        <strong>
                          {step.action === 'create_draft'
                            ? t('Simpan draft — belum dikirim')
                            : t('Kirim email setelah persetujuan')}
                        </strong>
                      </p>
                    )}
                    {(step.agent === 'calendar' || step.agent === 'gmail') && (
                      <MeetingPanel
                        step={step}
                        workflow={active}
                        updated={setActive}
                        dirty={(value) => setMeetingDirty((d) => ({ ...d, [step.id]: value }))}
                      />
                    )}
                    {step.resultUrl && (
                      <p>
                        <a href={step.resultUrl} target="_blank" rel="noopener noreferrer">
                          {t('Buka hasil di Google ↗')}
                        </a>
                      </p>
                    )}
                    {step.result && <p className="step-result">{t(step.result)}</p>}
                    <span className={'status ' + step.status}>{t(statuses[step.status])}</span>
                  </div>
                </div>
              ))}
            </div>
            {error && (
              <div className="banner error" role="alert">
                {error}
              </div>
            )}
            <div className="dialog-actions">
              <span className="quiet">
                {active.steps.length}
                {t('langkah')}
              </span>
              {active.status === 'planned' &&
                active.mode === 'live' &&
                active.steps.some((s) => s.agent === 'drive' && s.action === 'create_folder') && (
                  <label className="check-label">
                    <input
                      type="checkbox"
                      checked={confirmNewFolder}
                      onChange={(e) => setConfirmNewFolder(e.target.checked)}
                    />
                    {t(
                      'Saya ingin membuat folder baru, meskipun ada folder dengan nama yang sama.',
                    )}
                  </label>
                )}
              {active.status === 'planned' ? (
                <button
                  className="button primary"
                  disabled={
                    busy ||
                    active.steps.some(
                      (s) =>
                        s.agent === 'docs' &&
                        s.action === 'create_document_in_folder' &&
                        !s.folderId,
                    ) ||
                    Object.values(meetingDirty).some(Boolean) ||
                    active.steps.some((s) => (s.recipientIssues || []).length > 0) ||
                    active.steps.some(
                      (s) =>
                        s.agent === 'drive' &&
                        (!s.action || (s.action !== 'create_folder' && !s.folderId)),
                    ) ||
                    (active.mode === 'live' &&
                      active.steps.some(
                        (s) => s.agent === 'drive' && s.action === 'create_folder',
                      ) &&
                      !confirmNewFolder)
                  }
                  onClick={() => void execute()}
                >
                  {busy && <LoaderCircle size={15} className="spin" />}
                  {active.mode === 'demo' ? t('Jalankan uji coba') : t('Setujui & jalankan')}
                  <ArrowRight size={15} />
                </button>
              ) : (
                <button
                  className="button secondary"
                  onClick={() => {
                    setActive(null);
                    void refresh();
                  }}
                >
                  {t('Tutup')}
                </button>
              )}
            </div>
          </div>
        </Dialog>
      )}
    </>
  );
}
