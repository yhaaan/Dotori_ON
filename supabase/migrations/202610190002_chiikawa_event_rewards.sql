begin;

-- Chiikawa event milestone rewards: clearing R10, R20 ... R100 pays 10, 20 ... 100
-- DOTORION points, once each, through the member_point_awards ledger.
--
-- Event progress lives on each client, so the server cannot prove a clear. What it
-- does own is the part that matters for points: the deadline, the fixed amount per
-- milestone, and that a milestone pays only once per member. The rewards stay in
-- the ledger after the event ends; only claiming stops.

create or replace function public.chiikawa_event_ends_at()
returns timestamptz
language sql
immutable
set search_path = ''
as $$
  -- The end of 30 November in Seoul. SubjugationEvent.EndsAtUtc mirrors this.
  select timestamptz '2026-12-01 00:00:00+09';
$$;

alter table public.chiikawa_event_rounds
  add column if not exists cleared integer not null default 0;
alter table public.chiikawa_event_rounds
  drop constraint if exists chiikawa_event_rounds_cleared_check;
alter table public.chiikawa_event_rounds
  add constraint chiikawa_event_rounds_cleared_check check (cleared between 0 and 100);

create or replace function public.claim_chiikawa_event_reward(p_milestone integer, p_cleared integer)
returns table (
  claimed boolean,
  awarded integer,
  total_points integer
)
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member public.members;
  v_awarded integer;
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  if pg_catalog.clock_timestamp() >= public.chiikawa_event_ends_at() then
    raise exception using errcode = '22023', message = 'event_ended';
  end if;

  if p_milestone is null or p_milestone < 10 or p_milestone > 100 or p_milestone % 10 <> 0 then
    raise exception using errcode = '22023', message = 'invalid_milestone';
  end if;

  if p_cleared is null or p_cleared < p_milestone or p_cleared > 100 then
    raise exception using errcode = '22023', message = 'milestone_not_cleared';
  end if;

  select m.* into v_member
  from public.members as m
  where m.id = (select auth.uid()) and m.is_active;

  if not found then
    raise exception using errcode = '42501', message = 'active_member_required';
  end if;

  insert into public.chiikawa_event_rounds as r (member_id, team_id, round, max_round, cleared)
  values (v_member.id, v_member.team_id, p_cleared, p_cleared, p_cleared)
  on conflict (member_id) do update
  set cleared = greatest(r.cleared, excluded.cleared),
      updated_at = pg_catalog.clock_timestamp();

  -- A second press conflicts and inserts nothing; that is an answer, not an error.
  insert into public.member_point_awards as a (member_id, source, award_key, points)
  values (v_member.id, 'chiikawa-2026', 'R' || p_milestone, p_milestone)
  on conflict (member_id, source, award_key) do nothing
  returning a.points into v_awarded;

  return query
  select true, coalesce(v_awarded, 0), public.member_total_points(v_member.id);
end;
$$;

create or replace function public.chiikawa_event_claimed_rewards()
returns table (milestone integer)
language sql
stable
security definer
set search_path = ''
as $$
  select pg_catalog.substr(a.award_key, 2)::integer
  from public.member_point_awards as a
  where a.member_id = (select auth.uid()) and a.source = 'chiikawa-2026'
  order by 1;
$$;

-- Developer dashboard reset. An admin clears the whole team's event rounds and event points;
-- anyone else clears only their own. Check-in points and every other table are untouched.
-- Progress kept in other members' local saves comes back on their next sync.
create or replace function public.reset_chiikawa_event()
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member public.members;
  v_rounds integer;
  v_awards integer;
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  select m.* into v_member
  from public.members as m
  where m.id = (select auth.uid()) and m.is_active;

  if not found then
    raise exception using errcode = '42501', message = 'active_member_required';
  end if;

  if v_member.is_admin then
    delete from public.chiikawa_event_rounds as r where r.team_id = v_member.team_id;
    get diagnostics v_rounds = row_count;
    delete from public.member_point_awards as a
    using public.members as m
    where a.member_id = m.id and m.team_id = v_member.team_id and a.source = 'chiikawa-2026';
    get diagnostics v_awards = row_count;
  else
    delete from public.chiikawa_event_rounds as r where r.member_id = v_member.id;
    get diagnostics v_rounds = row_count;
    delete from public.member_point_awards as a
    where a.member_id = v_member.id and a.source = 'chiikawa-2026';
    get diagnostics v_awards = row_count;
  end if;

  return v_rounds + v_awards;
end;
$$;

revoke all on function public.reset_chiikawa_event() from public, anon;
grant execute on function public.reset_chiikawa_event() to authenticated;

revoke all on function public.chiikawa_event_ends_at() from public, anon;
revoke all on function public.claim_chiikawa_event_reward(integer, integer) from public, anon;
revoke all on function public.chiikawa_event_claimed_rewards() from public, anon;
grant execute on function public.chiikawa_event_ends_at() to authenticated;
grant execute on function public.claim_chiikawa_event_reward(integer, integer) to authenticated;
grant execute on function public.chiikawa_event_claimed_rewards() to authenticated;

commit;
