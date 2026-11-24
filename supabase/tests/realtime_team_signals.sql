\set ON_ERROR_STOP on

-- LOCAL ONLY, after `npx supabase start` and `npx supabase db reset`.
-- Run as the local database owner. All fixtures and fault injection roll back;
-- seed.sql and existing teams are left untouched. This checks database RLS, not
-- an actual WebSocket join, which still needs the two-client manual check.
begin;

create function pg_temp.assert_true(p_condition boolean, p_message text)
returns void language plpgsql as $$
begin
  if p_condition is distinct from true then
    raise exception 'assertion_failed: %', p_message;
  end if;
end;
$$;

create function pg_temp.assert_counts(p_state bigint, p_member bigint, p_nudge bigint)
returns void language plpgsql security definer set search_path = '' as $$
declare
  v_state bigint;
  v_member bigint;
  v_nudge bigint;
begin
  select count(*) filter (where payload->>'kind' = 'state'),
         count(*) filter (where payload->>'kind' = 'member'),
         count(*) filter (where payload->>'kind' = 'nudge')
  into v_state, v_member, v_nudge
  from realtime.messages
  where topic = 'team:11111111-1111-4111-8111-111111111111'
    and event = 'team_signal' and private;
  if (v_state, v_member, v_nudge) is distinct from (p_state, p_member, p_nudge) then
    raise exception 'signal counts expected (%,%,%), got (%,%,%)',
      p_state, p_member, p_nudge, v_state, v_member, v_nudge;
  end if;
end;
$$;

insert into auth.users (id, aud, role, created_at, updated_at) values
  ('aaaaaaaa-1111-4111-8111-111111111111', 'authenticated', 'authenticated', now(), now()),
  ('bbbbbbbb-2222-4222-8222-222222222222', 'authenticated', 'authenticated', now(), now());
insert into public.teams (id, name) values
  ('11111111-1111-4111-8111-111111111111', 'Realtime test A'),
  ('22222222-2222-4222-8222-222222222222', 'Realtime test B');
insert into public.members (id, team_id, display_name, sort_order) values
  ('aaaaaaaa-1111-4111-8111-111111111111', '11111111-1111-4111-8111-111111111111', 'RT fixture A', 0),
  ('bbbbbbbb-2222-4222-8222-222222222222', '22222222-2222-4222-8222-222222222222', 'RT fixture B', 0);
insert into public.member_current_state (member_id) values
  ('aaaaaaaa-1111-4111-8111-111111111111'),
  ('bbbbbbbb-2222-4222-8222-222222222222');
select pg_temp.assert_counts(1, 1, 0);

-- No-op assignments and metadata-only updates must not generate traffic.
update public.members set display_name = display_name, avatar_key = avatar_key,
  sort_order = sort_order, is_active = is_active
where id in ('aaaaaaaa-1111-4111-8111-111111111111', 'bbbbbbbb-2222-4222-8222-222222222222');
update public.member_current_state set updated_at = clock_timestamp()
where member_id = 'aaaaaaaa-1111-4111-8111-111111111111';
select pg_temp.assert_counts(1, 1, 0);

set local role authenticated;
select set_config('request.jwt.claims',
  '{"sub":"aaaaaaaa-1111-4111-8111-111111111111","role":"authenticated"}', true);
select public.check_in('aaaaaaaa-1111-4111-8111-111111111111', 'cccccccc-3333-4333-8333-333333333333');
select pg_temp.assert_counts(2, 1, 0);
select public.heartbeat('aaaaaaaa-1111-4111-8111-111111111111',
  (select attendance_session_id from public.member_current_state
   where member_id = 'aaaaaaaa-1111-4111-8111-111111111111'),
  'cccccccc-3333-4333-8333-333333333333');
select pg_temp.assert_counts(2, 1, 0);
select public.change_activity('aaaaaaaa-1111-4111-8111-111111111111', 'break');
select pg_temp.assert_counts(3, 1, 0);
select public.set_status_note('aaaaaaaa-1111-4111-8111-111111111111', 'Realtime test');
select pg_temp.assert_counts(4, 1, 0);
select public.set_status_note('aaaaaaaa-1111-4111-8111-111111111111', 'Realtime test');
select pg_temp.assert_counts(4, 1, 0);
select public.set_avatar_key('aaaaaaaa-1111-4111-8111-111111111111', 'realtime-test');
select pg_temp.assert_counts(4, 2, 0);
select public.set_avatar_key('aaaaaaaa-1111-4111-8111-111111111111', 'realtime-test');
select pg_temp.assert_counts(4, 2, 0);
select public.send_nudge();
select pg_temp.assert_counts(4, 2, 1);
select public.check_out('aaaaaaaa-1111-4111-8111-111111111111', 'manual');
select pg_temp.assert_counts(5, 2, 1);
reset role;

