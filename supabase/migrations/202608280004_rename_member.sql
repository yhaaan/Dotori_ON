begin;

-- Renaming used to mean losing everything. The name is the identity here: the
-- Auth account's email and password are both derived from it, so changing the
-- name changed the credentials, and the only way through was to sign out and
-- sign in as somebody new. Every session, interval and check-in stayed behind
-- with the old name.
--
-- This does the whole swap in one transaction instead. The member row and the
-- Auth account move together or neither moves, because the half-done state is
-- the one that cannot be recovered from: an account whose credentials no longer
-- match its name is an account nobody can sign in to.
--
-- The credentials are supplied by the caller rather than derived here. The
-- client is the authority on its own derivation - it is the thing that has to
-- reproduce them on the next launch - and a server that derived them
-- independently would only introduce a way for the two to disagree. Nothing is
-- disclosed by passing them: the derivation ships inside the build, so anyone
-- holding the app can already compute the credentials for any name.

-- pgcrypto lives in the extensions schema on Supabase. Probing it here means a
-- deployment without it fails now, loudly, rather than at the first rename.
do $$
begin
  perform extensions.crypt('probe', extensions.gen_salt('bf'));
exception
  when undefined_function or invalid_schema_name then
    raise exception using
      errcode = '0A000',
      message = 'rename_member requires pgcrypto in the extensions schema';
end;
$$;

create or replace function public.rename_member(
  p_display_name text,
  p_email text,
  p_password text
)
returns public.members
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_member_id uuid := (select auth.uid());
  v_member public.members;
begin
  if v_member_id is null then
    raise exception using errcode = '42501', message = 'authentication_required';
  end if;

  -- Only ever the caller. Taking a member id would be a way to rename a
  -- teammate, and with it a way to take their credentials.
  if not exists (
    select 1 from public.members as m where m.id = v_member_id and m.is_active
  ) then
    raise exception using errcode = '42501', message = 'member_identity_mismatch';
  end if;

  -- Shape checks only. They are not a security boundary - the caller could send
  -- any derived-looking pair for its own account - but they stop a malformed
  -- request from writing credentials nothing can ever sign in with.
  if p_email !~ '^m[0-9a-f]{32}@teamoverlay\.invalid$' then
    raise exception using errcode = '22023', message = 'derived_email_malformed';
  end if;

  if p_password !~ '^[0-9a-f]{64}$' then
    raise exception using errcode = '22023', message = 'derived_password_malformed';
  end if;

  -- The display name's length, canonical form and uniqueness are all enforced by
  -- the constraints and the trigger on members, so the update below is the
  -- validation. normalized_name is generated, and follows on its own.
  begin
    update public.members as m
    set display_name = p_display_name,
        updated_at = pg_catalog.clock_timestamp()
    where m.id = v_member_id
    returning m.* into v_member;
  exception
    when unique_violation then
      raise exception using errcode = '23505', message = 'member_name_taken';
  end;

  -- Written straight to the Auth tables rather than through GoTrue, because
  -- GoTrue's own endpoint cannot be part of this transaction and a rename that
  -- only half applied is exactly what this function exists to prevent. Sessions
  -- already issued keep working: nothing here revokes them, so the app carries
  -- on and simply signs in with the new credentials next launch.
  update auth.users as u
  set email = p_email,
      encrypted_password = extensions.crypt(p_password, extensions.gen_salt('bf')),
      updated_at = pg_catalog.clock_timestamp()
  where u.id = v_member_id;

  -- Recent GoTrue looks a password grant up through identities, so an identity
  -- left on the old address would leave the account unreachable under either
  -- name. Guarded because older projects have no such table.
  if pg_catalog.to_regclass('auth.identities') is not null then
    update auth.identities as i
    set provider_id = p_email,
        identity_data = pg_catalog.jsonb_set(
          i.identity_data,
          '{email}',
          pg_catalog.to_jsonb(p_email),
          true
        ),
        updated_at = pg_catalog.clock_timestamp()
    where i.user_id = v_member_id
      and i.provider = 'email';
  end if;

  return v_member;
end;
$$;

comment on function public.rename_member(text, text, text) is
  'Renames the calling member and moves their Auth credentials to match, in one transaction. Every session, interval and check-in stays with them.';

revoke all on function public.rename_member(text, text, text)
  from public, anon, authenticated;
grant execute on function public.rename_member(text, text, text) to authenticated;

commit;
