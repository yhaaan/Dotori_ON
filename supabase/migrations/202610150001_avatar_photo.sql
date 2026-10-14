begin;

-- A member may wear a photo of their own instead of a catalog icon.
--
-- The photo is cropped and shrunk on the client to a 64x64 PNG before it is
-- sent, so a row is a few kilobytes and a whole team (four members at most) is
-- well under a megabyte. That is small enough to keep in Postgres beside the
-- member instead of in Storage: it is read and written with the same session
-- and the same team rule as every other row, and there is no second set of
-- bucket policies to keep in step with these.
--
-- members.avatar_key still says what the card draws. A photo is announced as
-- 'photo.<version>', which already fits members_avatar_key_check. The roster
-- poll carries that key to every client; a client that has not seen this
-- version yet fetches the row once and caches it, and every later poll is free.
create table public.member_avatar_photos (
  member_id uuid primary key,
  team_id uuid not null,
  version integer not null,
  png_base64 text not null,
  updated_at timestamptz not null default pg_catalog.clock_timestamp(),
  constraint member_avatar_photos_member_fkey
    foreign key (team_id, member_id)
    references public.members (team_id, id)
    on delete cascade,
  constraint member_avatar_photos_version_check check (version >= 1),
  -- 32 KiB of PNG, base64 encoded. A 64x64 photo is about a third of that.
  constraint member_avatar_photos_size_check
    check (pg_catalog.char_length(png_base64) between 1 and 43692)
);

comment on table public.member_avatar_photos is
  'One cropped 64x64 PNG per member, announced through members.avatar_key = photo.<version>.';

alter table public.member_avatar_photos enable row level security;

create policy member_avatar_photos_select_same_team
on public.member_avatar_photos
for select
to authenticated
using (
  (select auth.uid()) is not null
  and team_id = public.current_member_team_id()
);

revoke all on table public.member_avatar_photos from anon, authenticated;
grant select on table public.member_avatar_photos to authenticated;

-- Writes go only through this function, which is where the picture is checked:
-- it must really be a PNG and really be 64x64, so the column can only ever hold
-- what the client is built to draw, whatever someone sends by hand.
create or replace function public.set_avatar_photo(
  p_member_id uuid,
  p_png_base64 text
)
returns public.members
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_png bytea;
  v_version integer;
  v_member public.members;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  if p_png_base64 is null or pg_catalog.char_length(p_png_base64) not between 1 and 43692 then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end if;

  begin
    v_png := pg_catalog.decode(p_png_base64, 'base64');
  exception when others then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end;

  -- PNG signature, then the IHDR chunk's width and height (bytes 16..23).
  if pg_catalog.octet_length(v_png) < 33
     or pg_catalog.substr(v_png, 1, 8) <> '\x89504e470d0a1a0a'::bytea
     or pg_catalog.substr(v_png, 13, 4) <> '\x49484452'::bytea
     or pg_catalog.substr(v_png, 17, 8) <> '\x0000004000000040'::bytea then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end if;

  select m.* into v_member
  from public.members as m
  where m.id = p_member_id
    and m.is_active;

  if v_member.id is null then
    raise exception using errcode = '42501', message = 'member_not_registered_or_inactive';
  end if;

  insert into public.member_avatar_photos as photo (member_id, team_id, version, png_base64)
  values (p_member_id, v_member.team_id, 1, p_png_base64)
  on conflict (member_id) do update
  set version = photo.version + 1,
      png_base64 = excluded.png_base64,
      updated_at = pg_catalog.clock_timestamp()
  returning photo.version into v_version;

  update public.members as m
  set avatar_key = 'photo.' || v_version::text
  where m.id = p_member_id
  returning m.* into v_member;

  -- Same as set_avatar_key: the roster is read from member_current_state, so
  -- touching it is what carries the new key to the other clients.
  update public.member_current_state as state_row
  set updated_at = pg_catalog.clock_timestamp()
  where state_row.member_id = p_member_id;

  return v_member;
end;
$$;

comment on function public.set_avatar_photo(uuid, text) is
  'Stores the calling member''s cropped 64x64 PNG and points avatar_key at it. Allowed while clocked out.';

revoke all on function public.set_avatar_photo(uuid, text) from public, anon, authenticated;
grant execute on function public.set_avatar_photo(uuid, text) to authenticated;

commit;
