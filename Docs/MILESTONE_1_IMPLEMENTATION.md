# 1차 마일스톤 구현 기록

기준 문서: `TEAM_OVERLAY_PROJECT_HANDOFF.md`

## 구현 범위

- Windows 480x220 무테 오버레이와 실제 Always-on-top 토글
- 고정 순서 4인 카드와 오프라인/온라인 표시
- 로컬 사용자 출근, 퇴근, 근무/휴식/식사 상태 변경
- 출근 이후 로컬 경과 시간 표시
- 오프라인 사용자의 마지막 퇴근 시각 표시
- 서버 없이 전체 흐름을 실행하는 `MockTeamBackend`
- 가짜 팀원 출근 이벤트와 이벤트당 1회 알림음
- 창 드래그, 최소화, 닫기 시 트레이 숨김, 트레이 복원
- 트레이 메뉴의 표시 및 `퇴근 후 종료`

## 코드 구조

- `Assets/_TeamOverlay/Scripts/Core`: 상태 모델, 이벤트, 백엔드 계약
- `Assets/_TeamOverlay/Scripts/Backend/Mock`: 메모리 기반 Mock 백엔드와 테스트 제어 계약
- `Assets/_TeamOverlay/Scripts/UI`: 런타임 uGUI 구성과 사용자 상호작용
- `Assets/_TeamOverlay/Scripts/Audio`: 런타임 생성 알림음
- `Assets/_TeamOverlay/Scripts/Platform/Windows`: Win32 창/트레이 통합
- `Assets/_TeamOverlay/Tests/EditMode`: Mock 백엔드 EditMode 테스트
- `Assets/_TeamOverlay/Editor`: 프로젝트 구성 및 Windows x86_64 빌드 메뉴

## 실행과 빌드

Unity에서 다음 메뉴를 실행한다.

1. 필요하면 `Team Overlay > Configure Project`
2. `Team Overlay > Build Windows x86_64`
3. `Builds/Windows/TeamOverlay.exe` 실행

UI는 에디터 Play 모드에서도 확인할 수 있다. Always-on-top, 무테, 트레이 등 Win32 통합은 Windows Standalone 플레이어에서만 활성화된다.

## 검증 결과

- Unity EditMode 테스트: 8개 모두 통과
- Windows x86_64 플레이어 빌드 성공
- 실제 창 크기 480x220, 무테 및 Topmost 확장 스타일 확인
- TOP 버튼으로 Topmost 해제/재설정 확인
- 드래그에 따른 창 위치 변경 확인
- 닫기 메시지 후 프로세스 유지 및 트레이 숨김, 트레이 콜백 복원 확인
- 출근 후 경과 시간 증가, 휴식 상태 즉시 반영 확인
- 가짜 팀원 출근 이벤트가 카드에 즉시 반영되는 흐름 확인

## 다음 마일스톤 경계

현재 백엔드는 의도적으로 메모리 기반 Mock 구현이다. Supabase 연동, 인증, Realtime 동기화, 서버 기준 heartbeat/timeout, 실제 업무 데이터 저장은 다음 마일스톤 범위다.
