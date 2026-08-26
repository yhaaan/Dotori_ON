begin;

-- Signing in has to happen before a name can be claimed, so every rejected
-- claim (team_full, member_name_taken, invalid_member_name) leaves an anonymous
-- Auth user that owns nothing. Those cannot be prevented entirely from the
-- client, and they accumulate forever without a sweep.
--
-- Requires auth.users.is_anonymous, which current GoTrue provides. Failing loudly
-- on an older instance is preferable to matching rows by a looser condition and
-- deleting a real account.
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
  -- A grace period is the only thing separating an abandoned identity from one
  -- that is between signup and claim right now.
  if p_minimum_age is null or p_minimum_age < interval '5 minutes' then
    raise exception using errcode = '22023', message = 'minimum_age_out_of_range';
  end if;

  if p_batch_size is null or p_batch_size not between 1 and 1000 then
    raise exception using errcode = '22023', message = 'batch_size_out_of_range';
  end if;

  with doomed as (
    select u.id
    from auth.users as u
    where u.is_anonymous
      and u.created_at < v_now - p_minimum_age
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
  'Removes anonymous Auth users that never claimed a member name. Never touches a user that owns a member row.';

-- Lets the client refuse to sign up at all when the team is already full, so the
-- most common rejection stops creating garbage in the first place. Only a count
-- is exposed; member names are not readable before joining.
create or replace function public.team_capacity(p_team_id uuid default null)
returns table (occupied integer, capacity integer)
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_team_id uuid;
begin
  if p_team_id is null then
    if (select pg_catalog.count(*) from public.teams) <> 1 then
      raise exception using errcode = '22023', message = 'team_id_required';
    end if;

    select t.id
    into v_team_id
    from public.teams as t
    order by t.created_at, t.id
    limit 1;
  else
    v_team_id := p_team_id;
  end if;

  return query
    select
      (
        select pg_catalog.count(*)::integer
        from public.members as m
        where m.team_id = v_team_id
      ),
      -- Matches the sort_order 0..3 slot allocation in claim_member_name.
      4;
end;
$$;

comment on function public.team_capacity(uuid) is
  'Occupied slot count and hard capacity for a team. Readable before joining so a full team can be reported without creating an Auth user.';

revoke all on function public.delete_orphan_anonymous_users(interval, integer)
  from public, anon, authenticated;
revoke all on function public.team_capacity(uuid) from public;

grant execute on function public.delete_orphan_anonymous_users(interval, integer) to service_role;
grant execute on function public.team_capacity(uuid) to anon, authenticated;

commit;
