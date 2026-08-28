# Milestone 3: Supabase identity integration

## Delivered flow

Unity now uses the linked hosted Supabase project for identity registration:

1. Load a Supabase Auth session from Windows Credential Manager.
2. If none exists, create one anonymous Auth user and persist the session.
3. Refresh sessions within two minutes of access-token expiry and immediately
   persist the rotated refresh token.
4. Query `members` by the immutable Auth UUID.
5. If a member exists, restore its server name and skip name setup.
6. If no member exists, call `claim_member_name` and create the local profile
   only after the server accepts the unique name.
7. Existing milestone-2 local profiles are claimed into Supabase automatically
   when their Auth identity has no member row yet.

The display name remains a unique human-facing handle. Supabase Auth UUID is the
actual person/member primary key, and the local `client_instance_id` remains the
stable installation/device identifier.

## Security behavior

- The publishable key is public client configuration and is sent only as the
  `apikey` header.
- The signed-in user's JWT is sent as `Authorization: Bearer <JWT>` so RLS applies.
- Access and rotating refresh tokens are stored in Windows Credential Manager,
  not `PlayerPrefs`, JSON files, logs, or source control.
- If refresh credentials are invalid, the app refuses to silently create a new
  anonymous identity because doing that would orphan the old name ownership.
- Secret and service-role keys are absent from the Unity project and build.

## Current boundary

Identity and member registration are live. Attendance, activity changes,
heartbeat, and team events still use `ProfiledMockTeamBackend`. The next stage is
an `ITeamBackend` implementation backed by the deployed RPCs and Realtime tables.

## Verification

- Local Supabase REST probe verified anonymous signup and the exact single-object
  response shape of `claim_member_name`; the probe user was removed by DB reset.
- Unity EditMode tests: **22 passed, 0 failed**.
- Tests cover anonymous bootstrap, session refresh/rotation, invalid refresh
  recovery, authenticated member claim, and real Windows Credential Manager
  save/load/delete.
- Windows release build with the integration: **Success**.
- No disposable test user or member was created in the hosted project. The first
  real app launch is intentionally reserved for the user's chosen name.

Evidence is stored under ignored `Logs/DOTORIONValidation`:

- `editmode-m3-secure-store-results.xml`
- `editmode-m3-secure-store.log`
- `build-m3-supabase.log`
