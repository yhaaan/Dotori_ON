\set ON_ERROR_STOP on

begin;

create or replace function pg_temp.assert_true(p_condition boolean, p_message text)
returns void
language plpgsql
as $$
begin
  if p_condition is distinct from true then
    raise exception 'assertion_failed: %', p_message;
  end if;
end;
$$;

-- Stable test identities. The transaction is rolled back at the end.
insert into auth.users (id, aud, role, created_at, updated_at)
values
  ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'authenticated', 'authenticated', now(), now()),
  ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb', 'authenticated', 'authenticated', now(), now()),
  ('cccccccc-cccc-4ccc-8ccc-cccccccccccc', 'authenticated', 'authenticated', now(), now()),
  ('dddddddd-dddd-4ddd-8ddd-dddddddddddd', 'authenticated', 'authenticated', now(), now())
on conflict (id) do nothing;

insert into public.teams (id, name, timezone)
values (
  '00000000-0000-4000-8000-000000000001',
  'Project DDD',
  'Asia/Seoul'
)
on conflict (id) do nothing;

set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","role":"authenticated"}',
  true
);

select pg_temp.assert_true(
  (select claimed.display_name from public.claim_member_name(
    '  Alice   Kim  ',
    '00000000-0000-4000-8000-000000000001',
    'default'
  ) as claimed) = 'Alice Kim',
  'claim_member_name must trim and collapse whitespace'
);

select pg_temp.assert_true(
  (select m.normalized_name from public.members as m
   where m.id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') = 'alice kim',
  'normalized_name must be lower-case canonical display name'
);

select pg_temp.assert_true(
  (select claimed.id from public.claim_member_name(
    'Alice Kim',
    '00000000-0000-4000-8000-000000000001',
    'default'
  ) as claimed) = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  'repeating the same claim must be idempotent'
);

do $verify$
begin
  begin
    perform public.claim_member_name(
      'Different Name',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected member_name_already_claimed';
  exception
    when sqlstate '23505' then
      if sqlerrm <> 'member_name_already_claimed' then
        raise;
      end if;
  end;
end
$verify$;

reset role;
set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","role":"authenticated"}',
  true
);

select pg_temp.assert_true(
  (select claimed.normalized_name from public.claim_member_name(
    'Bora',
    '00000000-0000-4000-8000-000000000001',
    'default'
  ) as claimed) = 'bora',
  'second authenticated user must claim another handle'
);

reset role;
set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"cccccccc-cccc-4ccc-8ccc-cccccccccccc","role":"authenticated"}',
  true
);

