# Milestone 4: live attendance over the deployed RPCs

Milestone 3 left identity live and everything else on `ProfiledMockTeamBackend`.
This milestone moves attendance, activity changes and heartbeats onto the
deployed Supabase project.

## Delivered flow

`SupabaseTeamBackend` implements `ITeamBackend` against the RPCs that were
already deployed in milestone 2:

| `ITeamBackend` | Server |
| --- | --- |
| `GetTeamStateAsync` | `GET /rest/v1/member_current_state?select=…,members!inner(…)` |
| `CheckInAsync` | `rpc/check_in` |
| `ChangeActivityAsync` | `rpc/change_activity` |
| `CheckOutAsync` | `rpc/check_out` |
| `SendHeartbeatAsync` | `rpc/heartbeat` |

The roster read joins `members` so one request returns both the current state and
the display name. Inactive members are dropped client-side.

## Polling instead of Realtime

`member_current_state` and `team_events` are registered with the
`supabase_realtime` publication, but the Unity client has no Phoenix-channel
WebSocket implementation and this project deliberately avoids the Unity Supabase
SDK. The roster is therefore polled every three seconds.

Team events are **derived by diffing consecutive snapshots** rather than read from
`team_events`:

- One request covers both the roster and the events, instead of two.
- A dropped poll degrades into a late event rather than a lost one.
- The first snapshot only seeds the baseline, so launching the app does not
  replay the whole roster as fresh check-ins and ring the notification tone.

Replacing polling with Realtime later only has to replace the periodic
`GetTeamStateAsync` call in `TeamOverlayApp.Update`; `ITeamBackend` and the view
are unaffected. `team_events` becomes necessary once nudges or emotes are added,
because those carry no state to diff.

## Heartbeat

`SendHeartbeatAsync` existed on the interface but nothing ever called it. The app
now heartbeats every 45 seconds, comfortably inside the server's three-minute
`close_stale_attendance_sessions` timeout, so one failed heartbeat never clocks
anyone out.

Heartbeats are addressed to a specific `attendance_session_id`. The backend
captures it from the `check_in` response and from every roster read, and skips the
heartbeat entirely when there is no open session rather than guessing one. When
the server reports `attendance_session_not_open` or `client_instance_mismatch`,
the session id is forgotten so the client stops heartbeating into a session that
is gone.

## Failure behavior

- Poll failures back off exponentially from 3s to a 30s ceiling and log once per
  failure streak, so a dropped connection cannot flood the console.
- Heartbeat failures are warnings only and never interrupt the user.
- The attendance RPC error messages are mapped to Korean UI text
  (`member_already_clocked_in`, `client_instance_mismatch`, …) instead of surfacing
  raw server codes.
- `check_out` refuses `auto_timeout` and `admin` client-side; the server reserves
  those for its own closure path and rejects them with `checkout_reason_not_allowed`.

## Token ownership

`SupabaseIdentityClient` now implements `ISupabaseSessionProvider` and is the only
component that spends a refresh token. `SupabaseTeamBackend` asks it for a valid
access token before every request, so the two can never race each other into an
invalidated session.

## Mock backend

`TeamOverlayApp` keeps a `Use Mock Backend` inspector toggle. It is off by
default; turning it on runs the overlay against the in-memory roster so the UI can
be exercised without a network. Identity still uses the real project either way.

## Verification

- All three assemblies compile clean against the Unity 6000.3.8f1 Roslyn
  toolchain with the project's own compile arguments.
- `SupabaseTeamBackendTests` covers state mapping, inactive-member filtering,
  first-snapshot silence, check-in and activity-change event derivation, session
  capture for heartbeats, heartbeat suppression without a session, session
  forgetting on a closed session, and the server-only checkout reason guard.
- **The EditMode suite has not been executed for this milestone.** The Unity
  Editor held the project lock, so batch-mode test execution was unavailable. Run
  Test Runner before treating this milestone as verified.
- No live request has been made against the hosted project yet.

## Current boundary

- Automatic checkout on OS shutdown is still missing: `WM_QUERYENDSESSION` is not
  handled, so `os_shutdown` is never sent. The server's stale-session sweep covers
  it within three minutes.
- `team_events` is not read. Nudges, emotes and messages will need it.
- Cross-PC name handover still fails with `member_name_taken`; see
  `Docs/PREFAB_UI_EDITING.md`.
- Statistics screens (daily/cumulative rankings) are untouched.
