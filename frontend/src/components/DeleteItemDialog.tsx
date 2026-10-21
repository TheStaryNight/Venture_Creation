import { useAppContext } from '../app/AppContext';
import { Dialog } from './Dialog';

export function DeleteItemDialog() {
  const { deletion, setDeletion, deleting, deleteError, confirmDeletion, preferences, bootstrap } =
    useAppContext();
  if (!deletion) return null;
  const english = preferences.language === 'en';
  const project = deletion.kind === 'project';
  const title = english
    ? `Delete ${project ? 'project' : 'conversation'}?`
    : `Hapus ${project ? 'project' : 'percakapan'}?`;
  return (
    <Dialog
      title={title}
      close={() => {
        if (!deleting) setDeletion(null);
      }}
    >
      <p className="deletion-name">{deletion.name}</p>
      <p>
        {english
          ? 'This removes the Orbit record. Files, calendar events and messages already sent remain in their apps.'
          : 'Data ini akan dihapus dari Orbit. File, event kalender, dan pesan yang sudah terkirim tetap ada di aplikasinya.'}
      </p>
      {!project && (
        <p>
          {english
            ? 'Pending reminders will also be removed.'
            : 'Reminder yang masih menunggu juga akan dihapus.'}
        </p>
      )}
      {project && bootstrap.cloud && (
        <p>
          {english
            ? 'The deletion is saved to your account. Keep this page open until saving completes.'
            : 'Penghapusan disimpan ke akunmu. Tunggu sampai penyimpanan selesai sebelum menutup halaman.'}
        </p>
      )}
      {deleteError && <p role="alert">{deleteError}</p>}
      <div className="dialog-actions">
        <button className="button secondary" disabled={deleting} onClick={() => setDeletion(null)}>
          {english ? 'Cancel' : 'Batal'}
        </button>
        <button
          className="button primary"
          disabled={deleting}
          onClick={() => void confirmDeletion()}
        >
          {deleting ? (english ? 'Deleting…' : 'Menghapus…') : english ? 'Delete' : 'Hapus'}
        </button>
      </div>
    </Dialog>
  );
}
