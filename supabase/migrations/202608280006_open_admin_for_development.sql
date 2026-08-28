begin;

-- TEMPORARY, for development only.
--
-- The admin gate is opened to every active member because nobody has a settled
-- account yet: the team is still being made and unmade while the app is built,
-- and a flag set by hand on a row that is about to be deleted protects nothing
-- and locks the dashboard away from the person building it.
--
-- The column stays. Going back to a real gate before the team runs on this for
-- real is one line here - restore the is_admin lookup below - plus setting the
-- flag once from the SQL editor:
--   update public.members set is_admin = true where display_name = '이름';
--
-- Nothing else has to change: every admin function asks this one question.
create or replace function public.is_team_admin()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (
    select 1
    from public.members as m
    where m.id = (select auth.uid()) and m.is_active
  );
$$;

comment on function public.is_team_admin() is
  'DEVELOPMENT: currently true for every active member. Restore the is_admin lookup before the team relies on this.';

commit;
