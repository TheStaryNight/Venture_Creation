import { useEffect, useState } from 'react';
import { api } from './data';
import { usePreferences } from './Preferences';
type Contact = { id: string; name: string; email: string };
type Group = { id: string; name: string; members: string[] };
const emptyId = '00000000-0000-0000-0000-000000000000';
export function ContactsPanel() {
  const en = usePreferences().preferences.language === 'en';
  const [contacts, setContacts] = useState<Contact[]>([]); const [groups, setGroups] = useState<Group[]>([]);
  const [contact, setContact] = useState<Contact>({ id: emptyId, name: '', email: '' });
  const [group, setGroup] = useState<Group>({ id: emptyId, name: '', members: [] });
  const [memberSearch, setMemberSearch] = useState('');
  const [search, setSearch] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function load() { const [c, g] = await Promise.all([api<Contact[]>('/contacts'), api<Group[]>('/groups')]); setContacts(c); setGroups(g); }
  useEffect(() => { void load().catch(e => setError(e.message)); }, []);
  async function act(path: string, body: object, reset?: () => void) { setBusy(true); setError(''); try { await api(path, body); reset?.(); await load(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <section className="contacts-panel"><p>{en ? 'Private contacts for your workspace. Use a contact or group name when planning a meeting.' : 'Kontak pribadi untuk workspace kamu. Sebut nama kontak atau grup saat membuat meeting.'}</p>
    {error && <p className="banner error" role="alert">{error}</p>}
    <input aria-label={en ? 'Search contacts' : 'Cari kontak'} placeholder={en ? 'Search names or emails' : 'Cari nama atau email'} value={search} onChange={e => setSearch(e.target.value)}/>
    <div className="account-columns"><section><h2>{en ? 'Contacts' : 'Kontak'}</h2>
      <form onSubmit={e => { e.preventDefault(); void act('/contacts', contact, () => setContact({ id: emptyId, name: '', email: '' })); }}>
        <label>{en ? 'Name' : 'Nama'}<input required maxLength={100} value={contact.name} onChange={e => setContact({ ...contact, name: e.target.value })}/></label>
        <label>Email<input required type="email" maxLength={254} value={contact.email} onChange={e => setContact({ ...contact, email: e.target.value })}/></label>
        <button className="button primary" disabled={busy}>{en ? 'Save contact' : 'Simpan kontak'}</button>
        {contact.id !== emptyId && <button type="button" className="button secondary" onClick={() => setContact({ id: emptyId, name: '', email: '' })}>{en ? 'Cancel edit' : 'Batal edit'}</button>}
      </form>
      {contacts.filter(c => (c.name + c.email).toLowerCase().includes(search.toLowerCase())).map(c => <div className="account-row" key={c.id}><div><strong>{c.name}</strong><p>{c.email}</p></div><button disabled={busy} onClick={() => setContact(c)}>{en ? 'Edit' : 'Edit'}</button><button disabled={busy} onClick={() => { if (confirm(en ? `Delete ${c.name}? They will also be removed from groups.` : `Hapus ${c.name}? Kontak ini juga akan dikeluarkan dari grup.`)) void act(`/contacts/${c.id}/delete`, {}); }}>{en ? 'Delete' : 'Hapus'}</button></div>)}
      {!contacts.length && <p>{en ? 'No contacts yet. Add your first teammate above.' : 'Belum ada kontak. Tambahkan teman timmu di atas.'}</p>}
    </section><section><h2>{en ? 'Groups' : 'Grup'}</h2>
      <form onSubmit={e => { e.preventDefault(); void act('/groups', group, () => { setGroup({ id: emptyId, name: '', members: [] }); setMemberSearch(''); }); }}>
        <label>{en ? 'Group name' : 'Nama grup'}<input required maxLength={100} value={group.name} onChange={e => setGroup({ ...group, name: e.target.value })}/></label>
        <fieldset className="member-picker"><legend>{en ? 'Members' : 'Anggota'}</legend>
          <input type="search" aria-label={en ? 'Find group members' : 'Cari anggota grup'} placeholder={en ? 'Type a contact name or email' : 'Ketik nama atau email kontak'} value={memberSearch} onChange={e => setMemberSearch(e.target.value)}/>
          <p className="member-help">{contacts.length ? (en ? 'Select contacts to add them to this group.' : 'Centang kontak yang mau dimasukkan ke grup.') : (en ? 'Save a contact in the Contacts form first. They will appear here for selection.' : 'Simpan nama dan email di bagian Kontak dulu. Setelah itu, pilih kontaknya di sini.')}</p>
          <div className="member-list">{contacts.filter(c => (c.name + ' ' + c.email).toLowerCase().includes(memberSearch.toLowerCase())).map(c => <label className="member-option" key={c.id}><input type="checkbox" disabled={busy} checked={group.members.includes(c.id)} onChange={e => { const checked = e.target.checked; setGroup(g => ({ ...g, members: checked ? [...g.members, c.id] : g.members.filter(id => id !== c.id) })); }}/><span><strong>{c.name}</strong><small>{c.email}</small></span></label>)}</div>
          {contacts.length > 0 && !contacts.some(c => (c.name + ' ' + c.email).toLowerCase().includes(memberSearch.toLowerCase())) && <p>{en ? 'No matching contacts.' : 'Kontak tidak ditemukan. Coba nama atau email lain.'}</p>}
          <span className="member-count">{group.members.length} {en ? 'selected' : 'anggota dipilih'}</span>
        </fieldset>
        <button className="button primary" disabled={busy}>{en ? 'Save group' : 'Simpan grup'}</button>
        {group.id !== emptyId && <button type="button" className="button secondary" onClick={() => setGroup({ id: emptyId, name: '', members: [] })}>{en ? 'Cancel edit' : 'Batal edit'}</button>}
      </form>
      {groups.filter(g => g.name.toLowerCase().includes(search.toLowerCase())).map(g => <div className="account-row" key={g.id}><div><strong>{g.name}</strong><p>{g.members.length} {en ? 'members' : 'anggota'}</p></div><button disabled={busy} onClick={() => setGroup(g)}>Edit</button><button disabled={busy} onClick={() => { if (confirm(en ? `Delete group ${g.name}? Contacts will stay.` : `Hapus grup ${g.name}? Kontaknya tetap disimpan.`)) void act(`/groups/${g.id}/delete`, {}); }}>{en ? 'Delete' : 'Hapus'}</button></div>)}
    </section></div>
  </section>;
}
