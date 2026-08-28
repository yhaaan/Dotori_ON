begin;

-- A day here starts at 06:00, not midnight.
--
-- The team works past midnight often enough that a calendar boundary in the
-- middle of the evening was splitting one sitting across two days: a session
-- from 22:00 to 02:00 became two half days, and a check-in at 01:00 counted as
-- the day the person had not started yet. Six in the morning is the boundary
-- games have used for the same reason for decades - nobody is awake to be
-- surprised by it.
--
-- Both directions live here so the rule has exactly one definition. Everything
-- that turns an instant into a day, or a day into a span, goes through these.
create or replace function public.team_day_offset()
returns interval
language sql
immutable
parallel safe
set search_path = ''
as $$
  select interval '6 hours';
$$;

create or replace function public.team_local_date(p_at timestamptz)
returns date
language sql
stable
security definer
set search_path = ''
as $$
  select ((p_at at time zone public.team_timezone()) - public.team_day_offset())::date;
$$;

-- The instant a business date begins. The end of that date is the start of the
-- next one, so callers ask for p_date + 1 rather than adding a day themselves.
create or replace function public.team_day_start(p_date date)
returns timestamptz
language sql
stable
security definer
set search_path = ''
as $$
  select ((p_date)::timestamp + public.team_day_offset()) at time zone public.team_timezone();
$$;

comment on function public.team_local_date(timestamptz) is
  'The team-local business date an instant falls in. Days run 06:00 to 06:00.';
comment on function public.team_day_start(date) is
  'The instant a business date begins, 06:00 team-local.';

-- Check-in ---------------------------------------------------------------

create or replace function public.daily_check_in_streak(p_member_id uuid)
returns integer
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_today date := public.team_local_date(pg_catalog.clock_timestamp());
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
    (
      select coalesce(pg_catalog.sum(c.points), 0)::integer
      from public.member_check_ins as c
      where c.member_id = v_member_id
    ),
    public.daily_check_in_streak(v_member_id),
    v_today;
end;
$$;

-- Statistics -------------------------------------------------------------
--
-- The same rule, or the calendar and the check-in would disagree about which
-- day a late night belonged to. Nothing stored changes: these read the same
-- session timestamps and only re-bucket them, so the boundary can be moved
-- again by replacing team_day_offset.

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

revoke all on function public.team_day_offset() from public, anon, authenticated;
revoke all on function public.team_local_date(timestamptz) from public, anon, authenticated;
revoke all on function public.team_day_start(date) from public, anon, authenticated;
grant execute on function public.team_day_offset() to authenticated;
grant execute on function public.team_local_date(timestamptz) to authenticated;
grant execute on function public.team_day_start(date) to authenticated;

commit;
