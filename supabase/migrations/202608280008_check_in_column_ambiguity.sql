begin;

-- claim_daily_check_in failed at runtime with "column reference local_date is
-- ambiguous". Its OUT parameter was named local_date, which shadows the column
-- of the same name everywhere plpgsql substitutes variables - including the
-- ON CONFLICT inference list, where the insert names the very column it is
-- conflicting on.
--
-- Renaming the OUT parameter fixes the class rather than the one statement:
-- qualifying that single clause would leave the next unqualified reference to
-- local_date waiting to do the same thing. The name only ever mattered inside
-- the function - the client reads claimed, awarded, total_points and
-- streak_days, and never this - so nothing outside changes.
--
-- plpgsql bodies are not parsed until they run, so nothing catches this at
-- deploy time. It went unnoticed because the button that calls it was hidden
-- behind another one; the first press after that was fixed found it.
drop function if exists public.claim_daily_check_in();
create function public.claim_daily_check_in()
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

comment on function public.claim_daily_check_in() is
  'Claims today''s check-in for the caller and returns whether it counted, what it was worth, the running total and the streak. Idempotent within a team-local day.';

-- The status function has the same shadowing, and would have failed the same way
-- the moment anything in it referenced the column without qualifying it.
drop function if exists public.daily_check_in_status();
create function public.daily_check_in_status()
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

comment on function public.daily_check_in_status() is
  'Whether the caller has claimed today, their running point total and their streak, in the team timezone.';

revoke all on function public.claim_daily_check_in() from public, anon, authenticated;
grant execute on function public.claim_daily_check_in() to authenticated;
revoke all on function public.daily_check_in_status() from public, anon, authenticated;
grant execute on function public.daily_check_in_status() to authenticated;

commit;
