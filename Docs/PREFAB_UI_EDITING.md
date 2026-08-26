# Unity에서 Team Overlay UI 조정하기

이제 화면은 런타임 코드로 생성되지 않고 아래 네 개의 실제 프리팹을 사용합니다.

- `Assets/02. Prefabs/TeamOverlayCanvas.prefab`: 메인 화면 전체
- `Assets/02. Prefabs/TeamMemberCard.prefab`: 메인 화면에 중첩된 멤버 카드 원본
- `Assets/02. Prefabs/FirstRunNameModal.prefab`: 최초 실행 이름 설정 화면
- `Assets/02. Prefabs/Resources/TeamOverlay/TeamOverlayApp.prefab`: 앱 시작점과 위 프리팹 참조

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

메인 화면 우측 상단의 `숨김`은 시스템 트레이로 보내므로 프로세스가 유지됩니다. `×`는 퇴근 처리를 시도한 뒤 앱을 완전히 종료합니다.

## 이름변경(재로그인) 버튼

상단 바의 `이름변경`(`TopBar/SwitchAccount`, `TeamOverlayView._switchAccountButton`)은 이름을 고치는 기능이 아니라 로그아웃 후 최초 이름 입력 화면으로 돌아가는 재로그인 버튼입니다. 눌리면 출근 중이던 세션을 퇴근 처리하고, 이 PC에 저장된 "현재 로그인한 이름" 표시만 지운 뒤 `FirstRunNameModal`을 다시 띄웁니다.

이름이 곧 멤버 ID이므로, 같은 이름으로 다시 로그인하면 이전 멤버로 이어지고 다른 이름을 입력하면 새 멤버가 됩니다. 이를 위해 Supabase Auth 세션은 Windows 자격 증명 관리자에 이름별로 저장됩니다(`ProjectDDD.TeamOverlay.SupabaseAuth.<project-ref>.<이름 키>`). 즉 "같은 이름 → 기존 통계 누적"은 그 이름으로 로그인한 적이 있는 PC에서 성립하며, 다른 PC에서 같은 이름을 입력하면 서버가 `member_name_taken`으로 거절합니다. 다른 PC에서의 이름 인계는 서버 RPC 추가가 필요합니다.
