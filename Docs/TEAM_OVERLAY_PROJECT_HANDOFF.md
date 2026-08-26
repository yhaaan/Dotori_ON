# 팀 오버레이 앱 — 프로젝트 인수인계 문서

이 문서는 새 Codex 프로젝트/채팅에서 지금까지의 논의를 그대로 이어가기 위한 기준 문서다. 새 채팅에서는 이 파일을 먼저 읽고, 아래 결정과 범위를 유지한 채 개발을 진행한다.

## 새 채팅에서 바로 사용할 요청문

```text
이 저장소는 4인 팀 내부 커뮤니케이션용 Windows 데스크톱 오버레이 앱 프로젝트입니다.
먼저 TEAM_OVERLAY_PROJECT_HANDOFF.md 전체를 읽고, 기존 결정과 MVP 범위를 유지해주세요.

우선 할 일:
1. 현재 Unity 프로젝트 구조와 설정을 점검합니다.
2. 아직 프로젝트가 비어 있다면 Unity LTS의 2D 프로젝트를 기준으로 기본 폴더 구조를 만듭니다.
3. 서버 연결보다 먼저 MockTeamBackend와 가짜 팀원 4명으로 세로 슬라이스를 만듭니다.
4. 출근/퇴근, 상태 변경, 4명 카드, 출근 경과 타이머, 마지막 퇴근 시각이 동작하게 합니다.
5. Windows 빌드에서 작은 창·항상 위·창 드래그·트레이 최소화를 검증합니다.
6. 기존 사용자 파일과 변경사항은 보존하고, 구현 전 현재 상태를 짧게 보고해주세요.

첫 단계가 끝나면 실행 방법, 구현한 내용, 남은 위험 요소를 정리해주세요.
```

## 1. 제품 목표

팀원 4명이 사용하는 아주 작은 Windows 데스크톱 오버레이 앱을 만든다. 앱을 화면 위에 항상 띄워 두고, 누가 출근해 있는지와 현재 상태, 출근 후 경과 시간을 한눈에 확인하는 것이 핵심이다.

이 앱은 급여·근태 정산 시스템이 아니라, 원격 또는 소규모 팀의 가벼운 존재감과 커뮤니케이션을 위한 도구다. 다만 작업·식사·휴식 이력은 통계로 볼 수 있도록 정확한 데이터 모델을 사용한다.

## 2. MVP 필수 기능

- 고정된 팀원 4명 표시
- 출근 버튼
- 출근 상태에서 퇴근 버튼
- 현재 상태 변경: `작업중`, `쉬는중`, `식사중`
- 퇴근 상태: `오프라인`
- 현재 일하는 팀원과 각 팀원의 출근 시각 표시
- 캐릭터/아이콘 머리 위에 출근 후 경과 타이머 표시
- 팀원이 출근하면 다른 온라인 팀원에게 알림음 재생
- 오프라인 팀원의 마지막 퇴근 시각 표시
- 정상 종료 또는 컴퓨터 종료 시 자동 퇴근 시도
- 비정상 종료와 네트워크 단절을 처리하는 heartbeat 기반 자동 오프라인
- 작고 테두리 없는 창, 항상 위(Always on Top), 드래그 이동
- X 버튼을 눌렀을 때 완전 종료보다는 시스템 트레이로 숨기는 동작을 기본값으로 고려

## 3. 이후 확장 기능

- 특정 팀원 호출하기 및 알림 보내기
- 감정표현/이모트
- 간단한 메시지 또는 상태 메모
- 날짜별 작업시간 통계
- 일간 작업시간 1위
- 누적 작업시간 랭킹
- 식사시간·휴식시간 랭킹
- 주간/월간 통계 및 개인 추이

호출, 감정표현, 채팅은 MVP가 안정된 뒤 `team_events` 이벤트 모델 위에 추가한다.

## 4. 화면 방향

사용자가 그린 스케치의 핵심은 가로로 긴 작은 창 안에 팀원 4명의 실루엣 또는 캐릭터가 나란히 있는 형태다.

권장 초기 크기: 약 `480 × 220` 논리 픽셀. 실제 크기는 캐릭터와 글자 가독성에 맞춰 조절한다.

각 팀원 카드에는 다음을 표시한다.

