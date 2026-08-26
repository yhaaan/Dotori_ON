# Supabase remote setup

## Linked project

- Project ref: `pperuinfufndfathcosf`
- Project URL: `https://pperuinfufndfathcosf.supabase.co`
- Supabase CLI profile: `Project-DDD`
- Anonymous Auth: enabled and verified through the public Auth settings endpoint

The publishable key is included in the Unity public client configuration because
it is designed to ship in desktop applications and remains constrained by Auth
and RLS. The CLI access token, database password, secret key, and service-role key
are not recorded in this repository and must never be included in a player build.

## Deployment verification

On 2026-08-26, `supabase db push --dry-run` reported the expected initial five pending
migrations. A follow-up application-config migration added the single production
team required by name claiming. Both pushes completed successfully. `supabase migration list`
confirmed identical local and remote versions:

- `202608260001_core_schema.sql`
- `202608260002_security_and_rpcs.sql`
- `202608260003_schedule_heartbeat_cleanup.sql`
- `202608260004_member_name_policy_hardening.sql`
- `202608260005_unicode_member_name_policy.sql`
- `202608260006_default_project_team.sql`

Production seed data was not pushed. This avoids treating local fixture data as
remote application data.

## Safe maintenance commands

Preview remote schema changes before every deployment:

```powershell
npx supabase db push --dry-run
npx supabase db push
npx supabase migration list
```

The CLI login token is stored in the user's Supabase CLI settings outside this
repository. Never pass it as a command-line flag committed to scripts or copy it
into Unity configuration.
