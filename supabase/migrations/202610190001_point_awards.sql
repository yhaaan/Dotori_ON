begin;

-- Points that are not a daily check-in.
--
-- Until now every point was a member_check_ins row, and four functions summed
-- that table directly. An event reward is not a day, so it cannot be a check-in
-- row: (member_id, local_date) is that table's whole rule. This ledger holds
-- everything else, keyed by where it came from, so a source can never pay the
-- same award twice and a finished event leaves its points behind untouched.
--
-- Nothing may be written directly. Each source gets its own security definer
-- function that decides what an award is worth.
create table public.member_point_awards (
  member_id uuid not null references public.members (id) on delete cascade,
  source text not null,
  award_key text not null,
  points integer not null,
  claimed_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint member_point_awards_pkey primary key (member_id, source, award_key),
  constraint member_point_awards_points_check check (points between 0 and 1000),
  constraint member_point_awards_source_check check (source ~ '^[a-z0-9-]{1,40}$'),
  constraint member_point_awards_key_check check (pg_catalog.length(award_key) between 1 and 40)
);

alter table public.member_point_awards enable row level security;

create policy member_point_awards_select_same_team
on public.member_point_awards
for select
to authenticated
using (
  (select auth.uid()) is not null
  and public.can_access_member(member_id)
);

revoke all on public.member_point_awards from anon, authenticated;
grant select on public.member_point_awards to authenticated;

-- The one definition of a member's total. Everything that reports points goes
-- through here, so a new source only has to write to the ledger.
create or replace function public.member_total_points(p_member_id uuid)
returns integer
language sql
stable
security definer
set search_path = ''
as $$
  select (
    (
      select coalesce(pg_catalog.sum(c.points), 0)
      from public.member_check_ins as c
      where c.member_id = p_member_id
    ) + (
      select coalesce(pg_catalog.sum(a.points), 0)
      from public.member_point_awards as a
      where a.member_id = p_member_id
    )
  )::integer;
$$;

-- Only the security definer readers call it; nobody reads another team's total through it.
revoke all on function public.member_total_points(uuid) from public, anon, authenticated;

-- The four readers below are unchanged except that their total now comes from
-- member_total_points instead of summing member_check_ins themselves.

create or replace function public.claim_daily_check_in()
returns table (
  claimed boolean,
  awarded integer,
  total_points integer,
  streak_days integer,
  claimed_on date
)
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member_id uuid := (select auth.uid());
  v_today date;
  v_awarded integer;
begin
  if v_member_id is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  if not exists (
    select 1 from public.members as m where m.id = v_member_id and m.is_active
  ) then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  v_today := public.team_local_date(pg_catalog.clock_timestamp());

  insert into public.member_check_ins as c (member_id, local_date)
  values (v_member_id, v_today)
  on conflict (member_id, local_date) do nothing
  returning c.points into v_awarded;

  return query
  select
    true,
    coalesce(v_awarded, 0),
    public.member_total_points(v_member_id),
    public.daily_check_in_streak(v_member_id),
    v_today;
end;
$$;

create or replace function public.daily_check_in_status()
returns table (
  claimed boolean,
  total_points integer,
  streak_days integer,
  claimed_on date
)
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_member_id uuid := (select auth.uid());
  v_today date;
begin
  if v_member_id is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  v_today := public.team_local_date(pg_catalog.clock_timestamp());

  return query
  select
    exists (
      select 1
      from public.member_check_ins as c
      where c.member_id = v_member_id and c.local_date = v_today
    ),
    public.member_total_points(v_member_id),
    public.daily_check_in_streak(v_member_id),
    v_today;
end;
$$;

create or replace function public.team_activity_ranking(
  p_to date,
  p_from date default null
)
returns table (
  member_id uuid,
  display_name text,
  sort_order smallint,
  attendance_seconds integer,
  work_seconds integer,
  break_seconds integer,
  meal_seconds integer,
  total_points integer,
  streak_days integer
)
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_team_id uuid := public.current_member_team_id();
  v_from date;
  v_start timestamptz;
  v_end timestamptz;
begin
  if v_team_id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  if p_to is null then
    raise exception using errcode = '22023', message = 'to_date_required';
  end if;

  v_from := coalesce(
    p_from,
    (
      select public.team_local_date(pg_catalog.min(s.checked_in_at))
      from public.attendance_sessions as s
      join public.members as m on m.id = s.member_id
      where m.team_id = v_team_id
    ),
    p_to
  );

  if v_from > p_to then
    v_from := p_to;
  end if;

  if p_to - v_from > 3660 then
    raise exception using errcode = '22023', message = 'date_range_too_wide';
  end if;

  v_start := public.team_day_start(v_from);
  v_end := public.team_day_start(p_to + 1);

  return query
  select
    m.id,
    m.display_name,
    m.sort_order,
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(s.checked_out_at, v_now), v_end) - greatest(s.checked_in_at, v_start)))), 0)::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
        and s.checked_in_at < v_end
        and coalesce(s.checked_out_at, v_now) > v_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(a.ended_at, v_now), v_end) - greatest(a.started_at, v_start)))), 0)::integer
      from public.activity_intervals as a
      where a.member_id = m.id
        and a.status = 'working'
        and a.started_at < v_end
        and coalesce(a.ended_at, v_now) > v_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(a.ended_at, v_now), v_end) - greatest(a.started_at, v_start)))), 0)::integer
      from public.activity_intervals as a
      where a.member_id = m.id
        and a.status = 'break'
        and a.started_at < v_end
        and coalesce(a.ended_at, v_now) > v_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(a.ended_at, v_now), v_end) - greatest(a.started_at, v_start)))), 0)::integer
      from public.activity_intervals as a
      where a.member_id = m.id
        and a.status = 'meal'
        and a.started_at < v_end
        and coalesce(a.ended_at, v_now) > v_start
    ),
    public.member_total_points(m.id),
    public.daily_check_in_streak(m.id)
  from public.members as m
  where m.team_id = v_team_id
    and m.is_active
  order by 5 desc, m.sort_order;
end;
$$;

create or replace function public.admin_member_overview()
returns table (
  member_id uuid,
  display_name text,
  avatar_key text,
  sort_order smallint,
  is_active boolean,
  is_admin boolean,
  created_at timestamptz,
  session_count integer,
  attendance_seconds integer,
  total_points integer,
  last_checked_out_at timestamptz
)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
  if not public.is_team_admin() then
    raise exception using errcode = '42501', message = 'admin_required';
  end if;

  return query
  select
    m.id,
    m.display_name,
    m.avatar_key,
    m.sort_order,
    m.is_active,
    m.is_admin,
    m.created_at,
    (
      select pg_catalog.count(*)::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
    ),
    (
      select coalesce(
        pg_catalog.sum(
          pg_catalog.date_part(
            'epoch',
            coalesce(s.checked_out_at, pg_catalog.clock_timestamp()) - s.checked_in_at
          )
        ),
        0
      )::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
    ),
    public.member_total_points(m.id),
    (
      select pg_catalog.max(s.checked_out_at)
      from public.attendance_sessions as s
      where s.member_id = m.id
    )
  from public.members as m
  where m.team_id = public.current_member_team_id()
  order by m.sort_order, m.id;
end;
$$;

commit;
