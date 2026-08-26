손으로 편집하는 UI 프리팹. 화면은 런타임 코드로 생성하지 않는다.

`Resources/`에 번호가 없는 이유는 Resources.Load가 폴더 이름 자체에 의존하기 때문이다. 앱 진입점인 TeamOverlayApp 프리팹만 그 안에 두고, 나머지는 이 폴더에 평평하게 둔다.
`Team Overlay > Rebuild Editable UI Prefabs...`는 이 폴더를 초기 레이아웃으로 되돌리므로 수동 수정이 사라진다. 편집 방법은 `Docs/PREFAB_UI_EDITING.md`를 본다.
