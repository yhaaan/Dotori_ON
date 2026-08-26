begin;

-- Statistics read from the raw interval tables rather than a rollup table. At
-- four members the volume is trivial, and computing on demand means a session
-- that is still open is counted up to "now" instead of appearing only after
-- checkout.
--
-- Two rules from the handoff doc are what make this non-trivial:
--   * days are Asia/Seoul days, while every stored timestamp is UTC, so an
--     interval crossing local midnight must be split across two days;
--   * attendance time and work time are different numbers, because break and
--     meal are inside the attendance session but outside work.

create or replace function public.team_timezone()
returns text
language sql
stable
security definer
set search_path = ''
as $$
  select coalesce(
    (
      select t.timezone
      from public.teams as t
      join public.members as m on m.team_id = t.id
      where m.id = (select auth.uid())
    ),
    'Asia/Seoul'
  );
$$;

-- Per-day totals for one member. Every teammate may read every teammate's
-- numbers: the whole point of the screen is comparing them.
create or replace function public.member_daily_stats(
  p_member_id uuid,
  p_from date,
  p_to date
)
returns table (
  local_date date,
  attendance_seconds integer,
  work_seconds integer,
  break_seconds integer,
  meal_seconds integer
)
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_tz text := public.team_timezone();
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  if not public.can_access_member(p_member_id) then
    raise exception using errcode = '42501', message = 'member_not_visible';
  end if;

  if p_from is null or p_to is null or p_to < p_from then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  -- Bounded so a single call cannot ask the server to expand years of days.
  if p_to - p_from > 366 then
    raise exception using errcode = '22023', message = 'date_range_too_wide';
  end if;

  return query
  with days as (
    select
      d::date as day,
      (d::date)::timestamp at time zone v_tz as day_start,
      ((d::date) + 1)::timestamp at time zone v_tz as day_end
    from pg_catalog.generate_series(p_from, p_to, interval '1 day') as d
  ),
  attendance as (
    select s.checked_in_at as starts, coalesce(s.checked_out_at, v_now) as ends
    from public.attendance_sessions as s
    where s.member_id = p_member_id
  ),
  activity as (
    select a.status, a.started_at as starts, coalesce(a.ended_at, v_now) as ends
    from public.activity_intervals as a
    where a.member_id = p_member_id
  )
  select
    d.day,
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, d.day_end) - greatest(x.starts, d.day_start)))), 0)::integer
      from attendance as x
      where x.starts < d.day_end and x.ends > d.day_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, d.day_end) - greatest(x.starts, d.day_start)))), 0)::integer
      from activity as x
      where x.status = 'working' and x.starts < d.day_end and x.ends > d.day_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, d.day_end) - greatest(x.starts, d.day_start)))), 0)::integer
      from activity as x
      where x.status = 'break' and x.starts < d.day_end and x.ends > d.day_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, d.day_end) - greatest(x.starts, d.day_start)))), 0)::integer
      from activity as x
      where x.status = 'meal' and x.starts < d.day_end and x.ends > d.day_start
    )
  from days as d
  order by d.day;
end;
$$;

-- Work-time ranking across the caller's team for a local date range.
create or replace function public.team_work_ranking(
  p_from date,
  p_to date
)
returns table (
  member_id uuid,
  display_name text,
  sort_order smallint,
  work_seconds integer,
  attendance_seconds integer
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
  v_from timestamptz;
  v_to timestamptz;
begin
  if v_team_id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  if p_from is null or p_to is null or p_to < p_from then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  if p_to - p_from > 366 then
    raise exception using errcode = '22023', message = 'date_range_too_wide';
  end if;

  v_from := (p_from)::timestamp at time zone v_tz;
  v_to := ((p_to) + 1)::timestamp at time zone v_tz;

  return query
  select
    m.id,
    m.display_name,
    m.sort_order,
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(a.ended_at, v_now), v_to) - greatest(a.started_at, v_from)))), 0)::integer
      from public.activity_intervals as a
      where a.member_id = m.id
        and a.status = 'working'
        and a.started_at < v_to
        and coalesce(a.ended_at, v_now) > v_from
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(coalesce(s.checked_out_at, v_now), v_to) - greatest(s.checked_in_at, v_from)))), 0)::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
        and s.checked_in_at < v_to
        and coalesce(s.checked_out_at, v_now) > v_from
    )
  from public.members as m
  where m.team_id = v_team_id
    and m.is_active
  order by 4 desc, m.sort_order;
end;
$$;

comment on function public.member_daily_stats(uuid, date, date) is
  'Per-day attendance/work/break/meal seconds in the team timezone. Open intervals count up to now.';
comment on function public.team_work_ranking(date, date) is
  'Work-time ranking for the caller''s team over a local date range, highest first.';

revoke all on function public.team_timezone() from public, anon, authenticated;
revoke all on function public.member_daily_stats(uuid, date, date) from public, anon, authenticated;
revoke all on function public.team_work_ranking(date, date) from public, anon, authenticated;

grant execute on function public.team_timezone() to authenticated;
grant execute on function public.member_daily_stats(uuid, date, date) to authenticated;
grant execute on function public.team_work_ranking(date, date) to authenticated;

commit;
