import { t } from './i18n';
import { useEffect, useState } from 'react';
import { api, type Step, type Workflow } from './data';

type Folder = { id: string; name: string; modifiedTime?: string };
export function DriveFolderPicker({ step, workflowId, loginUrl, selected }: { step: Step; workflowId: string; loginUrl: string; selected: (workflow: Workflow) => void }) {
  const [search, setSearch] = useState(step.name || '');
  const [folders, setFolders] = useState<Folder[]>([]);
  const [next, setNext] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  useEffect(() => { setFolders([]); setLoaded(false); setNext(null); }, [search]);
  async function load(more = false) {
    setBusy(true); setError('');
    try {
      const term = more ? query : search;
      const result = await api<{ files: Folder[]; nextPageToken?: string }>('/google/folders?search=' + encodeURIComponent(term) + (more && next ? '&pageToken=' + encodeURIComponent(next) : ''));
      setFolders(old => more ? [...old, ...result.files] : result.files); setNext(result.nextPageToken || null); setQuery(term); setLoaded(true);
    } catch (e) { setError(e instanceof Error ? e.message : t("Folder belum dapat dimuat.")); }
    finally { setBusy(false); }
  }
  async function choose(folder: Folder) {
    setBusy(true); setError('');
    try { selected(await api<Workflow>(`/workflows/${workflowId}/steps/${step.id}/folder`, { folderId: folder.id })); }
    catch (e) { setError(e instanceof Error ? e.message : t("Folder belum dapat dipilih.")); }
    finally { setBusy(false); }
  }
  return <section className="drive-picker" aria-label={t("Pilih folder yang sudah ada")}>
    <strong>{step.action === 'list_folder_files' ? t("Lihat daftar file dalam folder") : t("Gunakan folder yang sudah ada")}</strong>
    <p>{t("Pilih folder yang tepat. Nama yang sama dapat dimiliki beberapa folder. Daftar ini memerlukan izin membaca metadata Drive.")}</p>
    {step.folderId && <p className="step-result">{t("Dipilih:")}<a href={'https://drive.google.com/drive/folders/' + encodeURIComponent(step.folderId)} target="_blank" rel="noopener noreferrer">{step.folderName}</a><br/><small>ID: {step.folderId}</small></p>}
    <div className="drive-search"><input aria-label={t("Cari nama folder")} value={search} disabled={busy} onChange={e => setSearch(e.target.value)} placeholder="Nama folder" maxLength={200}/><button type="button" className="button secondary" disabled={busy} onClick={() => void load()}>{t("Cari folder")}</button></div>
    {busy && <p role="status">{t("Memeriksa folder…")}</p>}
    {error && <div role="alert"><p>{error}</p><a href={loginUrl}>{t("Hubungkan ulang Google untuk memperbarui izin")}</a></div>}
    {loaded && folders.length === 0 && <p>{t("Tidak ada folder yang cocok. Coba nama lain atau kosongkan pencarian.")}</p>}
    <div className="drive-folder-list">{folders.map(folder => <div className="drive-folder-row" key={folder.id}><div><strong>{folder.name}</strong><small>ID: {folder.id}</small>{folder.modifiedTime && <small>{t("Diubah:")}{new Date(folder.modifiedTime).toLocaleString('id-ID')}</small>}<a href={'https://drive.google.com/drive/folders/' + encodeURIComponent(folder.id)} target="_blank" rel="noopener noreferrer">{t("Buka folder ↗")}</a></div><button type="button" className="button secondary" disabled={busy} onClick={() => void choose(folder)}>{step.folderId === folder.id ? 'Dipilih' : 'Pilih'}</button></div>)}</div>
    {next && <button type="button" className="button secondary" disabled={busy} onClick={() => void load(true)}>{t("Muat folder berikutnya")}</button>}
  </section>;
}
