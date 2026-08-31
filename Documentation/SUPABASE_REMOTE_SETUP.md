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

## Anonymous user cleanup

`202608270001_orphan_anonymous_user_cleanup.sql` and
`202608270002_schedule_orphan_user_cleanup.sql` were listed here as pending for a
while; `npx supabase db push --dry-run` on 2026-08-31 no longer offers them, so
they are applied. Check the dry run rather than this list - it reads the remote
migration history and this paragraph does not.

They exist because signing in must happen before a name can be claimed, so every
rejected claim leaves an anonymous Auth user that owns nothing and that the
client has no permission to delete.

- `delete_orphan_anonymous_users(interval, integer)` removes anonymous users with
  no member row once they are older than a grace period. It never touches a user
  that owns a member row. Granted to `service_role` only and scheduled hourly
  through pg_cron when the extension is available.
- `team_capacity(uuid)` reports occupied and total slots and is granted to `anon`
  so the client can refuse to sign up into a full team instead of creating an
  identity the server is guaranteed to reject. Only counts are exposed; member
  names stay unreadable before joining.

`delete_orphan_anonymous_users` requires `auth.users.is_anonymous`. On an older
GoTrue the migration fails loudly rather than matching rows by a looser condition.

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
