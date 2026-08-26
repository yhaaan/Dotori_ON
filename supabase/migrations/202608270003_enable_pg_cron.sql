begin;

-- The two cleanup migrations register their jobs only when pg_cron already
-- exists, so on a project without the extension they silently did nothing:
-- close_stale_attendance_sessions has never actually run in production, which
-- means heartbeat-based auto checkout was never active.
--
-- Enabling the extension here and (re)registering both jobs makes the schedule
-- part of the migration history instead of a manual dashboard step.
create extension if not exists pg_cron;

do $$
declare
  v_existing_job_id bigint;
begin
  if pg_catalog.to_regclass('cron.job') is null
     or pg_catalog.to_regprocedure('cron.schedule(text,text,text)') is null then
    raise notice 'pg_cron is still unavailable; both cleanup jobs must be scheduled externally';
    return;
  end if;

  execute 'select jobid from cron.job where jobname = $1 order by jobid limit 1'
  into v_existing_job_id
  using 'team-overlay-heartbeat-cleanup';

  if v_existing_job_id is null then
    execute 'select cron.schedule($1, $2, $3)'
    using
      'team-overlay-heartbeat-cleanup',
      '* * * * *',
      'select public.close_stale_attendance_sessions(interval ''3 minutes'', 100);';
  end if;

  execute 'select jobid from cron.job where jobname = $1 order by jobid limit 1'
  into v_existing_job_id
  using 'team-overlay-orphan-user-cleanup';

  if v_existing_job_id is null then
    execute 'select cron.schedule($1, $2, $3)'
    using
      'team-overlay-orphan-user-cleanup',
      '17 * * * *',
      'select public.delete_orphan_anonymous_users(interval ''30 minutes'', 100);';
  end if;
end
$$;

commit;
