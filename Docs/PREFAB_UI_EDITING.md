# Unity에서 Team Overlay UI 조정하기

이제 화면은 런타임 코드로 생성되지 않고 아래 네 개의 실제 프리팹을 사용합니다.

- `Assets/02. Prefabs/TeamOverlayCanvas.prefab`: 메인 화면 전체
- `Assets/02. Prefabs/TeamMemberCard.prefab`: 메인 화면에 중첩된 멤버 카드 원본
- `Assets/02. Prefabs/FirstRunNameModal.prefab`: 최초 실행 이름 설정 화면
- `Assets/Resources/TeamOverlay/TeamOverlayApp.prefab`: 앱 시작점과 위 프리팹 참조

## 권장 편집 순서

1. Unity Project 창에서 `TeamOverlayCanvas.prefab`을 더블 클릭합니다.
2. Prefab Mode에서 위치, 크기, 색상, 글자 크기와 버튼 모양을 조정합니다.
3. 네 카드에 공통으로 적용할 디자인은 `TeamMemberCard.prefab`에서 수정합니다.
4. 최초 이름 입력 화면은 `FirstRunNameModal.prefab`에서 수정합니다.
5. Play 버튼으로 480x220 화면과 버튼 동작을 확인합니다.
6. `Team Overlay > Build Windows x86_64`로 빌드합니다.

`Team Overlay > Create Missing Editable UI Prefabs`는 파일이 없을 때만 생성하며 기존 수정 내용을 덮어쓰지 않습니다.

`Team Overlay > Rebuild Editable UI Prefabs...`는 네 프리팹을 초기 레이아웃으로 다시 만들기 때문에 수동 수정 내용이 사라집니다. 초기화가 필요한 경우에만 사용하세요.

## 주의할 참조

각 프리팹 루트 컴포넌트의 `Prefab references` 필드는 버튼 동작과 데이터 바인딩에 사용됩니다. 계층 안의 오브젝트를 삭제하거나 교체했다면 대응하는 필드를 새 오브젝트로 다시 연결하세요. 단순한 위치, 크기, 색상, 텍스트 스타일 수정에는 참조를 바꿀 필요가 없습니다.

메인 화면 우측 상단은 `이름변경` · `TOP` · `—` · `×` 네 개입니다. `—`는 작업 표시줄로 최소화하고, `×`는 퇴근 처리를 시도한 뒤 앱을 완전히 종료합니다. Alt+F4도 `×`와 같은 경로로 갑니다.

시스템 트레이로 숨기는 `숨김` 버튼은 제거했습니다. 트레이 아이콘이 Windows 11의 오버플로 영역에 가려지면 창을 되찾을 방법이 없고, `forceSingleInstance` 때문에 재실행까지 막혀 작업 관리자로 프로세스를 죽여야 했습니다. 최소화는 항상 작업 표시줄에 남으므로 같은 용도를 안전하게 대신합니다.

## 상태 메모 입력칸

하단 컨트롤 바 오른쪽의 `StatusNoteInput`(`TeamOverlayView._statusNoteInput`)은 지금 뭘 하는지 24자까지 적는 칸입니다. 출근 중일 때만 보이고, Enter를 누르거나 포커스를 잃으면 저장됩니다. 서버가 퇴근 시 자동으로 비웁니다.

글자 수 제한을 늘리려면 InputField의 `Character Limit`만이 아니라 `member_current_state.status_note`의 CHECK 제약도 같이 고쳐야 합니다. 카드 폭이 약 110px라 그 이상은 어차피 잘립니다.

## 이름변경(재로그인) 버튼

상단 바의 `이름변경`(`TopBar/SwitchAccount`, `TeamOverlayView._switchAccountButton`)은 이름을 고치는 기능이 아니라 로그아웃 후 최초 이름 입력 화면으로 돌아가는 재로그인 버튼입니다. 눌리면 출근 중이던 세션을 퇴근 처리하고, 이 PC에 저장된 "현재 로그인한 이름" 표시만 지운 뒤 `FirstRunNameModal`을 다시 띄웁니다.

이름이 곧 멤버 ID이므로, 같은 이름으로 다시 로그인하면 이전 멤버로 이어지고 다른 이름을 입력하면 새 멤버가 됩니다. 이름의 정규화 키에서 Auth 이메일과 비밀번호를 유도하기 때문에 "같은 이름 → 기존 통계 누적"은 어느 PC에서나 성립합니다. Windows 자격 증명 관리자에 이름별로 저장되는 세션(`ProjectDDD.TeamOverlay.SupabaseAuth.<project-ref>.<이름 키>`)은 로그인 왕복을 한 번 아끼는 캐시일 뿐이며, 지워져도 이름만 다시 입력하면 복구됩니다.

이는 보안 경계가 아닙니다. 앱을 가진 사람은 누구든 팀원 이름으로 접속할 수 있으며, 4인 내부 도구라는 전제에서 내린 선택입니다.

## 통계 패널

`통계` 버튼을 누르면 `StatisticsPanel`이 열리고 창이 패널 높이(424px)만큼 늘어납니다. 패널 자체의 높이와 `WindowsOverlayWindow.StatisticsPanelHeight`는 같은 값이어야 하며, 어긋나면 패널 아래가 잘리거나 빈 공간이 생깁니다. `PrefabAssetTests`가 두 값을 함께 고정합니다.

패널 상단에는 탭 두 개(`내 통계`, `랭킹`)와 기간 버튼 세 개(`7일`, `이번 달`, `누적`)가 있습니다. 기간을 바꾸면 서버에 다시 요청하고, 기간에 따라 한 줄이 하루·한 주·한 달이 됩니다. `랭킹` 탭 위쪽의 지표 버튼 네 개(`작업`, `총시간`, `휴식`, `식사`)는 이미 받아 둔 값을 다시 정렬만 하므로 요청이 없습니다.

행 개수는 프리팹에 고정돼 있습니다(내 통계 7행, 랭킹 4행). 행을 늘리려면 `TeamOverlayPrefabBuilder`의 배열 크기와 위치 계산, 패널 높이, 그리고 위 두 상수를 함께 고쳐야 합니다.
