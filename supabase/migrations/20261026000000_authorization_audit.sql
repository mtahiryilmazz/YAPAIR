-- YAPAIR authorization & repair execution audit trail
create table if not exists public.authorization_audit (
    id bigserial primary key,
    machine_key text,
    mode_before text,
    mode_after text,
    action text not null default 'mode_change',
    target_id text,
    ip_address text,
    note text,
    created_at timestamptz not null default now()
);

create index if not exists idx_auth_audit_machine on public.authorization_audit (machine_key);
create index if not exists idx_auth_audit_action on public.authorization_audit (action);
create index if not exists idx_auth_audit_created on public.authorization_audit (created_at);

alter table public.authorization_audit enable row level security;
alter table public.authorization_audit force row level security;

revoke all on public.authorization_audit from anon, authenticated;
