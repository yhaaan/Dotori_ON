# Milestone 5: statistics by period and by metric

Milestone 4 left the overlay live on Supabase with no history screen. The first
statistics pass added a panel with the last seven days and a work-time ranking.
This milestone finishes the statistics items from the handoff document: weekly
and monthly views, a personal trend, and the break/meal rankings.

## What the panel shows now

The statistics button still expands the same window, but the panel now has two
axes the person can move on:

| Control | Choices | Cost |
| --- | --- | --- |
| Period | 최근 7일 · 이번 달 · 누적 | one request |
| Ranking metric | 작업 · 총시간 · 휴식 · 식사 | free, local re-sort |

`내 통계` lists the person's own buckets, newest first, with a summary line above
them (`합계 작업 · 총 · 평균`). `랭킹` lists every teammate ordered by the selected
metric, with the local member's row highlighted.

## One row is not always one day

A month of daily rows does not fit a 480-wide overlay, so the period decides the
bucket instead of the row count:

| Period | Range | Bucket | Rows |
| --- | --- | --- | --- |
| 최근 7일 | today - 6 … today | day | 7 |
| 이번 달 | 1st … today | week | up to 6 |
| 누적 | first session … today | month | newest 7 |

The bucketing happens in Postgres, not in the client. Days are Asia/Seoul days
while every stored timestamp is UTC, and an interval that crosses local midnight
has to be split; doing that once on the server is the only way the week and month
totals agree with the daily ones. Week buckets are ISO weeks (Monday), and the
first and last bucket of a range are clipped to the requested dates, so a partial
week reports its real span (`08.24~08.27`).

Only the newest seven buckets have a row. The summary line is computed from every
bucket the server returned, so a truncated all-time list still adds up.

## A null start date means "everything on record"

The client cannot know when the team started, and a hardcoded epoch would
silently drop history older than itself. `p_from` is therefore optional in both
statistics RPCs: left out, the server resolves it to the first `checked_in_at` of
the member (or of the team, for the ranking).

`JsonUtility` writes a null string as `""`, which Postgres cannot cast to a date,
so the all-time request bodies leave the field out entirely rather than sending an
empty one. That is what `PeriodStatsFromStartRequest` and `RankingFromStartRequest`
exist for.

## Server functions

`202608270007_statistics_periods_and_metrics.sql` replaces both milestone-4
statistics functions:

| Dropped | Added |
| --- | --- |
| `member_daily_stats(uuid, date, date)` | `member_period_stats(uuid, date, text, date)` |
| `team_work_ranking(date, date)` | `team_activity_ranking(date, date)` |

- `member_period_stats` returns `bucket_start`, `bucket_end` and the four second
  counts. `p_bucket` is `day`, `week` or `month`; anything else is rejected with
  `invalid_bucket`. A day-bucketed range is still capped at 366 days because it is
  one row per day; the coarser buckets only carry a sanity ceiling.
- `team_activity_ranking` returns attendance, work, break and meal seconds for
  every active member, work time first. Carrying all four means switching the
  ranked metric never leaves the client.

Both keep the milestone-4 properties: `security definer` with an empty
`search_path`, `authenticated`-only execute, team-scoped visibility through
`can_access_member` / `current_member_team_id`, and open intervals counted up to
the server clock.

## Client shape

- `StatisticsRange.Resolve(period, today)` is the single place a period becomes
  dates and a bucket. Both requests and every label read from it.
- `TeamStatisticsPanelView` owns the metric and re-sorts the cached ranking
  locally. It raises `PeriodChangeRequested` for the period, because that is the
  only one of the two that needs the server.
- `TeamOverlayApp.LoadStatistics` stamps every load with an incrementing request
  id. A slow answer for a period the person already left is dropped instead of
  landing on top of a newer one.
- The panel is 424px tall and the window grows by exactly that much;
  `PrefabAssetTests` pins both numbers so they cannot drift apart.

## Verification

- Unity EditMode tests: **55 passed, 0 failed** (batch mode, Unity 6000.3.8f1),
  up from the 34 of milestone 4.
- `TeamOverlayCanvas.prefab` was regenerated in batch mode
  (`RebuildMainViewFromCommandLine`), which is also what applies the bar-ratio
  layout tweak that had only reached the builder.
- New EditMode coverage: period resolution for all three periods, the metric
  accessor, request bodies for both RPCs including the omitted `p_from`, parsing
  of bucket dates and of all four ranking metrics, and a prefab-level test that
  clicking `식사` reorders the ranking rows without a request.
- `PrefabAssetTests` passing also confirms the regenerated prefab kept every
  serialized reference, that the panel is 424px, and that its YAML has no
  duplicate or dangling local file ids.
- **Not yet verified**: the migration has not been applied to the hosted project,
  so no statistics request has run against the new functions yet.

Evidence is stored under ignored `Logs/TeamOverlayValidation`:

- `editmode-m5.xml`
- `editmode-m5.log`
- `rebuild-m5.log`

## Current boundary

- `team_events` is still not read. Nudges, emotes and messages will need it.
- Rankings show every teammate's numbers to every teammate, by design; the panel
  keeps `총시간` and `작업` visually distinct so "left it running" is never read as
  "worked".