- 이름
- 캐릭터 또는 아이콘
- 현재 상태와 색상
- 온라인이면 출근 후 경과 시간
- 필요하면 작은 글씨로 출근 시각
- 오프라인이면 마지막 퇴근 시각

상단 바에는 다음을 둔다.

- 창 드래그 영역
- 항상 위 고정 토글
- 최소화/트레이 버튼
- 닫기 버튼

첫 버전은 불투명하거나 반투명한 일반 카드 창으로 만든다. 완전 투명 배경, 마우스 클릭 통과, 캐릭터만 떠 있는 형태는 Windows 네이티브 창 처리 난도가 높으므로 후속 기능으로 미룬다.

## 5. 상태 모델

하나의 `status` 값에 모든 의미를 섞지 않는다. 다음 세 축을 분리한다.

### 출퇴근 상태

- `clocked_in`
- `clocked_out`

### 활동 상태

- `working`
- `break`
- `meal`

### 연결 상태

- `connected`
- `degraded`
- `disconnected`

`오프라인`은 기본적으로 퇴근 상태를 의미한다. 단순 네트워크 단절은 바로 퇴근으로 확정하지 않고, 유예 시간 동안 연결 끊김으로 표시한 뒤 heartbeat 만료 시 자동 퇴근 처리한다.

## 6. 타이머 정의

MVP에서 캐릭터 머리 위의 주 타이머는 `출근 후 경과 시간`이다.

- 서버에는 `checked_in_at`을 UTC로 저장한다.
- 클라이언트는 서버 시각과 `checked_in_at`의 차이로 매초 화면만 갱신한다.
- 매초 DB에 시간을 저장하거나 전송하지 않는다.
- 상태가 바뀔 때만 활동 구간을 닫고 새 구간을 연다.

통계에서는 다음 시간을 서로 구분한다.

- `attendance_seconds`: 출근부터 퇴근까지 전체 시간
- `work_seconds`: `working` 구간의 합
- `break_seconds`: `break` 구간의 합
- `meal_seconds`: `meal` 구간의 합

따라서 화면의 출근 타이머와 실제 작업시간 통계는 다른 값일 수 있다.

## 7. 앱 종료와 heartbeat

heartbeat는 “컴퓨터가 켜져 있다”는 일반 신호가 아니라, **출근 처리된 앱이 아직 실행 중이고 서버와 통신하고 있다는 신호**다.

정상 종료 이벤트만으로는 충분하지 않다. 프로세스 강제 종료, 충돌, 정전, 인터넷 단절, OS의 종료 제한 시간 등에서는 종료 콜백이 실행되지 않거나 서버 요청이 완료되지 않을 수 있다. 따라서 두 방식을 함께 사용한다.

1. 정상 종료 경로
   - 사용자가 퇴근 버튼을 누르면 즉시 서버 퇴근 처리
   - 앱 종료 및 Windows 종료 이벤트에서 최선의 자동 퇴근 요청
   - 성공 시 정확한 퇴근 시각과 종료 사유 기록

2. 비정상 종료 보완
   - 출근 중에만 30~60초마다 heartbeat 전송
   - 마지막 heartbeat 이후 약 2~3분의 유예 시간이 지나면 서버가 자동 퇴근 처리
   - 이 경우 `auto_timeout` 같은 사유와 추정 시각을 기록

Realtime/WebSocket 연결은 빠른 화면 갱신용이고, heartbeat와 서버 정리 작업은 데이터의 최종 일관성을 위한 것이다.

절전 모드의 처리 방식은 구현 전에 결정한다. 초기 권장안은 짧은 절전은 유예하고, heartbeat 만료가 유예 시간을 넘으면 자동 퇴근시키는 것이다.

## 8. 권장 백엔드

현재 권장안은 **Supabase**다.

이유:

- PostgreSQL 기반이라 날짜별·누적·랭킹 통계 쿼리가 자연스럽다.
- Auth, Realtime, Row Level Security, RPC/함수, 예약 작업을 한 서비스에서 구성할 수 있다.
- 팀원 4명의 초기 사용량은 매우 작아서 무료 플랜으로 MVP를 시작하기 적합하다.
- 향후 웹 통계 화면이나 관리 기능을 붙이기 쉽다.

주의사항:

