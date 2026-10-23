-- YAPAIR central case store (Supabase/PostgreSQL)
-- Apply in Supabase SQL Editor or via `supabase db push`.
-- Raw text is optional; clients must send it only with explicit consent.

create table if not exists public.machines (
    machine_key text primary key,
    machine_label text not null,
    first_seen timestamptz not null default now(),
    last_seen timestamptz not null default now()
);

comment on column public.machines.machine_key is
    'Pseudonymous SHA-256 key; raw HWID/serial must never be stored.';
comment on column public.machines.machine_label is
    'Computer name label for support staff. Personal data; restrict access.';

create table if not exists public.cases (
    case_id text primary key,
    machine_key text references public.machines(machine_key) on delete set null,
    created_at timestamptz not null,
    normalized_text text not null,
    symptom_code text not null,
    symptom_label text not null,
    issue_family text not null,
    domain text not null,
    outcome text check (outcome in ('resolved', 'not_resolved', 'unknown', 'contradicted')),
    playbook_id text,
    user_confirmation text check (user_confirmation in ('resolved', 'not_resolved', 'unsure', 'not_asked')),
    raw_text text,
    received_at timestamptz not null default now()
);

comment on column public.cases.raw_text is
    'Optional raw complaint. Store only with explicit consent; prefer null.';

create index if not exists idx_cases_machine on public.cases (machine_key);
create index if not exists idx_cases_family on public.cases (issue_family);
create index if not exists idx_cases_symptom on public.cases (symptom_code);
create index if not exists idx_cases_created on public.cases (created_at);
create index if not exists idx_cases_outcome on public.cases (outcome);

-- Row Level Security: deny direct anon/auth access. The sync API uses the
-- service role on the server; the desktop client never holds that key.
alter table public.machines enable row level security;
alter table public.cases enable row level security;
alter table public.machines force row level security;
alter table public.cases force row level security;

revoke all on public.machines from anon, authenticated;
revoke all on public.cases from anon, authenticated;

-- Optional: a separate support role can be granted SELECT later.
-- create role yapair_support nologin;
-- grant select on public.machines, public.cases to yapair_support;
