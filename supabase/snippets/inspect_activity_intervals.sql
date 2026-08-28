-- Why are there still activity_intervals after the reset?
--
-- The reset deletes them, and nothing in the schema can silently refuse: the
-- table has no delete-blocking policy and no trigger. So rows that are there
-- afterwards are almost certainly new ones - checking in creates a session and
-- an interval in the same breath, so pressing 출근 once refills both.
--
-- created_at settles it. Compare it against when the reset was run:
--   newer  -> the app made them, nothing is wrong, and the sessions table will
--             have matching rows
--   older  -> the delete really did not take, and the sessions count below will
--             be zero while intervals are not, which should be impossible
select
  (select count(*) from public.activity_intervals) as intervals,
  (select count(*) from public.attendance_sessions) as sessions,
  (select pg_catalog.min(created_at) from public.activity_intervals) as oldest,
  (select pg_catalog.max(created_at) from public.activity_intervals) as newest,
  pg_catalog.clock_timestamp() as now;

-- The rows themselves, newest first, with who they belong to and whether the
-- session behind them still exists. An interval whose session is missing is the
-- only genuinely broken state here.
select
  m.display_name,
  i.status,
  i.started_at,
  i.ended_at,
  i.created_at,
  (s.id is not null) as session_exists
from public.activity_intervals as i
left join public.members as m on m.id = i.member_id
left join public.attendance_sessions as s on s.id = i.attendance_session_id
order by i.created_at desc
limit 20;
