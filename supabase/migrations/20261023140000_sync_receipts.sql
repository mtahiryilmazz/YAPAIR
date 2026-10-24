create table if not exists public.sync_receipts (
    batch_id    text primary key,
    case_count  integer not null check (case_count >= 0),
    api_version text not null default 'v1',
    received_at timestamptz not null default now()
);

alter table public.sync_receipts enable row level security;
alter table public.sync_receipts force row level security;
revoke all on public.sync_receipts from anon, authenticated;
