begin;

-- Profile photos grow from 64x64 to 96x96.
--
-- The card draws the avatar at 48 units, which is 48, 72 or 96 pixels at the
-- 100 / 150 / 200 % UI scales. A 64 pixel photo had to be stretched at 150 and
-- 200 % and shrunk by an uneven 4:3 at 100 %, so it looked soft everywhere.
-- 96 pixels is drawn 1:1 at 200 % and halved exactly at 100 %.
--
-- 64x64 stays accepted: clients from before this change keep uploading that
-- size, and every client draws whatever size it is sent.

alter table public.member_avatar_photos
  drop constraint member_avatar_photos_size_check;

-- 64 KiB of PNG, base64 encoded. A 96x96 photo is about half of that.
alter table public.member_avatar_photos
  add constraint member_avatar_photos_size_check
    check (pg_catalog.char_length(png_base64) between 1 and 87384);

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
  v_size bytea;
  v_version integer;
  v_member public.members;
begin
  if (select auth.uid()) is null or (select auth.uid()) <> p_member_id then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  if p_png_base64 is null or pg_catalog.char_length(p_png_base64) not between 1 and 87384 then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end if;

  begin
    v_png := pg_catalog.decode(p_png_base64, 'base64');
  exception when others then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end;

  -- PNG signature, then the IHDR chunk's width and height (bytes 17..24):
  -- 96x96 from current clients, 64x64 from older ones.
  if pg_catalog.octet_length(v_png) < 33
     or pg_catalog.substr(v_png, 1, 8) <> '\x89504e470d0a1a0a'::bytea
     or pg_catalog.substr(v_png, 13, 4) <> '\x49484452'::bytea then
    raise exception using errcode = '22023', message = 'invalid_avatar_photo';
  end if;

  v_size := pg_catalog.substr(v_png, 17, 8);
  if v_size <> '\x0000006000000060'::bytea
     and v_size <> '\x0000004000000040'::bytea then
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

  update public.member_current_state as state_row
  set updated_at = pg_catalog.clock_timestamp()
  where state_row.member_id = p_member_id;

  return v_member;
end;
$$;

comment on function public.set_avatar_photo(uuid, text) is
  'Stores the calling member''s cropped 96x96 (or legacy 64x64) PNG and points avatar_key at it. Allowed while clocked out.';

revoke all on function public.set_avatar_photo(uuid, text) from public, anon, authenticated;
grant execute on function public.set_avatar_photo(uuid, text) to authenticated;

commit;
