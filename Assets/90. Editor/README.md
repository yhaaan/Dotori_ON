에디터 전용 도구. 빌드에 나가지 않는다.

폴더 이름이 `Editor`가 아니어도 되는 이유는 DOTORION.Editor.asmdef가 includePlatforms를 Editor로 고정하고 있기 때문이다. asmdef를 지우면 이 폴더 이름이 다시 의미를 갖게 되니 주의한다.
`DOTORI ON` 메뉴(프로젝트 설정, 프리팹 생성·정리, Windows x86_64 빌드)가 여기 있다. 런타임 코드에서 참조하면 안 된다.
