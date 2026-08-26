begin;

create or replace function public.current_member_team_id()
returns uuid
language sql
stable
security definer
set search_path = ''
as $$
  select m.team_id
  from public.members as m
  where m.id = (select auth.uid())
    and m.is_active
$$;

create or replace function public.can_access_member(p_member_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (
    select 1
    from public.members as target
    join public.members as viewer
      on viewer.team_id = target.team_id
    where target.id = p_member_id
      and viewer.id = (select auth.uid())
      and viewer.is_active
  )
$$;

create or replace function public.can_access_attendance_session(p_session_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (
    select 1
    from public.attendance_sessions as session_row
    join public.members as viewer
      on viewer.team_id = session_row.team_id
    where session_row.id = p_session_id
      and viewer.id = (select auth.uid())
      and viewer.is_active
  )
$$;

-- Anonymous Auth users receive the authenticated database role. Their Auth UUID
-- becomes the member primary key when they claim a visible name for the first time.
create or replace function public.claim_member_name(
  p_display_name text,
  p_team_id uuid default null,
  p_avatar_key text default 'default'
)
returns public.members
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_auth_id uuid := (select auth.uid());
  v_name text;
  v_normalized_name text;
  v_avatar_key text;
  v_team_id uuid;
  v_sort_order smallint;
  v_member public.members;
begin
  if v_auth_id is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  v_name := pg_catalog.regexp_replace(
    pg_catalog.btrim(p_display_name),
    '[[:space:]]+',
    ' ',
    'g'
  );
  v_normalized_name := public.normalize_member_name(v_name);
  v_avatar_key := coalesce(nullif(pg_catalog.btrim(p_avatar_key), ''), 'default');

  if v_normalized_name is null
     or pg_catalog.char_length(v_name) not between 1 and 16
     or v_name ~ '[[:cntrl:]]' then
    raise exception using errcode = '22023', message = 'invalid_member_name';
  end if;

  if pg_catalog.char_length(v_avatar_key) not between 1 and 64
     or v_avatar_key !~ '^[A-Za-z0-9._-]+$' then
    raise exception using errcode = '22023', message = 'invalid_avatar_key';
  end if;

  -- Serialize concurrent first-launch requests from the same Auth identity.
  perform pg_catalog.pg_advisory_xact_lock(
    pg_catalog.hashtextextended(v_auth_id::text, 0)
  );

  select m.*
  into v_member
  from public.members as m
  where m.id = v_auth_id;

  if v_member.id is not null then
    if not v_member.is_active then
      raise exception using errcode = '42501', message = 'member_inactive';
    end if;

    if v_member.normalized_name <> v_normalized_name then
      raise exception using errcode = '23505', message = 'member_name_already_claimed';
    end if;

    return v_member;
  end if;

  if p_team_id is null then
    if (select pg_catalog.count(*) from public.teams) <> 1 then
      raise exception using errcode = '22023', message = 'team_id_required';
    end if;

    select t.id
    into v_team_id
    from public.teams as t
    order by t.created_at, t.id
    limit 1;
  else
    v_team_id := p_team_id;
  end if;

  -- The team row lock serializes slot allocation. sort_order 0..3 plus its
  -- unique constraint is also the hard four-person capacity invariant.
  perform 1
  from public.teams as t
  where t.id = v_team_id
  for update;

  if not found then
    raise exception using errcode = '23503', message = 'team_not_found';
  end if;

  select slot.slot_number::smallint
  into v_sort_order
  from pg_catalog.generate_series(0, 3) as slot(slot_number)
  where not exists (
    select 1
    from public.members as existing_member
    where existing_member.team_id = v_team_id
      and existing_member.sort_order = slot.slot_number
  )
  order by slot.slot_number
  limit 1;

  if v_sort_order is null then
    raise exception using errcode = '23514', message = 'team_full';
  end if;

  begin
    insert into public.members (
      id,
      team_id,
      display_name,
      avatar_key,
      sort_order
    )
    values (
      v_auth_id,
      v_team_id,
      v_name,
      v_avatar_key,
      v_sort_order
    )
    returning * into v_member;
  exception
    when unique_violation then
      raise exception using errcode = '23505', message = 'member_name_taken';
  end;

  insert into public.member_current_state (member_id)
  values (v_auth_id);

  return v_member;
end;
$$;

create or replace function public.check_in(
  p_member_id uuid,
  p_client_instance_id uuid
)
returns public.member_current_state
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_member public.members;
  v_existing public.attendance_sessions;
  v_session_id uuid;
  v_state public.member_current_state;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  if p_client_instance_id is null then
    raise exception using errcode = '22023', message = 'client_instance_id_required';
  end if;

  select m.*
  into v_member
  from public.members as m
  where m.id = p_member_id
    and m.is_active
  for update;

  if v_member.id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  select s.*
  into v_existing
  from public.attendance_sessions as s
  where s.member_id = p_member_id
    and s.checked_out_at is null
  for update;

  if v_existing.id is not null then
    if v_existing.client_instance_id <> p_client_instance_id then
      raise exception using errcode = '23505', message = 'member_already_clocked_in';
    end if;

    -- Idempotent retry after a lost HTTP response from the same app instance.
    update public.attendance_sessions as s
    set last_heartbeat_at = v_now
    where s.id = v_existing.id;

    update public.member_current_state as state_row
    set last_heartbeat_at = v_now,
        connection_status = 'connected',
        updated_at = v_now
    where state_row.member_id = p_member_id
    returning state_row.* into v_state;

    if v_state.member_id is null then
      raise exception using errcode = 'P0001', message = 'member_state_missing';
    end if;

    return v_state;
  end if;

  insert into public.attendance_sessions (
    team_id,
    member_id,
    checked_in_at,
    last_heartbeat_at,
    client_instance_id
  )
  values (
    v_member.team_id,
    p_member_id,
    v_now,
    v_now,
    p_client_instance_id
  )
  returning id into v_session_id;

  insert into public.activity_intervals (
    attendance_session_id,
    member_id,
    status,
    started_at
  )
  values (
    v_session_id,
    p_member_id,
    'working',
    v_now
  );

  insert into public.member_current_state (
    member_id,
    attendance_session_id,
    attendance_status,
    activity_status,
    connection_status,
    checked_in_at,
    status_started_at,
    last_heartbeat_at,
    updated_at
  )
  values (
    p_member_id,
    v_session_id,
    'clocked_in',
    'working',
    'connected',
    v_now,
    v_now,
    v_now,
    v_now
  )
  on conflict (member_id) do update
  set attendance_session_id = excluded.attendance_session_id,
      attendance_status = excluded.attendance_status,
      activity_status = excluded.activity_status,
      connection_status = excluded.connection_status,
      checked_in_at = excluded.checked_in_at,
      status_started_at = excluded.status_started_at,
      last_heartbeat_at = excluded.last_heartbeat_at,
      updated_at = excluded.updated_at
  returning * into v_state;

  insert into public.team_events (
    team_id,
    actor_member_id,
    event_type,
    payload,
    created_at
  )
  values (
    v_member.team_id,
    p_member_id,
    'member_checked_in',
    pg_catalog.jsonb_build_object(
      'attendance_session_id', v_session_id,
      'activity_status', 'working'
    ),
    v_now
  );

  return v_state;
end;
$$;

create or replace function public.change_activity(
  p_member_id uuid,
  p_new_status public.activity_status
)
returns public.member_current_state
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_member public.members;
  v_session public.attendance_sessions;
  v_interval public.activity_intervals;
  v_state public.member_current_state;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  if p_new_status is null then
    raise exception using errcode = '22023', message = 'activity_status_required';
  end if;

  select m.*
  into v_member
  from public.members as m
  where m.id = p_member_id
    and m.is_active
  for update;

  if v_member.id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  select s.*
  into v_session
  from public.attendance_sessions as s
  where s.member_id = p_member_id
    and s.checked_out_at is null
  for update;

  if v_session.id is null then
    raise exception using errcode = '55000', message = 'member_not_clocked_in';
  end if;

  select interval_row.*
  into v_interval
  from public.activity_intervals as interval_row
  where interval_row.attendance_session_id = v_session.id
    and interval_row.ended_at is null
  for update;

  if v_interval.id is null then
    raise exception using errcode = 'P0001', message = 'open_activity_interval_missing';
  end if;

  update public.attendance_sessions as s
  set last_heartbeat_at = v_now
  where s.id = v_session.id;

  if v_interval.status = p_new_status then
    update public.member_current_state as state_row
    set last_heartbeat_at = v_now,
        connection_status = 'connected',
        updated_at = v_now
    where state_row.member_id = p_member_id
    returning state_row.* into v_state;

    return v_state;
  end if;

  update public.activity_intervals as interval_row
  set ended_at = v_now
  where interval_row.id = v_interval.id;

  insert into public.activity_intervals (
    attendance_session_id,
    member_id,
    status,
    started_at
  )
  values (
    v_session.id,
    p_member_id,
    p_new_status,
    v_now
  );

  update public.member_current_state as state_row
  set activity_status = p_new_status,
      connection_status = 'connected',
      status_started_at = v_now,
      last_heartbeat_at = v_now,
      updated_at = v_now
  where state_row.member_id = p_member_id
  returning state_row.* into v_state;

  if v_state.member_id is null then
    raise exception using errcode = 'P0001', message = 'member_state_missing';
  end if;

  insert into public.team_events (
    team_id,
    actor_member_id,
    event_type,
    payload,
    created_at
  )
  values (
    v_member.team_id,
    p_member_id,
    'member_activity_changed',
    pg_catalog.jsonb_build_object(
      'attendance_session_id', v_session.id,
      'activity_status', p_new_status::text
    ),
    v_now
  );

  return v_state;
end;
$$;

create or replace function public.check_out(
  p_member_id uuid,
  p_reason public.checkout_reason default 'manual'
)
returns public.member_current_state
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_member public.members;
  v_session public.attendance_sessions;
  v_state public.member_current_state;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  if p_reason is null or p_reason not in ('manual', 'app_exit', 'os_shutdown') then
    raise exception using errcode = '42501', message = 'checkout_reason_not_allowed';
  end if;

  select m.*
  into v_member
  from public.members as m
  where m.id = p_member_id
    and m.is_active
  for update;

  if v_member.id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  select s.*
  into v_session
  from public.attendance_sessions as s
  where s.member_id = p_member_id
    and s.checked_out_at is null
  for update;

  if v_session.id is null then
    select state_row.*
    into v_state
    from public.member_current_state as state_row
    where state_row.member_id = p_member_id;

    if v_state.member_id is null then
      raise exception using errcode = 'P0001', message = 'member_state_missing';
    end if;

    return v_state;
  end if;

  update public.activity_intervals as interval_row
  set ended_at = v_now
  where interval_row.attendance_session_id = v_session.id
    and interval_row.ended_at is null;

  if not found then
    raise exception using errcode = 'P0001', message = 'open_activity_interval_missing';
  end if;

  update public.attendance_sessions as s
  set checked_out_at = v_now,
      checkout_reason = p_reason
  where s.id = v_session.id;

  update public.member_current_state as state_row
  set attendance_session_id = null,
      attendance_status = 'clocked_out',
      activity_status = null,
      connection_status = 'disconnected',
      checked_in_at = null,
      status_started_at = null,
      last_heartbeat_at = null,
      last_checked_out_at = v_now,
      updated_at = v_now
  where state_row.member_id = p_member_id
  returning state_row.* into v_state;

  if v_state.member_id is null then
    raise exception using errcode = 'P0001', message = 'member_state_missing';
  end if;

  insert into public.team_events (
    team_id,
    actor_member_id,
    event_type,
    payload,
    created_at
  )
  values (
    v_member.team_id,
    p_member_id,
    'member_checked_out',
    pg_catalog.jsonb_build_object(
      'attendance_session_id', v_session.id,
      'checkout_reason', p_reason::text,
      'checked_out_at', v_now
    ),
    v_now
  );

  return v_state;
end;
$$;

create or replace function public.heartbeat(
  p_member_id uuid,
  p_attendance_session_id uuid,
  p_client_instance_id uuid
)
returns public.member_current_state
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_session public.attendance_sessions;
  v_state public.member_current_state;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  select s.*
  into v_session
  from public.attendance_sessions as s
  where s.id = p_attendance_session_id
    and s.member_id = p_member_id
    and s.checked_out_at is null
  for update;

  if v_session.id is null then
    raise exception using errcode = '55000', message = 'attendance_session_not_open';
  end if;

  if v_session.client_instance_id <> p_client_instance_id then
    raise exception using errcode = '42501', message = 'client_instance_mismatch';
  end if;

  update public.attendance_sessions as s
  set last_heartbeat_at = v_now
  where s.id = v_session.id;

  update public.member_current_state as state_row
  set last_heartbeat_at = v_now,
      connection_status = 'connected',
      updated_at = v_now
  where state_row.member_id = p_member_id
    and state_row.attendance_session_id = v_session.id
  returning state_row.* into v_state;

  if v_state.member_id is null then
    raise exception using errcode = 'P0001', message = 'member_state_session_mismatch';
  end if;

  return v_state;
end;
$$;

-- Intended for pg_cron or another trusted server-side scheduler. It is never
-- executable by anon/authenticated clients. Effective checkout is the last
-- accepted heartbeat so the grace period is not counted as work time.
create or replace function public.close_stale_attendance_sessions(
  p_timeout interval default interval '3 minutes',
  p_batch_size integer default 100
)
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_checkout_at timestamptz;
  v_closed_count integer := 0;
  v_session public.attendance_sessions;
begin
  if p_timeout < interval '1 minute' or p_timeout > interval '1 hour' then
    raise exception using errcode = '22023', message = 'timeout_out_of_range';
  end if;

  if p_batch_size < 1 or p_batch_size > 1000 then
    raise exception using errcode = '22023', message = 'batch_size_out_of_range';
  end if;

  for v_session in
    select s.*
    from public.attendance_sessions as s
    where s.checked_out_at is null
      and s.last_heartbeat_at < v_now - p_timeout
    order by s.last_heartbeat_at, s.id
    for update skip locked
    limit p_batch_size
  loop
    v_checkout_at := greatest(
      v_session.checked_in_at,
      least(v_session.last_heartbeat_at, v_now)
    );

    update public.activity_intervals as interval_row
    set ended_at = greatest(interval_row.started_at, v_checkout_at)
    where interval_row.attendance_session_id = v_session.id
      and interval_row.ended_at is null;

    update public.attendance_sessions as session_row
    set checked_out_at = v_checkout_at,
        checkout_reason = 'auto_timeout'
    where session_row.id = v_session.id;

    update public.member_current_state as state_row
    set attendance_session_id = null,
        attendance_status = 'clocked_out',
        activity_status = null,
        connection_status = 'disconnected',
        checked_in_at = null,
        status_started_at = null,
        last_heartbeat_at = null,
        last_checked_out_at = v_checkout_at,
        updated_at = v_now
    where state_row.member_id = v_session.member_id
      and state_row.attendance_session_id = v_session.id;

    insert into public.team_events (
      team_id,
      actor_member_id,
      event_type,
      payload,
      created_at
    )
    values (
      v_session.team_id,
      v_session.member_id,
      'member_checked_out',
      pg_catalog.jsonb_build_object(
        'attendance_session_id', v_session.id,
        'checkout_reason', 'auto_timeout',
        'checked_out_at', v_checkout_at
      ),
      v_now
    );

    v_closed_count := v_closed_count + 1;
  end loop;

  return v_closed_count;
end;
$$;

alter table public.teams enable row level security;
alter table public.members enable row level security;
alter table public.attendance_sessions enable row level security;
alter table public.activity_intervals enable row level security;
alter table public.member_current_state enable row level security;
alter table public.team_events enable row level security;

create policy teams_select_same_team
on public.teams
for select
to authenticated
using (
  (select auth.uid()) is not null
  and id = public.current_member_team_id()
);

create policy members_select_same_team
on public.members
for select
to authenticated
using (
  (select auth.uid()) is not null
  and team_id = public.current_member_team_id()
);

create policy attendance_sessions_select_same_team
on public.attendance_sessions
for select
to authenticated
using (
  (select auth.uid()) is not null
  and team_id = public.current_member_team_id()
);

create policy activity_intervals_select_same_team
on public.activity_intervals
for select
to authenticated
using (
  (select auth.uid()) is not null
  and public.can_access_attendance_session(attendance_session_id)
);

create policy member_current_state_select_same_team
on public.member_current_state
for select
to authenticated
using (
  (select auth.uid()) is not null
  and public.can_access_member(member_id)
);

create policy team_events_select_same_team
on public.team_events
for select
to authenticated
using (
  (select auth.uid()) is not null
  and team_id = public.current_member_team_id()
);

-- Table writes are deliberately absent: authenticated clients mutate state only
-- through the transaction-safe SECURITY DEFINER RPCs above.
revoke all on table public.teams from anon, authenticated;
revoke all on table public.members from anon, authenticated;
revoke all on table public.attendance_sessions from anon, authenticated;
revoke all on table public.activity_intervals from anon, authenticated;
revoke all on table public.member_current_state from anon, authenticated;
revoke all on table public.team_events from anon, authenticated;

grant select on table public.teams to authenticated;
grant select on table public.members to authenticated;
grant select on table public.attendance_sessions to authenticated;
grant select on table public.activity_intervals to authenticated;
grant select on table public.member_current_state to authenticated;
grant select on table public.team_events to authenticated;

revoke all on function public.normalize_member_name(text) from public, anon, authenticated;
revoke all on function public.current_member_team_id() from public, anon, authenticated;
revoke all on function public.can_access_member(uuid) from public, anon, authenticated;
revoke all on function public.can_access_attendance_session(uuid) from public, anon, authenticated;
revoke all on function public.claim_member_name(text, uuid, text) from public, anon, authenticated;
revoke all on function public.check_in(uuid, uuid) from public, anon, authenticated;
revoke all on function public.change_activity(uuid, public.activity_status) from public, anon, authenticated;
revoke all on function public.check_out(uuid, public.checkout_reason) from public, anon, authenticated;
revoke all on function public.heartbeat(uuid, uuid, uuid) from public, anon, authenticated;
revoke all on function public.close_stale_attendance_sessions(interval, integer) from public, anon, authenticated;

grant execute on function public.current_member_team_id() to authenticated;
grant execute on function public.can_access_member(uuid) to authenticated;
grant execute on function public.can_access_attendance_session(uuid) to authenticated;
grant execute on function public.claim_member_name(text, uuid, text) to authenticated;
grant execute on function public.check_in(uuid, uuid) to authenticated;
grant execute on function public.change_activity(uuid, public.activity_status) to authenticated;
grant execute on function public.check_out(uuid, public.checkout_reason) to authenticated;
grant execute on function public.heartbeat(uuid, uuid, uuid) to authenticated;
grant execute on function public.close_stale_attendance_sessions(interval, integer) to service_role;

do $$
begin
  if exists (
    select 1
    from pg_catalog.pg_publication
    where pubname = 'supabase_realtime'
  ) then
    if not exists (
      select 1
      from pg_catalog.pg_publication_tables
      where pubname = 'supabase_realtime'
        and schemaname = 'public'
        and tablename = 'member_current_state'
    ) then
      execute 'alter publication supabase_realtime add table public.member_current_state';
    end if;

    if not exists (
      select 1
      from pg_catalog.pg_publication_tables
      where pubname = 'supabase_realtime'
        and schemaname = 'public'
        and tablename = 'team_events'
    ) then
      execute 'alter publication supabase_realtime add table public.team_events';
    end if;
  else
    raise notice 'supabase_realtime publication is absent; realtime tables were not registered';
  end if;
end
$$;

commit;


