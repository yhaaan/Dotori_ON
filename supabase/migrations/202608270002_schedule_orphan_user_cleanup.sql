begin;

-- Same optional-pg_cron handling as the heartbeat sweep: register the job only
-- when the extension is already enabled so `supabase db reset` keeps working
-- locally. Hourly is plenty for identities that are only created by a failed
-- first launch.
do $$
declare
  v_existing_job_id bigint;
begin
  if pg_catalog.to_regclass('cron.job') is null
     or pg_catalog.to_regprocedure('cron.schedule(text,text,text)') is null then
    raise notice 'pg_cron is not enabled; schedule delete_orphan_anonymous_users externally';
    return;
  end if;

  execute
    'select jobid from cron.job where jobname = $1 order by jobid limit 1'
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