- Unity용 Supabase C# SDK는 핵심 공식 SDK만큼 지원이 강하지 않을 수 있으므로, 앱 전체가 SDK에 직접 의존하지 않게 한다.
- `ITeamBackend` 인터페이스 뒤에 구현을 감추고, 필요하면 REST/RPC와 Realtime WebSocket을 직접 사용한다.
- 클라이언트 빌드에 `service_role` 키를 절대 넣지 않는다.
- 사용자에게는 공개용 anon 키만 사용하고, RLS로 같은 팀 데이터에만 접근하게 한다.
- 서버 기준 시각을 사용하고 모든 원본 시각은 UTC로 저장한다.
- 무료 플랜의 휴면·백업·제한 정책은 실제 배포 직전에 최신 공식 가격표로 다시 확인한다.

`뒤끝(BACKND)`도 Unity 공식 SDK와 운영 편의성이 있어 구현은 가능하다. 하지만 작업·식사·휴식 구간을 조합한 다양한 통계는 관계형 SQL이 더 편하고 확장성이 좋다. 따라서 이 프로젝트에서는 Supabase가 우선이며, 뒤끝은 Unity SDK 편의성이 통계 유연성보다 더 중요해질 때 대안으로 검토한다.

## 9. 권장 데이터베이스 구조

### `teams`

- `id`
- `name`
- `timezone` — 기본 `Asia/Seoul`
- `created_at`

### `members`

- `id` — Auth 사용자 ID와 연결
- `team_id`
- `display_name`
- `avatar_key`
- `sort_order`
- `is_active`

### `attendance_sessions`

- `id`
- `team_id`
- `member_id`
- `checked_in_at`
- `checked_out_at` — 출근 중이면 null
- `checkout_reason` — `manual`, `app_exit`, `os_shutdown`, `auto_timeout`, `admin`
- `last_heartbeat_at`
- `client_instance_id`

### `activity_intervals`

- `id`
- `attendance_session_id`
- `member_id`
- `status` — `working`, `break`, `meal`
- `started_at`
- `ended_at`

### `member_current_state`

빠른 현재 화면 조회를 위한 테이블 또는 뷰다.

- `member_id`
- `attendance_session_id`
- `attendance_status`
- `activity_status`
- `checked_in_at`
- `status_started_at`
- `last_heartbeat_at`
- `last_checked_out_at`
- `updated_at`

### `team_events`

출근 알림, 호출, 감정표현 같은 실시간 이벤트를 기록한다.

- `id`
- `team_id`
- `actor_member_id`
- `target_member_id` — 전체 대상이면 null
- `event_type`
- `payload` — JSONB
- `created_at`

### `member_daily_stats` (선택)

처음에는 SQL 뷰/쿼리로 계산하고, 데이터가 늘거나 화면이 느려지면 일별 집계 테이블을 추가한다.

- `member_id`
- `local_date`
- `attendance_seconds`
- `work_seconds`
- `break_seconds`
- `meal_seconds`
- `updated_at`

## 10. 서버 동작과 RPC

중요 변경은 여러 쿼리를 클라이언트에서 순서대로 실행하지 않고, 트랜잭션을 보장하는 RPC/서버 함수로 만든다.

- `check_in(member_id, client_instance_id)`
  - 열린 출근 세션 중복 방지
  - 출근 세션 생성
  - 기본 `working` 활동 구간 생성
  - 현재 상태 갱신
  - 출근 이벤트 생성

- `change_activity(member_id, new_status)`
  - 기존 활동 구간 종료
  - 새 활동 구간 시작
  - 현재 상태 갱신

- `check_out(member_id, reason)`
  - 현재 활동 구간 종료
  - 출근 세션 종료
  - 현재 상태 갱신
  - 퇴근 이벤트 생성

- `heartbeat(member_id, attendance_session_id, client_instance_id)`
  - 현재 열린 세션과 기기 인스턴스를 검증
  - 마지막 heartbeat 시각 갱신

서버 측 예약 작업은 만료된 heartbeat를 찾아 자동 퇴근 처리한다. 모든 시각은 클라이언트가 보낸 시각보다 DB 서버 시각을 우선한다.

Realtime 구독 대상은 `member_current_state`와 `team_events` 정도로 제한한다. 타이머 숫자는 로컬에서 계산한다.

## 11. 통계 규칙

