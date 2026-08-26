begin;

-- The visible member name is a unique handle. The durable identity used by all
-- foreign keys remains auth.users.id (auth.uid()).
create or replace function public.normalize_member_name(p_name text)
returns text
language sql
immutable
strict
parallel safe
set search_path = ''
as $$
  select pg_catalog.lower(
    pg_catalog.regexp_replace(pg_catalog.btrim(p_name), '[[:space:]]+', ' ', 'g')
  );
$$;

create type public.attendance_status as enum (
  'clocked_out',
  'clocked_in'
);

create type public.activity_status as enum (
  'working',
  'break',
  'meal'
);

create type public.connection_status as enum (
  'disconnected',
  'degraded',
  'connected'
);

create type public.checkout_reason as enum (
  'manual',
  'app_exit',
  'os_shutdown',
  'auto_timeout',
  'admin'
);

create table public.teams (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  timezone text not null default 'Asia/Seoul',
  created_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint teams_name_length_check
    check (pg_catalog.char_length(pg_catalog.btrim(name)) between 1 and 80),
  constraint teams_timezone_not_blank_check
    check (pg_catalog.char_length(pg_catalog.btrim(timezone)) between 1 and 64)
);

create table public.members (
  id uuid primary key references auth.users (id) on delete cascade,
  team_id uuid not null references public.teams (id) on delete restrict,
  display_name text not null,
  normalized_name text generated always as (
    public.normalize_member_name(display_name)
  ) stored,
  avatar_key text not null default 'default',
  sort_order smallint not null,
  is_active boolean not null default true,
  created_at timestamptz not null default pg_catalog.clock_timestamp(),
  updated_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint members_normalized_name_key unique (normalized_name),
  constraint members_team_sort_order_key unique (team_id, sort_order),
  constraint members_team_id_id_key unique (team_id, id),
  constraint members_display_name_length_check
    check (pg_catalog.char_length(display_name) between 1 and 16),
  constraint members_display_name_canonical_check
    check (
      display_name = pg_catalog.regexp_replace(
        pg_catalog.btrim(display_name),
        '[[:space:]]+',
        ' ',
        'g'
      )
      and display_name !~ '[[:cntrl:]]'
    ),
  constraint members_avatar_key_check
    check (
      pg_catalog.char_length(avatar_key) between 1 and 64
      and avatar_key ~ '^[A-Za-z0-9._-]+$'
    ),
  constraint members_sort_order_check check (sort_order between 0 and 3)
);

comment on column public.members.id is
  'The immutable member identity. Always equal to the owning Supabase Auth user UUID.';
comment on column public.members.normalized_name is
  'Globally unique, case-insensitive visible handle used for name claiming; never used as a foreign key.';

create table public.attendance_sessions (
  id uuid primary key default gen_random_uuid(),
  team_id uuid not null,
  member_id uuid not null,
  checked_in_at timestamptz not null,
  checked_out_at timestamptz,
  checkout_reason public.checkout_reason,
  last_heartbeat_at timestamptz not null,
  client_instance_id uuid not null,
  created_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint attendance_sessions_team_member_fkey
    foreign key (team_id, member_id)
    references public.members (team_id, id)
    on delete restrict,
  constraint attendance_sessions_id_member_key unique (id, member_id),
  constraint attendance_sessions_checkout_pair_check
    check (
      (checked_out_at is null and checkout_reason is null)
      or
      (checked_out_at is not null and checkout_reason is not null)
    ),
  constraint attendance_sessions_checkout_order_check
    check (checked_out_at is null or checked_out_at >= checked_in_at),
  constraint attendance_sessions_heartbeat_order_check
    check (
      last_heartbeat_at >= checked_in_at
      and (checked_out_at is null or last_heartbeat_at <= checked_out_at)
    )
);

create unique index attendance_sessions_one_open_per_member_idx
  on public.attendance_sessions (member_id)
  where checked_out_at is null;

create index attendance_sessions_team_checked_in_idx
  on public.attendance_sessions (team_id, checked_in_at desc);

create index attendance_sessions_open_heartbeat_idx
  on public.attendance_sessions (last_heartbeat_at)
  where checked_out_at is null;

create table public.activity_intervals (
  id uuid primary key default gen_random_uuid(),
  attendance_session_id uuid not null,
  member_id uuid not null,
  status public.activity_status not null,
  started_at timestamptz not null,
  ended_at timestamptz,
  created_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint activity_intervals_session_member_fkey
    foreign key (attendance_session_id, member_id)
    references public.attendance_sessions (id, member_id)
    on delete restrict,
  constraint activity_intervals_end_order_check
    check (ended_at is null or ended_at >= started_at)
);

create unique index activity_intervals_one_open_per_session_idx
  on public.activity_intervals (attendance_session_id)
  where ended_at is null;

create index activity_intervals_member_started_idx
  on public.activity_intervals (member_id, started_at desc);

create table public.member_current_state (
  member_id uuid primary key references public.members (id) on delete cascade,
  attendance_session_id uuid,
  attendance_status public.attendance_status not null default 'clocked_out',
  activity_status public.activity_status,
  connection_status public.connection_status not null default 'disconnected',
  checked_in_at timestamptz,
  status_started_at timestamptz,
  last_heartbeat_at timestamptz,
  last_checked_out_at timestamptz,
  updated_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint member_current_state_session_member_fkey
    foreign key (attendance_session_id, member_id)
    references public.attendance_sessions (id, member_id)
    on delete restrict,
  constraint member_current_state_shape_check
    check (
      (
        attendance_status = 'clocked_out'
        and attendance_session_id is null
        and activity_status is null
        and connection_status = 'disconnected'
        and checked_in_at is null
        and status_started_at is null
        and last_heartbeat_at is null
      )
      or
      (
        attendance_status = 'clocked_in'
        and attendance_session_id is not null
        and activity_status is not null
        and connection_status in ('connected', 'degraded')
        and checked_in_at is not null
        and status_started_at is not null
        and last_heartbeat_at is not null
      )
    )
);

alter table public.member_current_state replica identity full;

create table public.team_events (
  id uuid primary key default gen_random_uuid(),
  team_id uuid not null references public.teams (id) on delete restrict,
  actor_member_id uuid not null,
  target_member_id uuid,
  event_type text not null,
  payload jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint team_events_actor_same_team_fkey
    foreign key (team_id, actor_member_id)
    references public.members (team_id, id)
    on delete restrict,
  constraint team_events_target_same_team_fkey
    foreign key (team_id, target_member_id)
    references public.members (team_id, id)
    on delete restrict,
  constraint team_events_type_check
    check (
      pg_catalog.char_length(event_type) between 1 and 64
      and event_type ~ '^[a-z][a-z0-9_]*$'
    ),
  constraint team_events_payload_object_check
    check (pg_catalog.jsonb_typeof(payload) = 'object')
);

create index team_events_team_created_idx
  on public.team_events (team_id, created_at desc, id);

create index team_events_target_created_idx
  on public.team_events (target_member_id, created_at desc)
  where target_member_id is not null;

commit;


