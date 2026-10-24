alter table public.machines
    add column if not exists notes text,
    add column if not exists owner text;

comment on column public.machines.notes is
    'Destek personeli notları (ariza gecmisi, ozel durumlar).';
comment on column public.machines.owner is
    'Makineyi kullanan kisi/birim (ornek: Ahmet Yilmaz - Muhasebe).';
