begin;

-- The Chiikawa event now runs to R200 (it stopped at R100). Only the round limits change:
-- milestone rewards stay R10 ... R100 (550P in total), and weapons live in the local save only.

alter table public.chiikawa_event_rounds
  drop constraint if exists chiikawa_event_rounds_round_check;
alter table public.chiikawa_event_rounds
  add constraint chiikawa_event_rounds_round_check check (round between 1 and 200);
alter table public.chiikawa_event_rounds
  drop constraint if exists chiikawa_event_rounds_max_round_check;
alter table public.chiikawa_event_rounds
  add constraint chiikawa_event_rounds_max_round_check check (max_round between 1 and 200);
alter table public.chiikawa_event_rounds
  drop constraint if exists chiikawa_event_rounds_cleared_check;
alter table public.chiikawa_event_rounds
  add constraint chiikawa_event_rounds_cleared_check check (cleared between 0 and 200);

create or replace function public.set_chiikawa_event_round(p_round integer, p_max_round integer)
returns void language plpgsql security definer set search_path = '' as $$
declare v_member public.members;
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;
  if p_round is null or p_round < 1 or p_round > 200
    or p_max_round is null or p_max_round < 1 or p_max_round > 200 then
    raise exception using errcode = '22023', message = 'invalid_round';
  end if;
  select m.* into v_member from public.members m
    where m.id = (select auth.uid()) and m.is_active;
  if not found then
    raise exception using errcode = '42501', message = 'active_member_required';
  end if;
  insert into public.chiikawa_event_rounds as r (member_id, team_id, round, max_round)
    values (v_member.id, v_member.team_id, p_round, greatest(p_round, p_max_round))
    on conflict (member_id) do update
    set round = excluded.round,
        max_round = greatest(r.max_round, excluded.max_round),
        updated_at = pg_catalog.clock_timestamp();
end;
$$;

-- Same body as 202610190002, except a clear may now be reported up to R200.
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

  if p_cleared is null or p_cleared < p_milestone or p_cleared > 200 then
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

revoke all on function public.set_chiikawa_event_round(integer, integer) from public, anon;
grant execute on function public.set_chiikawa_event_round(integer, integer) to authenticated;
revoke all on function public.claim_chiikawa_event_reward(integer, integer) from public, anon;
grant execute on function public.claim_chiikawa_event_reward(integer, integer) to authenticated;

commit;