- 기준 시간대는 팀 설정인 `Asia/Seoul`을 사용한다.
- DB의 원본 시각은 UTC로 보관한다.
- 자정을 넘긴 활동 구간은 통계 계산 시 한국 시간 기준 날짜별로 나눈다.
- 일간 작업시간 1위는 해당 날짜의 `work_seconds` 합으로 정한다.
- 역대 작업시간은 전체 `work_seconds` 합으로 정한다.
- 식사시간 랭킹은 `meal_seconds` 합으로 정한다.
- 출근 중인 열린 구간은 통계 조회 시 현재 서버 시각까지 임시 계산할 수 있다.
- 자동 퇴근은 유예 시간만큼 과대 계산되지 않도록 마지막 정상 heartbeat 또는 정책상 추정 종료 시각을 사용한다.
- 동률 처리 규칙은 동일 순위 또는 먼저 도달한 사람 중 하나를 제품 정책으로 정한다.

통계가 경쟁을 과도하게 유도할 수 있으므로, UI에는 “오래 켜 둔 시간”과 “집중해서 일한 시간”의 차이를 명확히 표시한다.

## 12. Unity 기술 선택

Unity 개발 경험을 활용하는 선택은 충분히 가능하다. 특히 캐릭터, 애니메이션, 감정표현, 소리, 향후 시각적 확장에 유리하다.

권장 초기 설정:

- Unity의 현재 안정적인 LTS 버전
- 2D Core 템플릿
- Windows x86_64 우선
- Built-in Render Pipeline 또는 가장 단순한 2D 구성
- 개발 중 Mono, 배포 전 IL2CPP 빌드도 검증
- `Application.runInBackground = true`
- `QualitySettings.vSyncCount = 0`
- 약 30 FPS 제한
- 작은 기본 창 크기

Unity의 단점은 일반 데스크톱 UI 프레임워크보다 실행 파일과 메모리 사용량이 크고, 트레이·항상 위·투명 창 같은 기능에 Windows 네이티브 연동이 필요하다는 점이다. 4인 팀 내부 도구이고 사용자가 Unity 개발자이므로 이 비용은 감수할 만하다.

## 13. Unity 코드 구조

```text
Assets/
  00. Scenes/
  01. Scripts/
    00. Core/
    01. Identity/
    02. Supabase/
    03. Backend/
      Mock/
    04. Platform/
      Windows/
    05. Audio/
    07. UI/
  02. Prefabs/
  07. Settings/
  90. Editor/
  99. Tests/
    EditMode/
  Resources/
    TeamOverlay/
```

폴더 번호는 `Docs/FolderNumberingConvention.md`를 따른다. `Resources`만 번호가 없는데, Resources.Load가 `Resources`라는 폴더 이름 자체에 의존하기 때문이다(같은 문서 5.1의 "도구·엔진이 위치를 정하는 폴더" 예외).

핵심 인터페이스 예시:

```csharp
public interface ITeamBackend
{
    Task<IReadOnlyList<MemberState>> GetTeamStateAsync(CancellationToken ct);
    Task CheckInAsync(CancellationToken ct);
    Task ChangeActivityAsync(ActivityStatus status, CancellationToken ct);
    Task CheckOutAsync(CheckoutReason reason, CancellationToken ct);
    Task SendHeartbeatAsync(CancellationToken ct);
    IObservable<TeamEvent> Events { get; }
}
```

UI와 상태 로직은 `ITeamBackend`만 알게 하고, `MockTeamBackend`와 `SupabaseTeamBackend`를 교체할 수 있게 한다. 실제 시그니처는 사용하는 Unity/.NET 버전과 비동기 라이브러리에 맞춰 조정한다.

## 14. Windows 데스크톱 기능

다음 기능은 Unity UI만으로 끝나지 않으므로 Windows 빌드에서 초기에 검증한다.

- 항상 위: `SetWindowPos` 등의 Win32 API를 P/Invoke
- 테두리 없는 창과 위치/크기 유지
- 창 드래그 이동
- 시스템 트레이: `Shell_NotifyIcon` 또는 검증된 네이티브 플러그인
- 정상 Windows 종료 감지: Unity 종료 콜백과 `WM_QUERYENDSESSION`/`WM_ENDSESSION` 계열 메시지를 최선의 방식으로 처리
- X 클릭 시 트레이 숨김, 트레이 메뉴에서 명시적 `퇴근 후 종료`

