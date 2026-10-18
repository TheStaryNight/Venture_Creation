-- Private application schema: never expose this schema through the Supabase Data API.
create schema if not exists orbit;
revoke all on schema orbit from public;
do $$ begin
  if exists (select 1 from pg_roles where rolname = 'anon') then execute 'revoke all on schema orbit from anon'; end if;
  if exists (select 1 from pg_roles where rolname = 'authenticated') then execute 'revoke all on schema orbit from authenticated'; end if;
end $$;

create table if not exists orbit.users (
  id text primary key,
  created_at timestamptz not null default now()
);
create table if not exists orbit.workspaces (
  owner_id text primary key references orbit.users(id),
  state jsonb not null default '{"workspace":"Orbit","projects":[],"draft":""}',
  revision bigint not null default 0,
  updated_at timestamptz not null default now(),
  trial_started_at timestamptz,
  trial_workflows integer not null default 0 check (trial_workflows between 0 and 10)
);
create table if not exists orbit.connections (
  owner_id text not null references orbit.users(id),
  service text not null check (service in ('google','discord')),
  encrypted_payload text not null,
  updated_at timestamptz not null default now(),
  primary key(owner_id, service)
);
create table if not exists orbit.workflows (
  id uuid primary key,
  owner_id text not null references orbit.users(id),
  payload jsonb not null,
  updated_at timestamptz not null default now()
);
create index if not exists workflows_owner_idx on orbit.workflows(owner_id, updated_at desc);
create table if not exists orbit.monthly_usage (
  owner_id text not null references orbit.users(id),
  month date not null,
  plans integer not null default 0 check(plans >= 0),
  primary key(owner_id, month)
);
create table if not exists orbit.schema_versions (
  version integer primary key,
  applied_at timestamptz not null default now()
);
insert into orbit.schema_versions(version) values (1) on conflict do nothing;

-- Defense in depth. Database owner/runtime must be a server-only role; no client policies.
alter table orbit.users enable row level security;
alter table orbit.workspaces enable row level security;
alter table orbit.connections enable row level security;
alter table orbit.workflows enable row level security;
alter table orbit.monthly_usage enable row level security;
alter table orbit.schema_versions enable row level security;
