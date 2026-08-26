begin;

-- Application configuration, not development fixture data. The first four
-- Auth identities claim slots in this single team through claim_member_name.
insert into public.teams (id, name, timezone)
values (
  '00000000-0000-4000-8000-000000000001',
  'Project DDD',
  'Asia/Seoul'
)
on conflict (id) do update
set name = excluded.name,
    timezone = excluded.timezone;

commit;
