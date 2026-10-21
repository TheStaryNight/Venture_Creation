import { useEffect, useState } from 'react';
import { api } from './lib/api';
import { usePreferences } from './Preferences';
import { type Step, type Workflow } from './types';
export function MeetingPanel({
  step,
  workflow,
  updated,
  dirty,
}: {
  step: Step;
  workflow: Workflow;
  updated: (w: Workflow) => void;
  dirty: (value: boolean) => void;
}) {
  const { preferences } = usePreferences();
  const en = preferences.language === 'en';
  const [minutes, setMinutes] = useState(step.reminderMinutes ?? 30);
  const [choices, setChoices] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const changed = Object.keys(choices).length > 0 || minutes !== (step.reminderMinutes ?? 30);
  useEffect(() => {
    dirty(changed);
  }, [changed]);
  const zone = workflow.timeZone || preferences.timeZone;
  const date = (s: string) =>
    new Date(s).toLocaleString(en ? 'en-GB' : 'id-ID', {
      timeZone: zone,
      dateStyle: 'full',
      timeStyle: 'short',
    });
  async function save() {
    setBusy(true);
    setError('');
    try {
      updated(
        await api<Workflow>(`/workflows/${workflow.id}/steps/${step.id}/meeting`, {
          choices,
          reminderMinutes: step.reminderMinutes == null ? null : minutes,
        }),
      );
      setChoices({});
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="meeting-panel">
      {step.start && (
        <p>
          {date(step.start)}
          <br />
          {date(step.end)}
          <br />
          <strong>{zone}</strong>
        </p>
      )}
      <h4>
        {step.agent === 'gmail'
          ? en
            ? 'Email recipients'
            : 'Penerima email'
          : en
            ? 'Invitees'
            : 'Peserta yang diundang'}
      </h4>
      {(step.attendees || []).length ? (
        <ul>
          {step.attendees.map((email) => (
            <li key={email}>{email}</li>
          ))}
        </ul>
      ) : (
        <p>{en ? 'No invitees selected.' : 'Belum ada peserta yang dipilih.'}</p>
      )}
      {(step.recipientIssues || []).map((issue) => (
        <label className="field-label" key={issue.name}>
          {issue.name}
          {issue.choices.some((c) => c.emails.length) ? (
            <select
              disabled={workflow.status !== 'planned'}
              value={choices[issue.name] || ''}
              onChange={(e) => setChoices({ ...choices, [issue.name]: e.target.value })}
            >
              <option value="">{en ? 'Choose a match' : 'Pilih yang dimaksud'}</option>
              {issue.choices
                .filter((c) => c.emails.length)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label} ({c.emails.join(', ')})
                  </option>
                ))}
            </select>
          ) : (
            <p role="alert">
              {en
                ? 'No recipients found. Add contacts or group members, then create a new plan.'
                : 'Peserta belum ditemukan. Lengkapi kontak atau anggota grup, lalu susun rencana baru.'}
            </p>
          )}
        </label>
      ))}
      {step.reminderMinutes != null && (
        <label className="field-label">
          {en
            ? 'Discord reminder (minutes before meeting)'
            : 'Reminder Discord (menit sebelum meeting)'}
          <input
            type="number"
            min={0}
            max={10080}
            disabled={workflow.status !== 'planned'}
            value={minutes}
            onChange={(e) => setMinutes(Number(e.target.value))}
          />
          <p>
            {en ? 'Saved reminder:' : 'Reminder tersimpan:'}{' '}
            {date(
              new Date(new Date(step.start).getTime() - step.reminderMinutes * 60000).toISOString(),
            )}
          </p>
        </label>
      )}
      {workflow.status === 'planned' &&
        (Object.keys(choices).length > 0 || minutes !== (step.reminderMinutes ?? 30)) && (
          <button className="button secondary" disabled={busy} onClick={() => void save()}>
            {en ? 'Save changes' : 'Simpan perubahan'}
          </button>
        )}
      <p>
        {step.agent === 'gmail'
          ? en
            ? 'The selected email action runs only after approval. Test mode does not create drafts or send email.'
            : 'Tindakan email hanya berjalan setelah persetujuan. Mode uji tidak membuat draft atau mengirim email.'
          : workflow.mode === 'demo'
            ? en
              ? 'Test mode: no invitations or reminders will be sent.'
              : 'Mode uji: undangan dan reminder tidak dikirim.'
            : en
              ? 'Invitations are sent only after approval.'
              : 'Undangan baru dikirim setelah kamu menyetujui rencana.'}
      </p>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
type Reminder = { id: string; title: string; dueAt: string; status: string };
export function RemindersPanel() {
  const { preferences } = usePreferences();
  const en = preferences.language === 'en';
  const [items, setItems] = useState<Reminder[]>([]);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    const load = () =>
      api<Reminder[]>('/reminders')
        .then((r) => {
          if (active) setItems(r);
        })
        .catch((e) => {
          if (active) setError(e.message);
        });
    void load();
    const t = setInterval(() => void load(), 30000);
    return () => {
      active = false;
      clearInterval(t);
    };
  }, []);
  const labels: Record<string, string> = en
    ? {
        pending: 'Scheduled',
        sending: 'Sending',
        sent: 'Sent',
        missed: 'Missed',
        failed: 'Failed',
        needs_review: 'Check delivery before retrying',
        cancelled: 'Cancelled',
      }
    : {
        pending: 'Terjadwal',
        sending: 'Mengirim',
        sent: 'Terkirim',
        missed: 'Terlewat',
        failed: 'Gagal',
        needs_review: 'Periksa hasil pengiriman',
        cancelled: 'Dibatalkan',
      };
  return (
    <section className="reminders-panel">
      <h2>{en ? 'Discord reminders' : 'Reminder Discord'}</h2>
      {error && <p role="alert">{error}</p>}
      {!items.length && (
        <p>{en ? 'No scheduled reminders yet.' : 'Belum ada reminder terjadwal.'}</p>
      )}
      {items.map((r) => (
        <div className="account-row" key={r.id}>
          <div>
            <strong>{r.title}</strong>
            <p>
              {new Date(r.dueAt).toLocaleString(en ? 'en-GB' : 'id-ID', {
                timeZone: preferences.timeZone,
              })}{' '}
              · {labels[r.status]}
            </p>
          </div>
          {r.status === 'pending' && (
            <button
              onClick={() => {
                void api(`/reminders/${r.id}/cancel`, {})
                  .then(() =>
                    setItems(items.map((i) => (i.id === r.id ? { ...i, status: 'cancelled' } : i))),
                  )
                  .catch((e) => setError(e.message));
              }}
            >
              {en ? 'Cancel' : 'Batalkan'}
            </button>
          )}
        </div>
      ))}
    </section>
  );
}
