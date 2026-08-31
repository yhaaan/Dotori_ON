-- Wipes the project entirely: every record AND every Auth account.
--
-- Run it in the Supabase SQL editor. It is not a migration: nothing about the
-- schema changes, and a one-time erase does not belong in a history that gets
-- replayed onto a fresh database.
--
-- ERASED    every member, every Auth user, sessions, intervals, check-ins,
--           points, team events
-- KEPT      the team row, which is configuration rather than data - the app
--           claims names into it, and without it nobody can sign in at all.
--           202608260006_default_project_team.sql is what puts it there.
--
-- Afterwards each person opens the app, types a name, and gets a fresh member
-- with no history. There is no undo. Take a backup first if the history is
-- worth anything.
--
-- Order matters. attendance_sessions, activity_intervals and team_events are
-- ON DELETE RESTRICT, so a stray cascade can never drag attendance history out
-- with it; the references have to be released deliberately, innermost first.
-- member_current_state points at a session, so it goes before the sessions do.
begin;

delete from public.member_current_state;
delete from public.activity_intervals;
delete from public.attendance_sessions;
delete from public.team_events;
delete from public.member_check_ins;

-- Every account, not only the ones a member row still points at. Accounts whose
-- member row was removed some other way own nothing and are invisible in the
-- app, but they still hold a derived email, so the name they were built from
-- cannot be claimed again while they exist.
--
-- members.id references auth.users on delete cascade, so this takes the member
-- rows with it and frees every slot.
delete from auth.users;

select
  (select count(*) from auth.users) as auth_users,
  (select count(*) from public.members) as members,
  (select count(*) from public.attendance_sessions) as sessions,
  (select count(*) from public.activity_intervals) as intervals,
  (select count(*) from public.member_check_ins) as check_ins,
  (select count(*) from public.team_events) as events,
  (select count(*) from public.teams) as teams_kept;

commit;

-- Everything above must read 0 except teams_kept, which must read 1. If
-- teams_kept is 0 the team row was removed at some point and nobody can claim a
-- name until it is back:
--
--   insert into public.teams (id, name, timezone)
--   values ('00000000-0000-4000-8000-000000000001', 'Project DDD', 'Asia/Seoul')
--   on conflict (id) do nothing;
