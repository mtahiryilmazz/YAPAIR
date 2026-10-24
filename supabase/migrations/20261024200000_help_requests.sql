create table if not exists public.help_requests (
    id           bigserial primary key,
    machine_key  text,
    machine_label text,
    case_id      text,
    problem_text text not null,
    status       text not null default 'open'
                 check (status in ('open','in_progress','resolved','closed')),
    assigned_to  text,
    admin_note   text,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);

create index if not exists idx_help_status on public.help_requests (status);
create index if not exists idx_help_created on public.help_requests (created_at desc);

alter table public.help_requests enable row level security;
alter table public.help_requests force row level security;
revoke all on public.help_requests from anon, authenticated;
grant select, insert, update, delete on public.help_requests to service_role;

comment on table public.help_requests is
    'Kullanici yardim talepleri - IT ekibi admin panelden gorur';