에디터에서만 확인하지 말고 아주 이른 단계에 Windows 빌드를 만들어 항상 위, 백그라운드 실행, 종료 이벤트를 시험한다.

## 15. 권장 구현 순서

1. Unity 프로젝트 생성 및 기본 설정
2. 팀원·상태·세션 모델 정의
3. `ITeamBackend`와 `MockTeamBackend` 작성
4. 고정 팀원 4명 카드 UI
5. 로컬 출근/퇴근/상태 변경/타이머
6. 가짜 출근 이벤트와 알림음
7. Windows 작은 창, 항상 위, 창 드래그, 트레이 검증
8. Supabase 프로젝트와 DB 마이그레이션
9. Auth, RLS, RPC, Realtime 연결
10. 정상 종료 + heartbeat + 자동 퇴근
11. 활동 구간 및 통계 쿼리
12. 캐릭터·애니메이션·호출·감정표현 확장

서버 연결부터 시작하지 않는다. 먼저 Mock 데이터로 사용감과 Windows 오버레이 기술 위험을 제거한다.

## 16. 첫 번째 마일스톤 완료 조건

- Windows 빌드가 약 480×220 크기의 작은 창으로 열린다.
- 항상 위 토글이 실제 빌드에서 작동한다.
- 팀원 4명이 항상 같은 순서로 보인다.
- 출근 전에는 출근 버튼이 보인다.
- 출근하면 퇴근 및 상태 변경 버튼이 보인다.
- 출근 중 팀원의 경과 타이머가 로컬에서 계속 흐른다.
- 작업중/쉬는중/식사중 표시가 즉시 바뀐다.
- 가짜 팀원 출근 이벤트를 발생시키면 알림음이 한 번 재생된다.
- 오프라인 팀원은 마지막 퇴근 시각을 표시한다.
- 서버 없이 `MockTeamBackend`로 전체 흐름을 테스트할 수 있다.

## 17. 아직 결정할 항목

- 캐릭터 머리 위 주 타이머를 출근 경과 시간으로 확정할지, 실제 작업시간을 함께 표시할지
- heartbeat 주기와 자동 퇴근 유예 시간 — 현재 권장 30~60초 / 약 2~3분
- 절전 모드 진입 시 자동 퇴근 여부
- X 버튼의 기본 동작 — 현재 권장 트레이 숨김
- 한 계정의 여러 PC 동시 출근 허용 여부 — 초기에는 금지 권장
- 본인이 출근하기 전 다른 팀원 상태를 볼 수 있는지 — 현재 요구는 출근 후 연결
- 통계 랭킹을 모든 팀원에게 노출할지, 개인 통계 중심으로 할지

## 18. 초기 비범위

- macOS/Linux 지원
- 완전 투명·클릭 통과 오버레이
- 일반 메신저 수준의 채팅
- 영상/음성 통화
- 급여 정산이나 법적 근태 증빙 수준의 정확성
- 복잡한 조직/권한 관리

## 19. 현재 진행 상태

- 제품 요구사항과 기술 방향만 논의된 상태
- Unity 프로젝트는 아직 생성되지 않은 것으로 가정
- Supabase 프로젝트와 DB도 아직 생성되지 않음
- 코드 구현 없음
- 첨부 스케치는 임시 클립보드 경로일 수 있으므로, 새 프로젝트를 시작할 때 원본 이미지를 저장소의 `Docs` 또는 `Art/References` 폴더에 다시 넣는 것이 좋음

## 20. 새 프로젝트에서의 바로 다음 작업

Unity LTS 2D 프로젝트를 만든 뒤, 이 문서를 저장소의 `Docs/TEAM_OVERLAY_PROJECT_HANDOFF.md` 또는 루트의 `PROJECT_CONTEXT.md`로 복사한다. 새 Codex 채팅이 이 문서를 먼저 읽게 한 다음, **Mock 데이터로 동작하는 첫 번째 마일스톤**부터 구현한다.

프로젝트에 지속적으로 적용할 짧은 규칙은 추후 `AGENTS.md`에 옮긴다. 제품 요구사항과 설계 결정은 이 문서처럼 저장소 안의 문서로 유지한다.
