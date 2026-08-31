손으로 편집하는 UI 프리팹. 화면은 런타임 코드로 생성하지 않는다.

메인 화면, 멤버 카드, 최초 이름 입력 프리팹이 여기 있다. 앱 진입점인 DOTORIONApp 프리팹만 `Assets/Resources/DOTORION`에 있는데, Resources.Load가 `Resources`라는 폴더 이름 자체에 의존해서 번호를 붙이지 못하기 때문이다.
`DOTORI ON > Rebuild Editable UI Prefabs...`는 이 폴더를 초기 레이아웃으로 되돌리므로 수동 수정이 사라진다. 편집 방법은 `Documentation/PREFAB_UI_EDITING.md`를 본다.
