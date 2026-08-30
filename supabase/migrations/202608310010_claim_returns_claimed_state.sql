begin;

-- claim_daily_check_in and daily_check_in_status both return a column called
-- claimed, and they meant different things by it.
--
--   daily_check_in_status  claimed = today is already taken
--   claim_daily_check_in   claimed = this call is what took it
--
-- The second one is false whenever the day was already claimed, because
-- "on conflict do nothing" returns no row and v_awarded stays null. A client
-- reading the two responses through one field - which is the obvious thing to
-- do, they have the same shape - was told nobody had checked in yet on every
-- press after the first, and drew the button as if today were still there to
-- take.
--
-- The name wins: claimed answers "is today taken", in both functions. Once this
-- call returns without raising, the row exists whether it was inserted now or
-- was already there, so the answer is simply true. Nothing is lost, because
-- awarded already tells the two cases apart - it is the points this particular
-- call earned, and it is zero when the day was already claimed.
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
  'Claims today''s check-in for the caller. Returns claimed (always true once it returns: the day is taken either way), awarded (what this call earned, zero if the day was already claimed), the running total and the streak. Idempotent within a team-local day.';

grant execute on function public.claim_daily_check_in() to authenticated;

commit;
