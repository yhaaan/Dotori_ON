# Team Overlay Supabase backend

This directory contains the reproducible database part of milestone 2. It is
designed for Supabase Auth, Postgres, Realtime, RLS, and optional Supabase Cron.

## Identity and first-launch contract

`members.id` is always the Supabase Auth UUID (`auth.uid()`). The display name is
a globally unique, user-facing handle, but it is not a database primary key and
is never used by foreign keys.

The eventual Unity first-launch flow should be:

1. Restore the persisted Supabase Auth session. If none exists, call anonymous
   sign-in. Anonymous Auth users still use the `authenticated` database role.
2. Query `members` for `id = auth.uid()`.
3. If the row exists, use its `display_name` and skip name setup.
4. If it does not exist, show name setup and call `claim_member_name` once.
5. Persist and auto-refresh the Auth session. Do not treat a locally cached name
   as identity; every server operation is authorized by the Auth UUID.

Name rules are enforced again in Postgres, which is the final authority:

- Unicode NFKC normalization (so compatibility characters cannot bypass identity);
- trim leading/trailing whitespace;
- collapse consecutive whitespace to one space;
- 1 through 16 characters after normalization;
- letters/numbers, spaces, underscore, and hyphen only, with at least one letter or number;
- no control characters;
- global uniqueness of the lower-case `normalized_name`.

The database's POSIX `[[:alnum:]]` character class follows its UTF-8 locale.
Supabase-managed UTF-8 databases recognize the intended Unicode letters and numbers,
but this must be rechecked if the schema is moved to a differently collated/self-hosted
PostgreSQL database.

Calling `claim_member_name` again with the same normalized name is idempotent.
Trying to rename an existing identity returns SQLSTATE `23505` with
`member_name_already_claimed`. Claiming another person's name returns SQLSTATE
`23505` with `member_name_taken`.

Anonymous Auth credentials must be stored reliably. If the local Auth session is
deleted before the anonymous identity is linked to a recoverable login method,
the user cannot prove ownership of the reserved name. Account linking/recovery is
therefore a required follow-up before broader distribution.

## Schema and invariants

- `teams`: team metadata and IANA timezone (seeded as `Asia/Seoul`).
- `members`: Auth-bound identity, unique normalized handle, fixed slot 0..3.
- `attendance_sessions`: at most one open session per member.
- `activity_intervals`: at most one open activity per attendance session.
- `member_current_state`: the small Realtime snapshot consumed by the overlay.
- `team_events`: append-only check-in/activity/check-out event stream.

All timestamps are `timestamptz` generated from the database clock. The client
only sends IDs and requested state; it never sends authoritative timestamps.

Authenticated clients receive `SELECT` only on same-team rows through RLS. They
have no direct table-write grants. Mutations go through transaction-safe,
`SECURITY DEFINER` RPCs whose `search_path` is empty and whose object names are
schema-qualified:

| RPC | Purpose |
| --- | --- |
| `claim_member_name` | Bind the current Auth UUID to one unique name and team slot |
| `check_in` | Open attendance plus initial `working` interval and event |
| `change_activity` | Close the current interval, open another, update state/event |
| `check_out` | Close activity/session and publish the offline state/event |
| `heartbeat` | Validate member, session, and client instance before refreshing |
| `close_stale_attendance_sessions` | Trusted server cleanup; not callable by clients |

`check_in` is idempotent only for the same `client_instance_id`. A different app
instance is rejected while a session is open, enforcing one active device per
member. `change_activity` also refreshes heartbeat because a successful state
change proves the client is connected.

## Local setup and verification

Prerequisites are the Supabase CLI and Docker. From the repository root:

```powershell
supabase init
supabase start
supabase db reset
psql "postgresql://postgres:postgres@127.0.0.1:54322/postgres" `
  -v ON_ERROR_STOP=1 `
  -f supabase/tests/milestone_2.sql
```

Run `supabase init` only while `supabase/config.toml` is absent. Migration `202608260006_default_project_team.sql` creates the single shared
application team in every environment. The default `supabase/seed.sql` is applied
after migrations by `supabase start` and `supabase db reset`; it idempotently
reasserts that local team configuration but deliberately does not fake member rows: real/anonymous Auth identities claim those rows through the
same production RPC.

The verification script runs inside a transaction and rolls back. It covers name
normalization/collision, immutable identity, check-in retry, activity intervals,
heartbeat instance validation, checkout, timeout cleanup, RLS team isolation,
and denial of direct writes/client cleanup execution.

For a linked development project, preview before applying:

```powershell
supabase db push --dry-run
supabase db push
```

Do not use a linked database reset against production. Do not use
`--include-seed` for production data.

## Heartbeat and Cron

The client target is one heartbeat every 45 seconds while clocked in. Cleanup
uses a three-minute timeout and runs every minute. An automatic timeout closes at
the last accepted heartbeat, not at the end of the grace period, so disconnected
time is not over-counted.

Migration `202608260003_schedule_heartbeat_cleanup.sql` checks whether pg_cron is
already enabled. If it is unavailable (common in a minimal local Postgres), the
migration emits a notice and succeeds instead of breaking database reset.

On a hosted project, enable Supabase Cron/`pg_cron` from **Integrations → Cron**
before applying the scheduler migration. If migrations were applied before Cron
was enabled, register the job once from the SQL editor:

```sql
select cron.schedule(
  'team-overlay-heartbeat-cleanup',
  '* * * * *',
  $$select public.close_stale_attendance_sessions(interval '3 minutes', 100);$$
);
```

The cleanup RPC is granted only to `service_role` and remains a server-side
operation. A Unity build may contain only the project's publishable/legacy anon
key. Never place the `service_role` key in client configuration, source files,
logs, or a shipped build.

## Client RPC payload shapes

After Auth and name claim, examples sent through the Supabase Data API are:

```json
{"p_display_name":"Yohan","p_team_id":"00000000-0000-4000-8000-000000000001","p_avatar_key":"default"}
{"p_member_id":"<auth.uid>","p_client_instance_id":"<installation UUID>"}
{"p_member_id":"<auth.uid>","p_new_status":"break"}
{"p_member_id":"<auth.uid>","p_reason":"manual"}
{"p_member_id":"<auth.uid>","p_attendance_session_id":"<open session UUID>","p_client_instance_id":"<installation UUID>"}
```

Realtime subscriptions should be limited to `member_current_state` and
`team_events`. UI timers are calculated locally from server timestamps rather
than written every second.

## References

- [Supabase users and anonymous-user role behavior](https://supabase.com/docs/guides/auth/users)
- [Supabase Row Level Security guidance](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [Supabase local development workflow](https://supabase.com/docs/guides/local-development/cli-workflows)
- [Supabase database seeding](https://supabase.com/docs/guides/local-development/seeding-your-database)
- [Supabase Cron](https://supabase.com/docs/guides/cron)
