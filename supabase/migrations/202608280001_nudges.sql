begin;

-- Nudges are the first thing to be written into team_events as an event rather
-- than derived from a roster diff: a nudge carries no state, so there is nothing
-- for two snapshots to differ in.
--
-- Rate limiting is a burst allowance, not a fixed delay. Poking someone three
-- times in a row is the point of the feature; poking them fifty times is not.
-- Five nudges to the same target are free, and the sixth within ten seconds of
-- the first is refused until the oldest one ages out of the window.

create index if not exists team_events_actor_created_idx
  on public.team_events (actor_member_id, created_at desc);

create or replace function public.send_nudge(p_target_member_id uuid default null)
returns uuid
language plpgsql
volatile
security definer
set search_path = ''
as $$
declare
  v_actor uuid := (select auth.uid());
  v_team_id uuid := public.current_member_team_id();
  v_open_session uuid;
  v_recent integer;
  v_event_id uuid;
begin
  if v_team_id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  -- Sending is an act of being at work, like every other mutation here.
  select s.id
  into v_open_session
  from public.attendance_sessions as s
  where s.member_id = v_actor
    and s.checked_out_at is null
  order by s.checked_in_at desc
  limit 1;

  if v_open_session is null then
    raise exception using errcode = '42501', message = 'member_not_clocked_in';
  end if;

  if p_target_member_id is not null then
    if p_target_member_id = v_actor then
      raise exception using errcode = '22023', message = 'nudge_target_is_self';
    end if;

    if not exists (
      select 1
      from public.members as m
      where m.id = p_target_member_id
        and m.team_id = v_team_id
        and m.is_active
    ) then
      raise exception using errcode = '42501', message = 'member_not_visible';
    end if;
  end if;

  -- A null target is the whole team, and "is not distinct from" makes that its
  -- own bucket instead of colliding with every individual target.
  select pg_catalog.count(*)
  into v_recent
  from public.team_events as e
  where e.actor_member_id = v_actor
    and e.event_type = 'nudge'
    and e.target_member_id is not distinct from p_target_member_id
    and e.created_at > pg_catalog.clock_timestamp() - interval '10 seconds';

  if v_recent >= 5 then
    raise exception using errcode = '22023', message = 'nudge_too_soon';
  end if;

  insert into public.team_events (
    team_id,
    actor_member_id,
    target_member_id,
    event_type,
    payload)
  values (
    v_team_id,
    v_actor,
    p_target_member_id,
    'nudge',
    '{}'::jsonb)
  returning id into v_event_id;

  return v_event_id;
end;
$$;

comment on function public.send_nudge(uuid) is
  'Records a nudge in team_events for one teammate, or for the whole team when the target is null. Requires an open attendance session and allows five per target per ten seconds.';

revoke all on function public.send_nudge(uuid) from public, anon, authenticated;
grant execute on function public.send_nudge(uuid) to authenticated;

commit;
