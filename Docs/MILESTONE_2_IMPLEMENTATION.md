# Milestone 2 implementation

## Delivered scope

Milestone 2 now has two complementary parts:

1. A reproducible Supabase database contract for Auth-bound member identity,
   attendance/activity state, heartbeat cleanup, Realtime-facing snapshots, RLS,
   and transaction-safe RPCs.
2. A Unity first-run name flow that creates one durable local installation
   profile, skips setup on later launches, and injects the saved name into the
   current mock-backed overlay.

The visible name is a globally unique handle in the database design. It is not
the database primary key: `members.id` remains the immutable Supabase Auth UUID.
This keeps foreign keys and authorization stable while still giving the four
team members a simple human-readable identifier.

## Unity identity flow

- On a clean launch, `TeamOverlayApp` waits for a valid name before constructing
  the normal overlay view/backend.
- Names are Unicode NFKC-normalized, trimmed, whitespace-collapsed, limited to
  16 text elements, and restricted to letters/numbers, spaces, `_`, and `-`.
- `LocalIdentityProfileStore` writes
  `Application.persistentDataPath/identity/local-profile.json` atomically and
  maintains recovery candidates for interrupted writes.
- The saved profile contains only the display name, normalized unique-name key,
  installation UUID, schema version, and creation timestamp. It contains no
  access token, refresh token, or service key and does not use `PlayerPrefs`.
- A valid saved profile is restored automatically on subsequent launches. A
  corrupt profile is reported instead of silently replacing the identity.
- `ProfiledMockTeamBackend` makes the current mock milestone render and emit the
  saved local member name without changing the deterministic mock member ID.

Key implementation files:

- `Assets/_TeamOverlay/Scripts/Identity/DisplayNamePolicy.cs`
- `Assets/_TeamOverlay/Scripts/Identity/LocalIdentityProfileStore.cs`
- `Assets/_TeamOverlay/Scripts/UI/FirstRunNameView.cs`
- `Assets/_TeamOverlay/Scripts/UI/TeamOverlayApp.cs`
- `Assets/_TeamOverlay/Scripts/Backend/Mock/ProfiledMockTeamBackend.cs`

## Supabase database contract

Migrations under `supabase/migrations` create:

- `teams` and Auth-UUID-backed `members` with four fixed team slots;
- attendance sessions and activity intervals with one-open-row constraints;
- `member_current_state` and append-only `team_events` for lightweight Realtime;
- same-team read policies and revoked direct client writes;
- `SECURITY DEFINER` RPCs for claiming a name, check-in, activity changes,
  check-out, heartbeat, and trusted stale-session cleanup;
- optional one-minute Cron scheduling with a three-minute heartbeat timeout;
- database-side Unicode/name-policy enforcement and normalized-name uniqueness.

Client mutations must use RPCs. The shipped Unity client must never contain the
Supabase `service_role` key. See `supabase/README.md` for payloads, local reset,
SQL verification, hosted deployment, and Cron setup.

## Verification completed

- Unity EditMode source tests: **17 passed, 0 failed**.
- Windows release build: **Success** with Unity 6000.3.8f1.
- Clean runtime launch: name modal accepted `CodexTest`, then the overlay showed
  `CodexTest · 나`.
- Second runtime launch: name setup was skipped and the same saved name was
  restored automatically.
- The runtime test identity was moved out of LocalLow into the ignored validation
  logs after verification, so it does not become the developer's real identity.

Evidence is kept in the ignored `Logs/TeamOverlayValidation` directory, including
`editmode-m2-source-results.xml`, `build-m2-release.log`, and the first/second
launch screenshots.

## Integration boundary and next step

The Auth/member-registration portion of this boundary is now implemented. Unity
creates or restores an anonymous Supabase Auth session, stores it in Windows
Credential Manager, queries the Auth-UUID-backed member row, and calls
`claim_member_name` before committing the local profile. See
`Docs/MILESTONE_3_IDENTITY_INTEGRATION.md`.

The team-state backend is still the deterministic mock. The next implementation
stage is to replace it with Supabase attendance RPCs and Realtime subscriptions
to `member_current_state` and `team_events`.

Name alone is not proof of identity. Anonymous users still need an account-linking
and recovery path before broader distribution.

## Database verification status

Local verification is now complete:

- Docker Desktop 4.88.1 is installed with the WSL 2 backend.
- Supabase CLI 2.115.0 is pinned as a project development dependency.
- `supabase/config.toml` was initialized and anonymous Auth was enabled.
- All six migrations and `supabase/seed.sql` applied successfully.
- `supabase/tests/milestone_2.sql` completed with
  `Milestone 2 SQL verification passed.`
- Core local services (Postgres, Auth, REST, Realtime, Storage, Studio, and Kong)
  are running. Optional local Analytics is disabled because it is not needed by
  the overlay and its Vector collector was incompatible with the current Docker
  Desktop socket proxy.

Daily commands from the repository root:

```powershell
npm run supabase:start
npm run supabase:stop
npm run supabase:reset
```

Local Studio is available at `http://127.0.0.1:54323` while the stack is running.
Local credentials can be displayed when needed with `npx supabase status`; never
copy the local secret/service-role output into Unity source or version control.
