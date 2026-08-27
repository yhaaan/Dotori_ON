begin;

-- Picking a profile icon is not an act of being at work: unlike a status note it
-- describes the person, not the session, so this deliberately does not require an
-- open attendance session the way set_status_note does.
--
-- The key is the catalog entry's name, not a file path or a URL. Storing a name
-- means the client owns the artwork: adding an icon ships a sprite, and no row
-- ever points at an image that a later build removed - an unknown key simply
-- falls back to the name initial.
create or replace function public.set_avatar_key(
  p_member_id uuid,
  p_avatar_key text
)
returns public.members
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_avatar_key text;
  v_member public.members;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  -- Same normalisation as claim_member_name, so clearing the icon and never
  -- having picked one are the same stored value.
  v_avatar_key := coalesce(nullif(pg_catalog.btrim(p_avatar_key), ''), 'default');

  if pg_catalog.char_length(v_avatar_key) not between 1 and 64
     or v_avatar_key !~ '^[A-Za-z0-9._-]+$' then
    raise exception using errcode = '22023', message = 'invalid_avatar_key';
  end if;

  update public.members as m
  set avatar_key = v_avatar_key
  where m.id = p_member_id
    and m.is_active
  returning m.* into v_member;

  if v_member.id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  -- The roster is read from member_current_state, so touching updated_at is what
  -- makes the new icon show up on the other clients' next poll.
  update public.member_current_state as state_row
  set updated_at = pg_catalog.clock_timestamp()
  where state_row.member_id = p_member_id;

  return v_member;
end;
$$;

comment on function public.set_avatar_key(uuid, text) is
  'Sets the calling member''s profile icon to a client catalog key. Allowed while clocked out.';

revoke all on function public.set_avatar_key(uuid, text) from public, anon, authenticated;
grant execute on function public.set_avatar_key(uuid, text) to authenticated;

commit;
