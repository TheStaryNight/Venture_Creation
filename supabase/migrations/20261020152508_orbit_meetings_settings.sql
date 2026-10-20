alter table orbit.workspaces add column if not exists account_data jsonb not null default '{}';
create table if not exists orbit.reminders (
 id uuid primary key,
 owner_id text not null references orbit.users(id),
 workflow_id uuid not null references orbit.workflows(id),
 title text not null,
 due_at timestamptz not null,
 meeting_at timestamptz not null,
 content text not null,
 encrypted_webhook text not null,
 status text not null check(status in ('pending','sending','sent','failed','needs_review','cancelled','missed'))
);
create index if not exists reminders_due_idx on orbit.reminders(due_at) where status='pending';
create index if not exists reminders_owner_idx on orbit.reminders(owner_id,due_at desc);
alter table orbit.reminders enable row level security;
revoke all on orbit.reminders from public;
insert into orbit.schema_versions(version) values (2) on conflict do nothing;
