begin;

-- The Unity first-run UI counts user-perceived text elements, while PostgreSQL
-- char_length counts Unicode code points. Sixteen is therefore also enforced as
-- a conservative server-side ceiling; the client may reject an overlong grapheme
-- sequence earlier with its more precise text-element rule.
alter table public.members
  drop constraint if exists members_display_name_length_check;

alter table public.members
  add constraint members_display_name_length_check
  check (pg_catalog.char_length(display_name) between 1 and 16);

create or replace function public.enforce_member_display_name_policy()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $function$
begin
  if pg_catalog.char_length(new.display_name) not between 1 and 16 then
    raise exception using
      errcode = '22023',
      message = 'invalid_member_name';
  end if;

  return new;
end
$function$;

drop trigger if exists members_enforce_display_name_policy on public.members;
create trigger members_enforce_display_name_policy
before insert or update of display_name on public.members
for each row
execute function public.enforce_member_display_name_policy();

revoke all on function public.enforce_member_display_name_policy()
  from public, anon, authenticated;

commit;
