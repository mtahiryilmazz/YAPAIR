-- YAPAIR RBAC, API Keys and Second Approval requests
create table if not exists public.api_keys (
    key_id text primary key,
    key_hash text not null,
    name text not null,
    role text not null default 'technician',
    created_at timestamptz not null default now(),
    expires_at timestamptz,
    revoked boolean not null default false
);

create index if not exists idx_api_keys_hash on public.api_keys (key_hash);
create index if not exists idx_api_keys_revoked on public.api_keys (revoked);

create table if not exists public.approval_requests (
    id text primary key,
    machine_key text not null,
    requested_mode text not null,
    reason text,
    status text not null default 'pending',
    approval_code text,
    requested_at timestamptz not null default now(),
    approved_by text,
    approved_at timestamptz
);

create index if not exists idx_approvals_machine on public.approval_requests (machine_key);
create index if not exists idx_approvals_status on public.approval_requests (status);

alter table public.api_keys enable row level security;
alter table public.api_keys force row level security;
alter table public.approval_requests enable row level security;
alter table public.approval_requests force row level security;

revoke all on public.api_keys from anon, authenticated;
revoke all on public.approval_requests from anon, authenticated;
