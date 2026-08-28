begin;

-- A developer flag, not a security boundary. The whole identity model is that a
-- name is the credential, and the derivation ships inside the build, so anyone
-- holding the app can sign in as the admin by typing their name. What this stops
-- is a teammate wandering into a destructive screen, which is the thing that
-- actually happens; it is not, and cannot be, protection against someone who
-- means it.
--
-- Nobody starts with it. The first admin is set by hand, once, from the SQL
-- editor:
--   update public.members set is_admin = true where display_name = '이름';
alter table public.members
  add column if not exists is_admin boolean not null default false;

create or replace function public.is_team_admin()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select coalesce(
    (
      select m.is_admin
      from public.members as m
      where m.id = (select auth.uid()) and m.is_active
    ),
    false
  );
$$;

-- Everything the developer dashboard lists, in one round trip. The numbers are
-- the same ones the statistics panel reports; what this adds is the shape of the
-- roster itself - who is active, who is admin, who has never clocked in.
create or replace function public.admin_member_overview()
returns table (
  member_id uuid,
  display_name text,
  avatar_key text,
  sort_order smallint,
  is_active boolean,
  is_admin boolean,
  created_at timestamptz,
  session_count integer,
  attendance_seconds integer,
  total_points integer,
  last_checked_out_at timestamptz
)
language plpgsql
stable
security definer
set search_path = ''
as $$
begin
  if not public.is_team_admin() then
    raise exception using errcode = '42501', message = 'admin_required';
  end if;

  return query
  select
    m.id,
    m.display_name,
    m.avatar_key,
    m.sort_order,
    m.is_active,
    m.is_admin,
    m.created_at,
    (
      select pg_catalog.count(*)::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
    ),
    (
      select coalesce(
        pg_catalog.sum(
          pg_catalog.date_part(
            'epoch',
            coalesce(s.checked_out_at, pg_catalog.clock_timestamp()) - s.checked_in_at
          )
        ),
        0
      )::integer
      from public.attendance_sessions as s
      where s.member_id = m.id
    ),
    (
      select coalesce(pg_catalog.sum(c.points), 0)::integer
      from public.member_check_ins as c
      where c.member_id = m.id
    ),
    (
      select pg_catalog.max(s.checked_out_at)
      from public.attendance_sessions as s
      where s.member_id = m.id
    )
  from public.members as m
  where m.team_id = public.current_member_team_id()
  order by m.sort_order, m.id;
end;
$$;

-- Erases a member and everything recorded under them, and frees their team slot.
--
-- The dependent tables are on delete restrict on purpose: losing attendance
-- history to a stray cascade is exactly what that was protecting against. So
-- this clears them in dependency order rather than relaxing the constraints,
-- which keeps the accident impossible everywhere except here, where it is the
-- whole point. There is no undo.
create or replace function public.admin_delete_member(p_member_id uuid)
returns void
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member public.members;
begin
  if not public.is_team_admin() then
    raise exception using errcode = '42501', message = 'admin_required';
  end if;

  -- Deleting yourself would take the admin flag with it and leave the team with
  -- no way back into this screen. The SQL editor is the right place for that.
  if p_member_id = (select auth.uid()) then
    raise exception using errcode = '42501', message = 'admin_cannot_delete_self';
  end if;

  select m.* into v_member
  from public.members as m
  where m.id = p_member_id and m.team_id = public.current_member_team_id();

  if v_member.id is null then
    raise exception using errcode = '42704', message = 'member_not_found';
  end if;

  delete from public.activity_intervals as i where i.member_id = p_member_id;
  delete from public.attendance_sessions as s where s.member_id = p_member_id;
  delete from public.team_events as e
  where e.actor_member_id = p_member_id or e.target_member_id = p_member_id;
  delete from public.member_check_ins as c where c.member_id = p_member_id;
  delete from public.member_current_state as st where st.member_id = p_member_id;

  -- members.id references auth.users on delete cascade, so the member row and
  -- the Auth account go together. Removing the account is what makes the name
  -- claimable again: leaving it behind would let the credentials for that name
  -- sign back in to nothing.
  delete from auth.users as u where u.id = p_member_id;
end;
$$;

comment on function public.admin_delete_member(uuid) is
  'Erases a teammate and every record under them, freeing their team slot. Admin only, never the caller, and there is no undo.';

revoke all on function public.is_team_admin() from public, anon, authenticated;
grant execute on function public.is_team_admin() to authenticated;
revoke all on function public.admin_member_overview() from public, anon, authenticated;
grant execute on function public.admin_member_overview() to authenticated;
revoke all on function public.admin_delete_member(uuid) from public, anon, authenticated;
grant execute on function public.admin_delete_member(uuid) to authenticated;

commit;
