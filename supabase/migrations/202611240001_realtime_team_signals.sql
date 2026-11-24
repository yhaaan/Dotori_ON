-- Polling an unchanged roster exhausted the free egress allowance. Broadcast
-- only invalidations; clients retain their existing RLS-protected snapshot read.
-- Heartbeat timestamps are deliberately excluded: forwarding them would replace
-- REST polling traffic with equally unnecessary Realtime traffic.
begin;

create or replace function public.send_team_signal(
  p_team_id uuid,
  p_kind text,
  p_member_id uuid
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
  -- A cascaded state deletion may no longer find its parent. The members DELETE
  -- trigger still has OLD.team_id and sends the roster invalidation instead.
  if p_team_id is null then
    return;
  end if;

  begin
    perform realtime.send(
      pg_catalog.jsonb_build_object('kind', p_kind, 'member_id', p_member_id),
      'team_signal',
      'team:' || p_team_id::text,
      true
    );
  exception when others then
    -- Attendance must remain writable even when Realtime is unavailable. Avoid
    -- logging payloads or server error text; SQLSTATE is enough for diagnosis.
    raise warning 'Team signal delivery failed (SQLSTATE %)', sqlstate;
  end;
end;
$$;

revoke all on function public.send_team_signal(uuid, text, uuid)
  from public, anon, authenticated;

create or replace function public.signal_team_state_change()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member_id uuid;
  v_team_id uuid;
begin
  if tg_op = 'UPDATE' then
    if not (
      old.attendance_session_id is distinct from new.attendance_session_id
      or old.attendance_status is distinct from new.attendance_status
      or old.activity_status is distinct from new.activity_status
      or old.connection_status is distinct from new.connection_status
      or old.checked_in_at is distinct from new.checked_in_at
      or old.status_started_at is distinct from new.status_started_at
      or old.last_checked_out_at is distinct from new.last_checked_out_at
      or old.status_note is distinct from new.status_note
    ) then
      return null;
    end if;
  end if;

  if tg_op = 'DELETE' then
    v_member_id := old.member_id;
  else
    v_member_id := new.member_id;
  end if;

  select m.team_id into v_team_id
  from public.members as m
  where m.id = v_member_id;

  perform public.send_team_signal(v_team_id, 'state', v_member_id);
  return null;
end;
$$;

revoke all on function public.signal_team_state_change()
  from public, anon, authenticated;

create trigger member_current_state_team_signal
  after insert or update or delete on public.member_current_state
  for each row execute function public.signal_team_state_change();

create or replace function public.signal_team_member_change()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
  if tg_op = 'UPDATE' then
    if not (
      old.display_name is distinct from new.display_name
      or old.avatar_key is distinct from new.avatar_key
      or old.sort_order is distinct from new.sort_order
      or old.is_active is distinct from new.is_active
    ) then
      return null;
    end if;
  end if;

  if tg_op = 'DELETE' then
    perform public.send_team_signal(old.team_id, 'member', old.id);
  else
    perform public.send_team_signal(new.team_id, 'member', new.id);
  end if;
  return null;
end;
$$;

revoke all on function public.signal_team_member_change()
  from public, anon, authenticated;

create trigger members_team_signal
  after insert or delete or update of display_name, avatar_key, sort_order, is_active
  on public.members
  for each row execute function public.signal_team_member_change();

create or replace function public.signal_team_nudge()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
  perform public.send_team_signal(new.team_id, 'nudge', new.actor_member_id);
  return null;
end;
$$;

revoke all on function public.signal_team_nudge()
  from public, anon, authenticated;

create trigger team_events_nudge_signal
  after insert on public.team_events
  for each row when (new.event_type = 'nudge')
  execute function public.signal_team_nudge();

-- Clients subscribe to their own private topic; they never publish signals.
create policy team_signal_read on realtime.messages
  for select to authenticated
  using (
    realtime.messages.extension = 'broadcast'
    and (select realtime.topic()) = 'team:' || public.current_member_team_id()::text
  );

commit;
