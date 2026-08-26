begin;

-- PostgreSQL/Supabase databases use UTF8. NFKC closes compatibility-character
-- bypasses (for example full-width Latin letters) before global uniqueness is
-- evaluated. NORMALIZE is PostgreSQL special syntax and cannot be schema-qualified.
create or replace function public.normalize_member_name(p_name text)
returns text
language sql
immutable
strict
parallel safe
set search_path = ''
as $$
  select pg_catalog.lower(
    pg_catalog.regexp_replace(
      pg_catalog.btrim(normalize(p_name, NFKC)),
      '[[:space:]]+',
      ' ',
      'g'
    )
  );
$$;

create or replace function public.enforce_member_display_name_policy()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $function$
declare
  v_name text;
begin
  v_name := pg_catalog.regexp_replace(
    pg_catalog.btrim(normalize(new.display_name, NFKC)),
    '[[:space:]]+',
    ' ',
    'g'
  );

  if v_name is null
     or pg_catalog.char_length(v_name) not between 1 and 16
     or v_name ~ '[[:cntrl:]]'
     or v_name !~ '^[[:alnum:]_ -]+$'
     or v_name !~ '[[:alnum:]]' then
    raise exception using
      errcode = '22023',
      message = 'invalid_member_name';
  end if;

  new.display_name := v_name;
  return new;
end
$function$;

-- Stored generated columns are recomputed on row update. This also canonicalizes
-- pre-existing display names through the BEFORE trigger. If two historical rows
-- collide after NFKC, the global unique constraint intentionally aborts migration
-- so an operator can resolve identity ownership instead of silently merging it.
update public.members
set display_name = display_name,
    updated_at = pg_catalog.clock_timestamp();

revoke all on function public.normalize_member_name(text)
  from public, anon, authenticated;
revoke all on function public.enforce_member_display_name_policy()
  from public, anon, authenticated;

commit;
