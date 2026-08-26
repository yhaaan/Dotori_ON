-- Development seed: member rows are intentionally not faked. Each member must
-- first obtain a Supabase Auth session, then atomically claim a name so that
-- members.id always equals auth.uid().
insert into public.teams (id, name, timezone)
values (
  '00000000-0000-4000-8000-000000000001',
  'Project DDD',
  'Asia/Seoul'
)
on conflict (id) do update
set name = excluded.name,
    timezone = excluded.timezone;
