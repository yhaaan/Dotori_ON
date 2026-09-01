begin;

-- Renaming has been failing with derived_email_malformed since the project was
-- renamed to DOTORION.
--
-- The client derives the Auth email as m<32 hex>@<domain>, and that domain moved
-- from teamoverlay.invalid to dotorion.invalid with everything else. This check
-- did not move with it, so every rename was rejected for looking wrong while
-- being exactly right.
--
-- The domain is dropped from the check rather than corrected to the new one.
-- These are shape checks, not a security boundary - the original said so - and
-- the caller can already send any valid-looking pair for its own account, so
-- pinning the domain never protected anything. What it did do was couple this
-- function to a constant in the client that nothing keeps in step, which is the
-- whole of this bug. Any .invalid host is now accepted; the local part still has
-- to be the derived digest, which is what stops a malformed request from writing
-- credentials nothing can sign in with.
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
  if p_email !~ '^m[0-9a-f]{32}@[a-z0-9.-]+\.invalid$' then
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
