# Realtime 팀 신호 전환 구현 보고

## 구현 범위

`REALTIME_MIGRATION_REQUEST.md`의 필수 DB·클라이언트 전환을 구현했다. 실제 빌드 및 로컬/실서버 통합 검증은 완료하지 못했으므로 **릴리스 승인 상태는 아니다**.

- private broadcast `team:<team_id>` / Phoenix `realtime:team:<team_id>`, `team_signal` 수신 전용.
- 상태·회원·찌르기 신호만 발행. 정상 하트비트의 시간 컬럼 변경과 동일 값 UPDATE는 제외한다.
- 기존 REST 조회, 스냅샷 비교, 찌르기 커서는 유지한다.
- 가입 확인 후 신호를 0.3초 동안 묶어 재조회하며, 신호가 없어도 300초마다 안전망 조회한다.
- 가입 실패/연결 끊김에는 기존 5/15/60초 적응형 폴링을 사용한다. 가입 세대가 바뀌면 즉시 보정 조회한다.
- 조회 중 건너뛴 신호와 실패한 조회는 재시도 대상으로 남긴다. 이전 로그인 세션의 응답은 다음 세션에 반영하지 않는다.
- Phoenix 하트비트 25초, 개별 응답 제한 60초, 가입 제한 15초, 재연결 1~60초 + 0~20% 지터.
- 60초마다 세션을 확인하고 변경된 토큰을 전송한다. REST와 Realtime의 refresh token 회전을 직렬화한다.
- 분할 수신, 동시 송신 방지, 취소·소켓 정리, 연결/장애 구간별 로그를 구현했다. 인증 값과 원시 연결 예외는 로그에 출력하지 않는다.
- Mock 백엔드는 변경하지 않았으며 Realtime 채널을 만들지 않는다.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `supabase/migrations/202611240001_realtime_team_signals.sql` | 실패 격리 신호 함수, 상태/회원/찌르기 트리거, 팀별 private broadcast SELECT 정책 |
| `supabase/tests/realtime_team_signals.sql` | 로컬 전용 SQL 검증. 두 팀/사용자를 트랜잭션 안에서 생성하고 끝에 롤백. RPC별 신호 수, 정상 하트비트 제외, RLS, 발행 권한 차단, 신호 실패 시 원래 변경 유지 검증 |
| `Assets/01. Scripts/00. Core/ITeamChangeSignals.cs` | Supabase 비의존 신호 인터페이스 |
| `Assets/01. Scripts/00. Core/TeamChangeRefreshScheduler.cs` | 고정 배치 창, 재가입 보정, 미처리 신호 유지 |
| `Assets/01. Scripts/02. Supabase/SupabaseRealtimeChannel.cs` | ClientWebSocket/Phoenix v1 채널 및 수명 관리 |
| `Assets/01. Scripts/02. Supabase/SupabaseIdentityClient.cs` | 공유 세션의 동시 토큰 갱신 보호 |
| `Assets/01. Scripts/02. Supabase/SupabaseTeamBackend.cs` | 기존 하트비트 응답에서 동일 출근 세션의 서버 확인 시간을 보관 |
| `Assets/01. Scripts/07. UI/DOTORIONApp.cs` | 채널 생성·정리, 신호/폴링 전환, 조회 중 신호 보존, 오래된 세션 응답 차단, 일일 출석 확인을 5분 안전망과 분리 |
| `Assets/Tests/Editor/TeamChangeRefreshSchedulerTests.cs` | 배치·재가입·진행 중/실패한 조회·초기화 테스트 |
| `Assets/Tests/Editor/SupabaseRealtimeChannelTests.cs` | 가짜 소켓으로 가입 상관관계·신호 필터·분할 수신·재연결·가입 제한·하트비트 제한·토큰 교체·송신 직렬화·Dispose 테스트 |
| `Assets/Tests/Editor/SupabaseSessionConcurrencyTests.cs` | 동시 갱신 1회 및 대기 취소 테스트 |
| `Assets/Tests/Editor/SupabaseHeartbeatSnapshotTests.cs` | 추가 GET 없이 로컬 하트비트 신선도 유지, 실패·퇴근·다른 출근 세션 차단 테스트 |
| `Documentation/REALTIME_MIGRATION_REPORT.md` | 구현 범위, 검증 제한, 후속 확인 안내 |

### 하트비트 호환 보정의 이유

`SubjugationEvent.CanAdvance()`는 마지막 하트비트가 3분 이내인지를 검사한다. 팀 스냅샷을 5분마다만 받으면서 정상 하트비트 신호를 제외하면, 정상 출근 중에도 토벌이 일시 정지할 수 있다.

