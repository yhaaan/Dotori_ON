-- Wipes every record of what the team did, and keeps who the team is.
--
-- Run it in the Supabase SQL editor. It is not a migration: nothing about the
-- schema changes, and a one-time erase does not belong in a history that gets
-- replayed onto a fresh database.
--
-- KEPT      members (names, icons, slots, is_admin), auth accounts, teams
-- ERASED    attendance sessions, activity intervals, check-ins and points,
--           team events, and every member's current status
--
-- There is no undo. Everyone stays able to sign in with the same name; they
-- simply have no history behind them afterwards.
--
-- The order matters. attendance_sessions, activity_intervals and team_events
-- are all ON DELETE RESTRICT so a stray cascade can never take attendance
-- history with it, which means the references have to be released deliberately,
-- innermost first. member_current_state points at a session, so it is cleared
-- before the sessions it points at.
begin;

-- Back to "never clocked in". The shape check insists a clocked-out row carries
-- no session, no activity, no timestamps, so every one of them is cleared in the
-- same statement rather than left half set.
update public.member_current_state
set attendance_session_id = null,
    attendance_status = 'clocked_out',
    activity_status = null,
    connection_status = 'disconnected',
    checked_in_at = null,
    status_started_at = null,
    last_heartbeat_at = null,
    last_checked_out_at = null,
    updated_at = pg_catalog.clock_timestamp();

delete from public.activity_intervals;
delete from public.attendance_sessions;
delete from public.team_events;
delete from public.member_check_ins;

-- What is left, so the result can be read rather than assumed.
select
  (select count(*) from public.members) as members_kept,
  (select count(*) from public.attendance_sessions) as sessions,
  (select count(*) from public.activity_intervals) as intervals,
  (select count(*) from public.member_check_ins) as check_ins,
  (select count(*) from public.team_events) as events,
  (select count(*) from public.member_current_state
   where attendance_status <> 'clocked_out') as still_clocked_in;

commit;
