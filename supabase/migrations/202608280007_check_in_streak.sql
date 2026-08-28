begin;

-- How many days in a row, ending today or yesterday.
--
-- Yesterday counts so the number does not read as broken every morning before
-- the button is pressed. A run that ended two days ago is over, and reporting it
-- as still standing would be the one thing a streak must not do.
--
-- The run is found by the standard trick for consecutive dates: over rows
-- ordered newest first, local_date + row_number is constant inside a run and
-- jumps at every gap. The newest row's value identifies the run that reaches the
-- end, so counting rows that share it is the length.
create or replace function public.daily_check_in_streak(p_member_id uuid)
returns integer
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_today date := (pg_catalog.clock_timestamp() at time zone public.team_timezone())::date;
  v_last date;
  v_streak integer;
begin
  select pg_catalog.max(c.local_date)
  into v_last
  from public.member_check_ins as c
  where c.member_id = p_member_id and c.local_date <= v_today;

  if v_last is null or v_today - v_last > 1 then
    return 0;
  end if;

  select pg_catalog.count(*)::integer
  into v_streak
  from (
    select c.local_date + (row_number() over (order by c.local_date desc))::integer as run_key
    from public.member_check_ins as c
    where c.member_id = p_member_id and c.local_date <= v_last
  ) as runs
  where runs.run_key = v_last + 1;

  return coalesce(v_streak, 0);
end;
$$;

comment on function public.daily_check_in_streak(uuid) is
  'Consecutive check-in days ending today or yesterday, in the team timezone. Zero once the run is broken.';

-- Both check-in functions gain the streak, so pressing the button can say how
-- long the run is without a second round trip.
--
-- Dropped rather than replaced: a set-returning function cannot change the shape
-- of what it returns in place.
drop function if exists public.claim_daily_check_in();
create function public.claim_daily_check_in()
returns table (
  claimed boolean,
  awarded integer,
  total_points integer,
  streak_days integer,
  local_date date
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

  v_today := (pg_catalog.clock_timestamp() at time zone public.team_timezone())::date;

  -- A second press on the same day conflicts and inserts nothing, which leaves
  -- v_awarded null. That is the answer, not an error: pressing twice is an
  -- ordinary thing to do and the caller only wants to know where it stands.
  insert into public.member_check_ins as c (member_id, local_date)
  values (v_member_id, v_today)
  on conflict (member_id, local_date) do nothing
  returning c.points into v_awarded;

  return query
  select
    v_awarded is not null,
    coalesce(v_awarded, 0),
    (
      select coalesce(pg_catalog.sum(c.points), 0)::integer
      from public.member_check_ins as c
      where c.member_id = v_member_id
    ),
    public.daily_check_in_streak(v_member_id),
    v_today;
end;
$$;

drop function if exists public.daily_check_in_status();
create function public.daily_check_in_status()
returns table (
  claimed boolean,
  total_points integer,
  streak_days integer,
  local_date date
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

  v_today := (pg_catalog.clock_timestamp() at time zone public.team_timezone())::date;

  return query
  select
    exists (
      select 1
      from public.member_check_ins as c
      where c.member_id = v_member_id and c.local_date = v_today
    ),
    (
      select coalesce(pg_catalog.sum(c.points), 0)::integer
      from public.member_check_ins as c
      where c.member_id = v_member_id
    ),
    public.daily_check_in_streak(v_member_id),
    v_today;
end;
$$;

-- The ranking screen is where teammates are compared, so it is where a streak
-- means something. Points come with it: both belong to the person rather than
-- to the date range being ranked, and both are read off the same row.
drop function if exists public.team_activity_ranking(date, date);
create function public.team_activity_ranking(
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
  v_tz text := public.team_timezone();
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
      select pg_catalog.min((s.checked_in_at at time zone v_tz)::date)
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

  v_start := (v_from)::timestamp at time zone v_tz;
  v_end := ((p_to) + 1)::timestamp at time zone v_tz;

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
    (
      select coalesce(pg_catalog.sum(c.points), 0)::integer
      from public.member_check_ins as c
      where c.member_id = m.id
    ),
    public.daily_check_in_streak(m.id)
  from public.members as m
  where m.team_id = v_team_id
    and m.is_active
  order by 5 desc, m.sort_order;
end;
$$;

comment on function public.team_activity_ranking(date, date) is
  'Per-member attendance/work/break/meal seconds for the caller''s team over a local date range, work time first, with each member''s point total and check-in streak. A null p_from starts at the team''s first session.';

revoke all on function public.daily_check_in_streak(uuid) from public, anon, authenticated;
grant execute on function public.daily_check_in_streak(uuid) to authenticated;
revoke all on function public.claim_daily_check_in() from public, anon, authenticated;
grant execute on function public.claim_daily_check_in() to authenticated;
revoke all on function public.daily_check_in_status() from public, anon, authenticated;
grant execute on function public.daily_check_in_status() to authenticated;
revoke all on function public.team_activity_ranking(date, date) from public, anon, authenticated;
grant execute on function public.team_activity_ranking(date, date) to authenticated;

commit;