이를 막기 위해 **이미 실행하는 45초 하트비트 RPC의 응답**에 있는 서버 시간을 로컬 회원 조회에 적용한다. 회원 ID, 출근 세션 ID, 출근 시각이 일치할 때만 적용하며 더 최신인 스냅샷은 덮어쓰지 않는다. 소켓 연결 상태나 클라이언트 시간을 하트비트 성공으로 간주하지 않는다. 실패한 하트비트와 종료/교체된 출근 세션은 유효 시간을 연장하지 않는다.

하트비트 RPC 주기·요청 내용·서버 동작은 그대로이며, 추가 REST 호출도 없다. `SubjugationEvent`와 라운드 동기화 코드는 수정하지 않았다. 요청 6.4의 선택 최적화는 별도 승인 전까지 미착수다.

## 검증 결과

| 항목 | 결과 |
|---|---|
| 변경 파일·diff 확인 | 실시. 요청 문서와 무관한 사용자 파일, 씬/프리팹/패키지/버전은 수정하지 않음 |
| Unity 컴파일 | 실행 시도했으나 내부 명령이 `spawn unity ENOENT`로 실패. 오류 0으로 확인한 것이 아님 |
| EditMode 테스트 | 스케줄러/하트비트 스냅샷 테스트 실행 시도도 같은 이유로 실패. 추가한 11개 테스트의 통과 여부 미확인 |
| `DOTORION.Runtime.csproj` 편집기 빌드 | 미실행. 등록된 실행 작업이 없음 |
| `DOTORION.Runtime.Player.csproj` 플레이어 빌드 | 미실행. 등록된 실행 작업이 없음 |
| 로컬 Supabase 시작/reset 및 SQL | 미실행. 등록된 실행 작업이 없음 |
| 두 클라이언트 1초 이내 반영 | 미실행 |
| 실제 네트워크 단절/복구 | 미실행 |
| 1시간 이상 토큰 갱신 후 수신 | 미실행 |

`list_tasks`는 빈 목록을 반환했다. 임의 명령 실행은 제공되지 않아 MSBuild/로컬 DB 명령은 실행하지 않았다. 따라서 csproj 임시 편집도 하지 않았다.

## 후속 확인 순서

1. **Unity 에디터에서 프로젝트를 한 번 열어 import 필요.** 새 C# 파일 7개의 `.meta`는 Unity가 생성해야 한다. 수동 작성하지 않았다.
2. Unity 연결을 복구하고 새 테스트 4개 클래스를 좁은 범위부터 실행한다. 기존 `SubjugationTests.AttendanceGateOnlyAcceptsCurrentConnectedWork`도 함께 확인한다.
3. 요청 문서 8절의 MSBuild로 편집기·플레이어 프로젝트를 각각 빌드한다. 새 런타임 파일 아래 3개가 **두 csproj 모두** 실제 Compile 목록에 있는지 확인한다.
   - `Assets\01. Scripts\00. Core\ITeamChangeSignals.cs`
   - `Assets\01. Scripts\00. Core\TeamChangeRefreshScheduler.cs`
   - `Assets\01. Scripts\02. Supabase\SupabaseRealtimeChannel.cs`
   - Unity 생성 목록에 없으면 요청 문서대로 임시 `<Compile Include="..." />`를 추가하고 검증 후 되돌린다. 출력 경로는 저장소 밖 임시 폴더로 한다.
4. 로컬에서만 `npx supabase start` → `npx supabase db reset`을 실행한 뒤 `supabase/tests/realtime_team_signals.sql`을 로컬 DB 소유자로 실행한다. 운영 DB에 이 검증 스크립트를 실행하지 않는다.
5. 로컬 클라이언트 두 개로 활동/출퇴근/메모/아바타/찌르기의 반영 시간을 확인한다. SQL RLS 검증과 별도로 다른 팀 private 토픽의 **실제 WebSocket 가입 거부**를 확인한다.
6. 유휴 5분간 주기 GET이 늘어나지 않는지, 연결 단절 시 5/15/60초 폴링으로 돌아오는지, REST 실패 뒤 재시도하는지, 1시간 이후에도 토큰 갱신과 신호 수신이 유지되는지 확인한다. 장시간 정상 출근 중 토벌 진행 및 네트워크 장애 시 정지도 확인한다.
7. 모든 검증 후 사람이 프로덕션 마이그레이션 적용 및 릴리스를 결정한다. 이번 작업에서는 프로덕션 DB 적용·버전 변경·릴리스·push를 하지 않았다.

## 커밋

- DB: `5efb24d` — `🗃️[Update] 팀 상태 변경에만 Realtime 신호 발행`
- 클라이언트와 이 보고서는 `⚙️[Core] 팀 상태 조회를 Realtime 신호 기반으로 전환` 커밋으로 정리한다. 확정 해시는 최종 응답 및 `git log`에서 확인한다.
- 브랜치는 `main`을 유지한다. 사용자가 제공한 미추적 `Documentation/REALTIME_MIGRATION_REQUEST.md`는 수정하거나 이번 커밋에 포함하지 않는다.
