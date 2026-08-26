begin;

-- A short "what I am doing right now" line shown on the member card. It lives on
-- member_current_state because the roster query already reads that table, so the
-- note costs no extra request. It is deliberately not part of the attendance
-- shape check: a note is independent of whether a session is open.
alter table public.member_current_state
  add column status_note text;

alter table public.member_current_state
  add constraint member_current_state_status_note_check
  check (
    status_note is null
    or (
      -- Cards are about 110px wide, so anything longer cannot be displayed.
      pg_catalog.char_length(status_note) between 1 and 24
      and status_note !~ '[[:cntrl:]]'
      and status_note = pg_catalog.btrim(status_note)
    )
  );

comment on column public.member_current_state.status_note is
  'Optional short free-text note shown on the member card. Cleared on check out.';

-- Only the owner may write their own note, and only while clocked in: the note
-- describes current work, so keeping it past a checkout would leave a stale line
-- next to an offline card.
create or replace function public.set_status_note(
  p_member_id uuid,
  p_note text
)
returns public.member_current_state
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_now timestamptz := pg_catalog.clock_timestamp();
  v_note text;
  v_state public.member_current_state;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  v_note := pg_catalog.regexp_replace(
    pg_catalog.btrim(coalesce(p_note, '')),
    '[[:space:]]+',
    ' ',
    'g'
  );

  -- Clearing the note is the empty string, which normalises to null.
  if v_note = '' then
    v_note := null;
  elsif pg_catalog.char_length(v_note) > 24 or v_note ~ '[[:cntrl:]]' then
    raise exception using errcode = '22023', message = 'invalid_status_note';
  end if;

  select state_row.*
  into v_state
  from public.member_current_state as state_row
  where state_row.member_id = p_member_id
  for update;

  if v_state.member_id is null then
    raise exception using errcode = 'P0001', message = 'member_state_missing';
  end if;

  if v_state.attendance_status <> 'clocked_in' then
    raise exception using errcode = '55000', message = 'member_not_clocked_in';
  end if;

  update public.member_current_state as state_row
  set status_note = v_note,
      updated_at = v_now
  where state_row.member_id = p_member_id
  returning state_row.* into v_state;

  return v_state;
end;
$$;

comment on function public.set_status_note(uuid, text) is
  'Sets or clears the calling member''s status note. Requires an open attendance session.';

revoke all on function public.set_status_note(uuid, text) from public, anon, authenticated;
grant execute on function public.set_status_note(uuid, text) to authenticated;

-- A trigger rather than an edit to check_out: every path that clocks somebody out
-- (manual, app exit, OS shutdown, the stale-session sweep) has to clear the note,
-- and this catches all of them without re-creating a verified function.
create or replace function public.clear_status_note_when_clocked_out()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.status_note := null;
  return new;
end;
$$;

create trigger member_current_state_clear_status_note
before update on public.member_current_state
for each row
when (new.attendance_status = 'clocked_out' and new.status_note is not null)
execute function public.clear_status_note_when_clocked_out();

commit;
