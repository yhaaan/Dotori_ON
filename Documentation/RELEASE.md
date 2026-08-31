# 릴리스 절차

DOTORI ON 을 새 버전으로 내보내는 방법이다. 앞으로 붙을 자동 업데이트 확인과
GitHub Pages 다운로드 페이지가 여기서 만들어진 릴리스를 그대로 읽어 간다.

---

## 0. 처음 한 번만

GitHub CLI 를 설치하고 로그인한다.

```
winget install --id GitHub.cli
gh auth login
```

---

## 1. 버전 올리기

버전의 단일 출처는 `ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` 이다.
Unity 의 Project Settings > Player 에서 고쳐도 되고, 파일을 직접 고쳐도 된다.

**반드시 `0.8.0` 처럼 세 자리로 적는다.** 두 자리로 두면 나중에 업데이트 확인이
버전을 비교할 때 `0.10` 이 `0.7` 보다 낮다고 판정한다. 릴리스 스크립트가 이걸
검사해서 두 자리면 멈춘다.

이 값은 앱 안에서 `Application.version` 으로 읽혀 설정 패널의 버전 행에 그대로
표시된다(`DOTORIONView.cs`).

## 2. 빌드

Unity 메뉴에서 **DOTORI ON > Build Windows x86_64** 를 실행한다.
결과는 `Builds/Windows/` 에 떨어진다.

## 3. 커밋하고 푸시

릴리스는 깨끗한 `main` 에서만 만든다. 스크립트가 브랜치와 작업 트리 상태를 확인한다.

## 4. 릴리스 발행

```
./Tools/release.ps1
```

스크립트가 하는 일은 다음과 같다.

1. `bundleVersion` 을 읽어 태그 이름(`v0.8.0`)을 정한다.
2. 빌드가 마지막 소스 변경보다 오래됐으면 멈춘다 — 버전만 올리고 다시 빌드하지 않은
   채 올리는 사고를 막는다. 의도한 것이면 `-Force` 를 붙인다.
3. `Builds/Windows/` 에서 배포하면 안 되는 것(`*_DoNotShip`, 지난 zip, `구 빌드`)을
   빼고 `Builds/DOTORI_ON.zip` 으로 묶는다. 압축 안의 최상단은 `DOTORI ON/`
   폴더 하나다.
4. zip 의 sha256 을 재서 `version.json` 을 만든다.
5. 태그를 밀고 GitHub Release 를 만들면서 zip 과 `version.json` 을 자산으로 올린다.
   릴리스 노트는 `--generate-notes` 로 커밋 목록에서 자동 생성된다. 직접 쓰려면
   `-Notes "..."`.

zip 내용만 눈으로 확인하고 싶으면 `-DryRun` 을 붙인다. 태그와 릴리스는 건너뛴다.

---

## version.json 은 손으로 고치지 않는다

돌고 있는 앱이 시작할 때 읽는 파일이고, 릴리스할 때마다 zip 과 **같은 순간에**
새로 만들어진다. 그래서 둘이 서로 다른 빌드를 가리킬 수가 없다.

```json
{
  "version":  "0.10.0",
  "download": "https://github.com/yhaaan/Dotori_ON/releases/latest/download/DOTORI_ON.zip",
  "sha256":   "받은 zip 을 덮어쓰기 전에 이걸로 검증한다",
  "notes":    "https://github.com/yhaaan/Dotori_ON/releases/latest"
}
```

저장소에 두고 손으로 맞추는 방법도 있었지만 쓰지 않았다. 버전을 올릴 때 한 번만
깜빡해도 앱이 틀린 버전을 보고, 그 사실을 아무도 모른 채 배포되기 때문이다.

앱 쪽에서 이 파일을 읽고 판단하는 규칙은 `Assets/01. Scripts/06. Update/` 에 있다.
sha256 이 없거나, 우리 릴리스가 아닌 주소를 담고 있으면 업데이트를 하지 않는다.

---

## 자산 파일명은 고정이다

릴리스 자산의 이름은 언제나 `DOTORI_ON.zip` 과 `version.json` 이다. 이 이름이
고정돼 있어야 아래 URL 이 **항상 최신 릴리스**를 가리킨다.

```
https://github.com/yhaaan/Dotori_ON/releases/latest/download/DOTORI_ON.zip
https://github.com/yhaaan/Dotori_ON/releases/latest/download/version.json
```

다운로드 페이지의 버튼과 자동 업데이트가 이 URL 들을 쓴다. 이름을 바꾸면 이미
배포된 구버전은 업데이트 경로가 끊겨서 영원히 그 버전에 남는다.

---

## 중간에 실패했을 때

태그를 민 뒤 `gh release create` 에서 실패하면 태그만 원격에 남는다.
원인을 고친 뒤 릴리스만 다시 만든다.

```
gh release create v0.8.0 "Builds/DOTORI_ON.zip" --title "DOTORI ON v0.8.0" --generate-notes --latest
```

태그부터 다시 하려면 원격과 로컬에서 지운다.

```
git push origin :refs/tags/v0.8.0
git tag -d v0.8.0
```

---

## 알아 둘 것 — SmartScreen

빌드에 코드 서명을 하지 않았기 때문에, 받은 사람이 처음 실행할 때 Windows 가
"알 수 없는 게시자" 경고를 띄운다. 서명 인증서를 사기 전까지는 기술로 피할 수 없고,
다운로드 페이지에 "추가 정보 > 실행" 안내를 적어 두는 것으로 대신한다.
