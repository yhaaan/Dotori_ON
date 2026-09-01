-- Carry daily reward check-ins with period statistics so the month calendar can
-- mark a claimed day independently from attendance-duration shading.

drop function if exists public.member_period_stats(uuid, date, text, date);

create function public.member_period_stats(
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
  meal_seconds integer,
  daily_check_in_days integer
)
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
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
      select public.team_local_date(pg_catalog.min(s.checked_in_at))
      from public.attendance_sessions as s
      where s.member_id = p_member_id
    ),
    p_to);

  if p_to < v_from then
    raise exception using errcode = '22023', message = 'invalid_date_range';
  end if;

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
      public.team_day_start(b.starts) as span_start,
      public.team_day_start(b.ends + 1) as span_end
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
    ),
    (
      select pg_catalog.count(*)::integer
      from public.member_check_ins as c
      where c.member_id = p_member_id
        and c.local_date between s.starts and s.ends
    )
  from spans as s
  order by s.starts;
end;
$$;

comment on function public.member_period_stats(uuid, date, text, date) is
  'Attendance/activity totals plus daily reward check-in count for each local-date bucket.';

revoke all on function public.member_period_stats(uuid, date, text, date) from public, anon, authenticated;
grant execute on function public.member_period_stats(uuid, date, text, date) to authenticated;