-- A real connection-status change is still visible, unlike normal heartbeats.
select public.check_in('aaaaaaaa-1111-4111-8111-111111111111', 'cccccccc-3333-4333-8333-333333333333');
update public.member_current_state set connection_status = 'degraded'
where member_id = 'aaaaaaaa-1111-4111-8111-111111111111';
select pg_temp.assert_counts(7, 2, 1);
select public.heartbeat('aaaaaaaa-1111-4111-8111-111111111111',
  (select attendance_session_id from public.member_current_state
   where member_id = 'aaaaaaaa-1111-4111-8111-111111111111'),
  'cccccccc-3333-4333-8333-333333333333');
select pg_temp.assert_counts(8, 2, 1);

-- The policy must be active and helper functions must not become RPC entry points.
select pg_temp.assert_true((select relrowsecurity from pg_catalog.pg_class
  where oid = 'realtime.messages'::regclass), 'Realtime messages RLS must be enabled');
select pg_temp.assert_true(
  not has_function_privilege('authenticated', 'public.send_team_signal(uuid,text,uuid)', 'EXECUTE')
  and not has_function_privilege('anon', 'public.send_team_signal(uuid,text,uuid)', 'EXECUTE'),
  'Signal publishing must remain trigger-only');

set local role authenticated;
select set_config('realtime.topic', 'team:11111111-1111-4111-8111-111111111111', true);
select pg_temp.assert_true(exists (select 1 from realtime.messages
  where topic = 'team:11111111-1111-4111-8111-111111111111' and event = 'team_signal'),
  'Own team topic must authorize reads');
select set_config('realtime.topic', 'team:22222222-2222-4222-8222-222222222222', true);
select pg_temp.assert_true(not exists (select 1 from realtime.messages
  where topic = 'team:22222222-2222-4222-8222-222222222222'),
  'Another team topic must not authorize reads');
select set_config('realtime.topic', 'team:11111111-1111-4111-8111-111111111111', true);
do $$
begin
  begin
    insert into realtime.messages (topic, extension, event, payload, private)
    values ('team:11111111-1111-4111-8111-111111111111', 'broadcast', 'team_signal', '{}', true);
    raise exception 'Client publication unexpectedly succeeded';
  exception when insufficient_privilege then null;
  end;
end;
$$;
reset role;

-- Force a delivery failure without replacing any Supabase platform functions.
-- The fixture-only trigger and the RPC mutation both disappear on rollback.
create function pg_temp.reject_test_signal()
returns trigger language plpgsql as $$
begin
  raise exception 'Injected local delivery failure';
end;
$$;
create trigger realtime_test_delivery_failure before insert on realtime.messages
  for each row when (new.topic = 'team:11111111-1111-4111-8111-111111111111')
  execute function pg_temp.reject_test_signal();
select public.change_activity('aaaaaaaa-1111-4111-8111-111111111111', 'meal');
select pg_temp.assert_true((select activity_status = 'meal' from public.member_current_state
  where member_id = 'aaaaaaaa-1111-4111-8111-111111111111'),
  'Failed delivery must not roll back the original mutation');
select pg_temp.assert_counts(8, 2, 1);
drop trigger realtime_test_delivery_failure on realtime.messages;

-- Delete coverage uses the other team, which has no attendance history.
delete from public.member_current_state where member_id = 'bbbbbbbb-2222-4222-8222-222222222222';
select pg_temp.assert_true((select count(*) = 2 from realtime.messages
  where topic = 'team:22222222-2222-4222-8222-222222222222' and payload->>'kind' = 'state'),
  'State INSERT and DELETE each emit a signal');
delete from public.members where id = 'bbbbbbbb-2222-4222-8222-222222222222';
select pg_temp.assert_true((select count(*) = 2 from realtime.messages
  where topic = 'team:22222222-2222-4222-8222-222222222222' and payload->>'kind' = 'member'),
  'Member DELETE must retain the old team topic');

rollback;
