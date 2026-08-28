-- Wipes the team entirely: every record AND every account.
--
-- Run this once after the DOTORION rename, before anyone opens the new build.
--
-- The rename changed how credentials are derived (DerivedTeamCredentials'
-- EmailDomain and PasswordNamespace), so the email and password the app now
-- computes for a name no longer match the account that name used to have.
-- Nothing breaks quietly: sign-in fails, the app falls through to sign-up, and
-- the four-slot limit refuses it. Clearing the accounts is what lets everyone
-- claim their name again on the new derivation.
--
-- ERASED    everything - members, Auth users, sessions, intervals, check-ins,
--           points, team events
-- KEPT      the team row itself, so names can be claimed straight back into it
--
-- Afterwards each person opens the app, types the same name as before, and gets
-- a fresh member with no history. There is no undo.
--
-- Order matters: attendance_sessions, activity_intervals and team_events are
-- ON DELETE RESTRICT so a stray cascade can never take attendance history with
-- it, which means the references are released deliberately, innermost first.
-- member_current_state points at a session, so it goes before the sessions do.
begin;

delete from public.member_current_state;
delete from public.activity_intervals;
delete from public.attendance_sessions;
delete from public.team_events;
delete from public.member_check_ins;

-- members.id references auth.users on delete cascade, so removing the accounts
-- takes the member rows with them and frees every slot.
delete from auth.users
where id in (select m.id from public.members as m);

-- Anonymous users left behind by rejected name claims own nothing and would
-- otherwise pile up.
delete from auth.users
where is_anonymous
  and id not in (select m.id from public.members as m);

select
  (select count(*) from public.members) as members,
  (select count(*) from public.attendance_sessions) as sessions,
  (select count(*) from public.activity_intervals) as intervals,
  (select count(*) from public.member_check_ins) as check_ins,
  (select count(*) from public.team_events) as events,
  (select count(*) from public.teams) as teams_kept;

commit;