do $verify$
begin
  begin
    perform public.claim_member_name(
      '12345678901234567',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected invalid_member_name';
  exception
    when sqlstate '22023' then
      if sqlerrm <> 'invalid_member_name' then
        raise;
      end if;
  end;
end
$verify$;

do $verify$
begin
  begin
    perform public.claim_member_name(
      ' ALICE    KIM ',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected member_name_taken';
  exception
    when sqlstate '23505' then
      if sqlerrm <> 'member_name_taken' then
        raise;
      end if;
  end;
end
$verify$;

do $verify$
begin
  begin
    perform public.claim_member_name(
      'Ａｌｉｃｅ　Ｋｉｍ',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected NFKC member_name_taken';
  exception
    when sqlstate '23505' then
      if sqlerrm <> 'member_name_taken' then
        raise;
      end if;
  end;
end
$verify$;

do $verify$
begin
  begin
    perform public.claim_member_name(
      'Name😀',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected invalid_member_name for symbol';
  exception
    when sqlstate '22023' then
      if sqlerrm <> 'invalid_member_name' then
        raise;
      end if;
  end;
end
$verify$;

do $verify$
begin
  begin
    perform public.claim_member_name(
      '___---',
      '00000000-0000-4000-8000-000000000001',
      'default'
    );
    raise exception 'expected invalid_member_name without alphanumeric';
  exception
    when sqlstate '22023' then
      if sqlerrm <> 'invalid_member_name' then
        raise;
      end if;
  end;
end
$verify$;

reset role;
set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","role":"authenticated"}',
  true
);

select public.check_in(
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  '11111111-1111-4111-8111-111111111111'
);

-- Same-client retry must not create a duplicate session or event.
select public.check_in(
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  '11111111-1111-4111-8111-111111111111'
);

select pg_temp.assert_true(
  (select pg_catalog.count(*)
   from public.attendance_sessions as s
   where s.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and s.checked_out_at is null) = 1,
  'check_in retry must leave one open session'
);

select pg_temp.assert_true(
  (select pg_catalog.count(*)
   from public.team_events as e
   where e.actor_member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and e.event_type = 'member_checked_in') = 1,
  'check_in retry must emit one event'
);

select public.change_activity(
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  'break'
);

select pg_temp.assert_true(
  (select state_row.activity_status
   from public.member_current_state as state_row
   where state_row.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') = 'break',
  'change_activity must immediately update current state'
);

select pg_temp.assert_true(
  (select pg_catalog.count(*)
   from public.activity_intervals as interval_row
   where interval_row.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and interval_row.ended_at is null
     and interval_row.status = 'break') = 1,
  'change_activity must close the old interval and open one new interval'
);

select public.heartbeat(
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  (select state_row.attendance_session_id
   from public.member_current_state as state_row
   where state_row.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'),
  '11111111-1111-4111-8111-111111111111'
);

do $verify$
begin
  begin
    perform public.heartbeat(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      (select state_row.attendance_session_id
       from public.member_current_state as state_row
       where state_row.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'),
      '99999999-9999-4999-8999-999999999999'
    );
    raise exception 'expected client_instance_mismatch';
  exception
    when sqlstate '42501' then
      if sqlerrm <> 'client_instance_mismatch' then
        raise;
      end if;
  end;
end
$verify$;

do $verify$
begin
  begin
    perform public.check_in(
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      '22222222-2222-4222-8222-222222222222'
    );
    raise exception 'expected member_identity_mismatch';
  exception
    when sqlstate '42501' then
      if sqlerrm <> 'member_identity_mismatch' then
        raise;
      end if;
  end;
end
$verify$;

select public.check_out(
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  'manual'
);

select pg_temp.assert_true(
  (select state_row.attendance_status
   from public.member_current_state as state_row
   where state_row.member_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') = 'clocked_out',
  'check_out must make current state offline'
);

reset role;
set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","role":"authenticated"}',
  true
);

select public.check_in(
  'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  '22222222-2222-4222-8222-222222222222'
);

reset role;

-- Age one open session without sleeping so timeout cleanup is deterministic.
update public.attendance_sessions as s
set checked_in_at = pg_catalog.clock_timestamp() - interval '10 minutes',
    last_heartbeat_at = pg_catalog.clock_timestamp() - interval '4 minutes'
where s.member_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
  and s.checked_out_at is null;

update public.activity_intervals as interval_row
set started_at = pg_catalog.clock_timestamp() - interval '10 minutes'
where interval_row.member_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
  and interval_row.ended_at is null;

update public.member_current_state as state_row
set checked_in_at = pg_catalog.clock_timestamp() - interval '10 minutes',
    status_started_at = pg_catalog.clock_timestamp() - interval '10 minutes',
    last_heartbeat_at = pg_catalog.clock_timestamp() - interval '4 minutes'
where state_row.member_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

select pg_temp.assert_true(
  public.close_stale_attendance_sessions(interval '3 minutes', 100) = 1,
  'timeout cleanup must close one stale session'
);

select pg_temp.assert_true(
  (select s.checkout_reason
   from public.attendance_sessions as s
   where s.member_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
   order by s.created_at desc
   limit 1) = 'auto_timeout',
  'timeout cleanup must record auto_timeout'
);

select pg_temp.assert_true(
  (select s.checked_out_at = s.last_heartbeat_at
   from public.attendance_sessions as s
   where s.member_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
   order by s.created_at desc
   limit 1),
  'timeout checkout must use the last accepted heartbeat, not grace-period end'
);

insert into public.teams (id, name, timezone)
values ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', 'Other Team', 'Asia/Seoul');

set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"dddddddd-dddd-4ddd-8ddd-dddddddddddd","role":"authenticated"}',
  true
);

select public.claim_member_name(
  'Duri',
  'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
  'default'
);

reset role;
set local role authenticated;
select pg_catalog.set_config(
  'request.jwt.claims',
  '{"sub":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","role":"authenticated"}',
  true
);

select pg_temp.assert_true(
  not exists (
    select 1
    from public.members as m
    where m.id = 'dddddddd-dddd-4ddd-8ddd-dddddddddddd'
  ),
  'RLS must hide members from another team'
);

do $verify$
begin
  begin
    update public.members
    set display_name = 'Tampered'
    where id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    raise exception 'expected direct update permission denial';
  exception
    when sqlstate '42501' then
      null;
  end;
end
$verify$;

reset role;

select pg_temp.assert_true(
  not pg_catalog.has_function_privilege(
    'anon',
    'public.check_in(uuid,uuid)',
    'EXECUTE'
  ),
  'anon must not execute mutation RPCs'
);

select pg_temp.assert_true(
  not pg_catalog.has_function_privilege(
    'authenticated',
    'public.close_stale_attendance_sessions(interval,integer)',
    'EXECUTE'
  ),
  'authenticated clients must not execute server cleanup'
);

rollback;

\echo 'Milestone 2 SQL verification passed.'


