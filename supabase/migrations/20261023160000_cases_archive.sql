alter table public.cases
    add column if not exists archived_at timestamptz;

create index if not exists idx_cases_archived on public.cases (archived_at);

comment on column public.cases.archived_at is
    'Arşive alınma zamanı. NULL = aktif vaka.';
