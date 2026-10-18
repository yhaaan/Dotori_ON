begin;

-- Display-only event progress. The optional event schema never changes attendance or roster queries.
-- round is where the member is fighting now (it can go down when they pick an earlier round);
-- max_round is the furthest round they have reached and only ever grows.
create table public.chiikawa_event_rounds (
  member_id uuid primary key,
  team_id uuid not null,
  round integer not null check (round between 1 and 100),
  max_round integer not null default 1 check (max_round between 1 and 100),
  updated_at timestamptz not null default pg_catalog.clock_timestamp(),
  foreign key (team_id, member_id) references public.members (team_id, id) on delete cascade
);
alter table public.chiikawa_event_rounds enable row level security;
create policy chiikawa_rounds_same_team on public.chiikawa_event_rounds
  for select to authenticated
  using (team_id = public.current_member_team_id());
revoke all on public.chiikawa_event_rounds from anon, authenticated;
grant select on public.chiikawa_event_rounds to authenticated;

create function public.set_chiikawa_event_round(p_round integer, p_max_round integer)
returns void language plpgsql security definer set search_path = '' as $$
declare v_member public.members;
begin
  if (select auth.uid()) is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;
  if p_round is null or p_round < 1 or p_round > 100
    or p_max_round is null or p_max_round < 1 or p_max_round > 100 then
    raise exception using errcode = '22023', message = 'invalid_round';
  end if;
  select m.* into v_member from public.members m
    where m.id = (select auth.uid()) and m.is_active;
  if not found then
    raise exception using errcode = '42501', message = 'active_member_required';
  end if;
  insert into public.chiikawa_event_rounds as r (member_id, team_id, round, max_round)
    values (v_member.id, v_member.team_id, p_round, greatest(p_round, p_max_round))
    on conflict (member_id) do update
    set round = excluded.round,
        max_round = greatest(r.max_round, excluded.max_round),
        updated_at = pg_catalog.clock_timestamp();
end;
$$;
revoke all on function public.set_chiikawa_event_round(integer, integer) from public, anon;
grant execute on function public.set_chiikawa_event_round(integer, integer) to authenticated;

commit;
