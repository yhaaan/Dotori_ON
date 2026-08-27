begin;

-- Milestone 5 widens the statistics panel from "last seven days, work time" to
-- any period and any activity metric.
--
-- Two things changed on the server:
--   * a day is no longer the only bucket. A month of daily rows does not fit the
--     overlay, so the server folds days into weeks or months instead of making
--     the client stitch them back together and get the Asia/Seoul day boundaries
--     wrong a second time.
--   * a null start date means "everything on record". The client cannot know when
--     the team started, and hardcoding an epoch would silently drop history.
--
-- The ranking now also carries break and meal seconds, so switching the ranked
-- metric is a local re-sort of four members instead of another round trip.

drop function if exists public.member_daily_stats(uuid, date, date);
drop function if exists public.team_work_ranking(date, date);

create or replace function public.member_period_stats(
  p_member_id uuid,
  p_to date,
  p_bucket text,
  p_from date default null
)
returns table (
  bucket_start date,
  bucket_end date,
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
  v_step interval;
  v_from date;
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  if not public.can_access_member(p_member_id) then
    raise exception using errcode = '42501', message = 'member_not_visible';
  end if;

  if p_to is null then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  v_step := case p_bucket
    when 'day' then interval '1 day'
    when 'week' then interval '1 week'
    when 'month' then interval '1 month'
    else null
  end;

  if v_step is null then
    raise exception using errcode = '22023', message = 'invalid_bucket';
  end if;

  v_from := coalesce(
    p_from,
    (
      select ((pg_catalog.min(s.checked_in_at)) at time zone v_tz)::date
      from public.attendance_sessions as s
      where s.member_id = p_member_id
    ),
    p_to);

  if p_to < v_from then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  -- Daily rows are one row per day, so they keep the tighter bound; the coarser
  -- buckets only need a ceiling a real team cannot reach.
  if (p_bucket = 'day' and p_to - v_from > 366) or p_to - v_from > 3660 then
    raise exception using errcode = '22023', message = 'date_range_too_wide';
  end if;

  return query
  with buckets as (
    select
      greatest(g::date, v_from) as starts,
      least((g + v_step)::date - 1, p_to) as ends
    from pg_catalog.generate_series(
      case p_bucket
        when 'week' then pg_catalog.date_trunc('week', v_from::timestamp)
        when 'month' then pg_catalog.date_trunc('month', v_from::timestamp)
        else v_from::timestamp
      end,
      p_to::timestamp,
      v_step) as g
  ),
  spans as (
    select
      b.starts,
      b.ends,
      (b.starts)::timestamp at time zone v_tz as span_start,
      (b.ends + 1)::timestamp at time zone v_tz as span_end
    from buckets as b
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
    s.starts,
    s.ends,
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, s.span_end) - greatest(x.starts, s.span_start)))), 0)::integer
      from attendance as x
      where x.starts < s.span_end and x.ends > s.span_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, s.span_end) - greatest(x.starts, s.span_start)))), 0)::integer
      from activity as x
      where x.status = 'working' and x.starts < s.span_end and x.ends > s.span_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, s.span_end) - greatest(x.starts, s.span_start)))), 0)::integer
      from activity as x
      where x.status = 'break' and x.starts < s.span_end and x.ends > s.span_start
    ),
    (
      select coalesce(pg_catalog.sum(
        pg_catalog.date_part('epoch',
          (least(x.ends, s.span_end) - greatest(x.starts, s.span_start)))), 0)::integer
      from activity as x
      where x.status = 'meal' and x.starts < s.span_end and x.ends > s.span_start
    )
  from spans as s
  order by s.starts;
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
  v_team_id uuid := public.current_member_team_id();
  v_from date;
  v_start timestamptz;
  v_end timestamptz;
begin
  if v_team_id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  if p_to is null then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  v_from := coalesce(
    p_from,
    (
      select ((pg_catalog.min(s.checked_in_at)) at time zone v_tz)::date
      from public.attendance_sessions as s
      join public.members as m on m.id = s.member_id
      where m.team_id = v_team_id
    ),
    p_to);

  if p_to < v_from then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

  -- No per-day expansion happens here, so the ranking only needs a sanity bound.
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
    )
  from public.members as m
  where m.team_id = v_team_id
    and m.is_active
  order by 5 desc, m.sort_order;
end;
$$;

comment on function public.member_period_stats(uuid, date, text, date) is
  'Attendance/work/break/meal seconds for one member bucketed by day, week or month in the team timezone. A null p_from starts at the member''s first session; open intervals count up to now.';
comment on function public.team_activity_ranking(date, date) is
  'Per-member attendance/work/break/meal seconds for the caller''s team over a local date range, work time first. A null p_from starts at the team''s first session.';

revoke all on function public.member_period_stats(uuid, date, text, date) from public, anon, authenticated;
revoke all on function public.team_activity_ranking(date, date) from public, anon, authenticated;

grant execute on function public.member_period_stats(uuid, date, text, date) to authenticated;
grant execute on function public.team_activity_ranking(date, date) to authenticated;

commit;
