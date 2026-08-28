begin;

-- Attendance in the "I showed up today" sense, which is a different thing from
-- the attendance sessions: it is a once-a-day button, not a record of work.
--
-- The primary key is the rule. Once a day per member cannot be enforced from the
-- client, because closing and reopening the app would hand out points forever, so
-- (member_id, local_date) being unique is what makes a second claim impossible
-- rather than merely discouraged.
--
-- Points are stored per row rather than as a running total on the member, so
-- changing what a day is worth later never rewrites what earlier days were worth.
-- There is nothing to spend them on yet; this is only the earning half.
create table public.member_check_ins (
  member_id uuid not null references public.members (id) on delete cascade,
  local_date date not null,
  points integer not null default 10,
  claimed_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint member_check_ins_pkey primary key (member_id, local_date),
  constraint member_check_ins_points_check check (points between 0 and 1000)
);

alter table public.member_check_ins enable row level security;

-- Readable across the team for the same reason the ranking is: comparing is the
-- point. Nothing may be written directly; claim_daily_check_in is the only way
-- in, and it is the thing that decides what a day is worth.
create policy member_check_ins_select_same_team
on public.member_check_ins
for select
to authenticated
using (
  (select auth.uid()) is not null
  and public.can_access_member(member_id)
);

-- The day is a team-local day, matching the statistics. A UTC day would roll over
-- at nine in the morning in Seoul, so the button would refuse a claim in the
-- middle of a working day and then allow two before lunch.
create or replace function public.claim_daily_check_in()
returns table (
  claimed boolean,
  awarded integer,
  total_points integer,
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
    v_today;
end;
$$;

comment on function public.claim_daily_check_in() is
  'Claims today''s check-in for the caller and returns whether it counted, what it was worth, and the running total. Idempotent within a team-local day.';

-- Lets the overlay open with the button already in the right state instead of
-- finding out by pressing it.
create or replace function public.daily_check_in_status()
returns table (
  claimed boolean,
  total_points integer,
  local_date date
)
language plpgsql
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
    v_today;
end;
$$;

comment on function public.daily_check_in_status() is
  'Whether the caller has claimed today, and their running point total, in the team timezone.';

revoke all on function public.claim_daily_check_in() from public, anon, authenticated;
grant execute on function public.claim_daily_check_in() to authenticated;
revoke all on function public.daily_check_in_status() from public, anon, authenticated;
grant execute on function public.daily_check_in_status() to authenticated;

commit;
