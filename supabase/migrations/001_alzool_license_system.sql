-- AL-ZOOL license system - Phase 1 database
-- Run this file in Supabase SQL Editor for the AL-ZOOL project.
-- Do NOT put the Supabase service_role/secret key in the WPF application.

create table if not exists public.alzool_license_admins (
    user_id uuid primary key references auth.users(id) on delete cascade,
    created_at timestamptz not null default now()
);

create table if not exists public.alzool_licenses (
    id uuid primary key default gen_random_uuid(),
    code_hash char(64) not null unique,
    plan_type text not null check (plan_type in ('temporary', 'permanent')),
    status text not null default 'active' check (status in ('active', 'revoked', 'expired')),
    expires_at timestamptz,
    device_hash char(64),
    activated_at timestamptz,
    customer_label text,
    notes text,
    created_by uuid references auth.users(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint alzool_license_expiry_rule check (
        (plan_type = 'permanent' and expires_at is null)
        or
        (plan_type = 'temporary' and expires_at is not null)
    )
);

create index if not exists idx_alzool_licenses_status
    on public.alzool_licenses(status);

create index if not exists idx_alzool_licenses_expires_at
    on public.alzool_licenses(expires_at);

create table if not exists public.alzool_license_events (
    id bigint generated always as identity primary key,
    license_id uuid references public.alzool_licenses(id) on delete set null,
    event_type text not null,
    details jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

-- All license data is server-only. The WPF app must call secured Edge Functions.
alter table public.alzool_license_admins enable row level security;
alter table public.alzool_licenses enable row level security;
alter table public.alzool_license_events enable row level security;

-- Intentionally create NO client-facing policies on these tables.
-- Supabase service-role access is reserved for trusted Edge Functions only.

comment on table public.alzool_license_admins is
'AL-ZOOL license administrators. Add the owner auth user UUID here from SQL Editor.';
comment on table public.alzool_licenses is
'AL-ZOOL license records. Only SHA-256 code hashes are stored; raw codes are shown once at creation.';
comment on table public.alzool_license_events is
'Audit trail for license creation, activation, revocation, and validation.';
