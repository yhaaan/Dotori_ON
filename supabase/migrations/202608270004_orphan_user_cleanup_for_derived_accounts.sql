begin;

-- Identity is now derived from the visible name (email + password computed from
-- it) so that a member is reachable from any PC. Those accounts are not
-- anonymous, so the previous sweep's `is_anonymous` filter no longer matches the
-- users a rejected claim leaves behind.
--
-- The only accounts this project ever creates are name-derived ones, so "no
-- member row and old enough" is the whole definition of an orphan. The grace
-- period remains the single thing separating an abandoned identity from one that
-- is between signup and claim right now.
create or replace function public.delete_orphan_anonymous_users(
  p_minimum_age interval default interval '30 minutes',
  p_batch_size integer default 100
)
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_deleted integer := 0;
begin
  if p_minimum_age is null or p_minimum_age < interval '5 minutes' then
    raise exception using errcode = '22023', message = 'minimum_age_out_of_range';
  end if;

  if p_batch_size is null or p_batch_size not between 1 and 1000 then
    raise exception using errcode = '22023', message = 'batch_size_out_of_range';
  end if;

  with doomed as (
    select u.id
    from auth.users as u
    where u.created_at < v_now - p_minimum_age
      and not exists (
        select 1
        from public.members as m
        where m.id = u.id
      )
    order by u.created_at
    limit p_batch_size
  )
  delete from auth.users as target
  using doomed
  where target.id = doomed.id;

  get diagnostics v_deleted = row_count;
  return v_deleted;
end;
$$;

comment on function public.delete_orphan_anonymous_users(interval, integer) is
  'Removes Auth users that never claimed a member name. Never touches a user that owns a member row.';

commit;
