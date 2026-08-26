begin;

-- pg_cron is optional in local and self-hosted environments. Do not install or
-- assume it here: register the job only when the extension is already enabled.
-- This keeps `supabase db reset` working even when cron is unavailable.
do $$
declare
  v_existing_job_id bigint;
begin
  if pg_catalog.to_regclass('cron.job') is null
     or pg_catalog.to_regprocedure('cron.schedule(text,text,text)') is null then
    raise notice 'pg_cron is not enabled; schedule close_stale_attendance_sessions externally';
    return;
  end if;

  execute
    'select jobid from cron.job where jobname = $1 order by jobid limit 1'
  into v_existing_job_id
  using 'team-overlay-heartbeat-cleanup';

  if v_existing_job_id is null then
    execute 'select cron.schedule($1, $2, $3)'
    using
      'team-overlay-heartbeat-cleanup',
      '* * * * *',
      'select public.close_stale_attendance_sessions(interval ''3 minutes'', 100);';
  end if;
end
$$;

commit;
