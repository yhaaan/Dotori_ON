# 개별 체력 · 통합 패널 · 반복 토글

## 동작

- 관리창은 기존 UI와 같은 제목 14px, 본문/능력치/버튼 12px를 사용한다. 2줄 능력치에 맞춰 행 간격은 44px, 패널 높이는 338px이다.
- 반복 버튼은 두 개의 순환 화살표다. 초록색 ON은 현재 라운드 반복, 회색 OFF는 승리 후 다음 라운드다. 호버 문구와 라운드 선택창 버튼은 `현재 라운드 반복 : ON` 또는 `현재 라운드 반복 : OFF`다. 기존 저장의 autoAdvance 값은 반대로 읽어 기존 진행 설정을 유지한다.
- 출전 멤버는 개별 체력을 가진다. 가장 오른쪽의 생존 멤버부터 적이 1명씩 공격하고 초과 피해는 다른 멤버에게 넘기지 않는다. 체력 0이면 전용 쓰러짐 스프라이트와 빈 체력바로 표시하고 공격하지 않는다. 전멸하면 한 라운드 아래에서 반복 ON으로 다시 시작하며 모두 회복한다.
- 시사는 매초 살아 있는 출전 멤버 전원(자신 포함)을 회복한다. 부활은 하지 않고, 공격 멤버가 모두 쓰러지면 회복하지 않는다. 레벨업/교체는 기존 멤버의 체력을 회복하지 않는다. 신규 고용 멤버는 자신의 최대체력으로 시작한다.
- 일반 모드에서는 토벌 스트립을 메인 Unity Canvas에 포함해 하나의 Windows 창으로 렌더링한다. 메인 윗공간 64px를 확보하며 Win32 색상 키로 바탕을 투명하게 한다. 기본 패널은 독립 창을 생성하지 않으며, 메인 드래그 중 같은 창의 픽셀로 함께 이동한다.
- 소형 모드에서는 메인 위 스트립 공간을 제거하고 소형 창과 독립적으로 드래그할 수 있는 알파 창을 표시한다. 관리/라운드 선택을 누르면 일반 모드로 복귀한다. 일반 모드 복귀 시 독립 창을 숨기고 다시 메인 안에 표시한다.
- 개발자 대시보드 단축키는 Ctrl+Shift+D다. `이벤트 코인 +9999`를 누를 때마다 현재 계정에 9,999코인을 더하고 즉시 저장한다.

## 승인된 적 10종

사용자가 후보 확인 후 승인한 순서: 키메라, 그 아이(아노코), 고블린, 데카츠요, 거대한 새, 두부 의태형, 거대 장수풍뎅이, 세이렌, 야만바, 검은 유성. 10라운드마다 같은 순서로 순환하며 능력치는 라운드에 따라 증가한다.

기존 `ChiikawaMonsters-v1.png`의 2종과 신규 `Assets/Resources/DOTORION/Events/Art/ChiikawaEnemies-v2.png`의 8종을 함께 사용한다. 쓰러짐은 `Assets/Resources/DOTORION/Events/Art/ChiikawaDown-v1.png`의 6종이다. 모든 신규 아트는 built-in image_gen으로 제작했다. 기존 얼굴 프로필은 변경하지 않는다.

외형 참고 자료: [공식 마켓](https://chiikawamarket.jp/collections/), [공식 토벌 장면 피규어](https://kitan.jp/products/chiikawa_toubatsu/), [공식 애니메이션 야만바 장면](https://www.anime-chiikawa.jp/images/episodes/307.jpg). 작업용 참고 이미지와 8칸 참조 보드는 Logs/ChiikawaReferences에 보관한다.

## 생성 프롬프트

### 신규 적 8종

```text
Use case: stylized-concept. Transparent PNG production sprite atlas: EXACTLY 8 characters, FOUR columns by TWO rows of equal-size cells. NO words, labels, gridlines, ground, shadows or background. Each subject fully inside its own cell with generous transparent margin, feet/bottom aligned within its cell. Pixel art matching 48px-high Chiikawa sprites: crisp stepped pixels, warm dark brown outline, natural limited colors, simplified canonical shapes. Reference 1 is a numbered 8-subject contact board; its panels 1 through 8 correspond exactly to sprites in reading order. Reference 2 is the established monster sprite pixel art style. Ignore merchandise hardware, humans, surrounding heroes and backgrounds. Top row: (1) Chiikawa Goblin: light green pointed head and ears, simple cute eyes, pink cheeks, ragged tan skirt; (2) Dekatsuyo: large round yellow furry body, WHITE FACE with two striped white horns, dark spiderlike four limbs and fanged smile, NOT a generic bear; (3) Giant bird: WHITE bird creature with long yellow duck bill, small oval black eyes and TWO long black eyestalk-like horns tipped yellow, tall feather body, NO Chiikawa heroes in this cell; (4) Tofu mimic: ivory cube with scored tofu top, tiny sad face, four long soft stretchy ivory limbs. Bottom row: (5) Giant rhinoceros beetle: round BROWN armored body with WHITE humanlike cheeky round face and forked brown horn, six jointed limbs, copy reference; (6) Siren: white round feline face, black forehead hair and ears with red interiors, whiskers, RED scaled mermaid fish tail, cute but menacing, full tail shown; (7) Yamanba: old woman monster from Chiikawa anime panel 7, pale yellow wrinkled face, messy white shoulder-length hair, wide open fanged mouth, muted olive green kimono tied at waist, full body standing or crouching, no other characters; (8) Black shooting star: dark charcoal five-point rounded star with shiny dark eyes and small vertically open oval mouth, only star no chains. Faces slightly toward LEFT where opponents stand, still recognizable front/threequarter view. All 8 must be distinct and faithful to references. Transparent alpha, no baked checkerboard.
```

### 데카츠요 뿔 수정

```text
Use case: precise-object-edit. Edit target is image 1, the transparent 4-column 2-row Chiikawa enemy sprite atlas. Keep its exact dimensions, alpha, pixel style, layout, all silhouettes, faces, colors and all other seven sprites unchanged. Only fix the top row SECOND sprite Dekatsuyo to match reference image 2: it must have exactly TWO striped white horns total, the existing pair attached to the WHITE FACE. REMOVE the extra two white striped horns erroneously attached to the TOP YELLOW FUR body, fill those two small regions with matching yellow fur outline so top body is smoothly round. The two horns on the white face must remain. Do not add or change anything else. Transparent background.
```

### 쓰러짐 6종

```text
Use case: identity-preserve. Create a production sprite sheet of SIX Chiikawa party characters in DEFEATED / KNOCKED DOWN poses, exactly SIX equal horizontal cells in ONE row on true transparent background. Reference 1 provides identity, original faces, color and clean pixel art style for all six; reference 2 overrides Usagi face and distinctive closed omega mouth with short lower curl. Reading order: Chiikawa white round bear, Hachiware white blue cat, Usagi cream yellow long-eared rabbit, Momonga white light blue flying squirrel with large blue tail, Shisa cream orange-maned Okinawan lion, Rakko tan otter white cape forehead scar. Each character lies on their side or slumps flat with closed or dizzy eyes and relaxed limbs to unmistakably indicate unconscious defeat, NOT just sitting resting, no gore. Keep canonical recognizable silhouette and blush. No floating status icons. Keep dropped canonical weapons beside bodies when applicable: pink U sasumata Chiikawa, blue U sasumata Hachiware, yellow staff Usagi, sword Rakko. Momonga and Shisa unarmed. Do NOT add text, labels, background, ground, shadows or gridlines. All six full silhouettes separated by large transparent gaps, same scale, consistent warm brown pixel outlines, limited pastel palette and hard stepped pixels. Preserve Usagi's unique mouth, never give a tongue-out generic cat smile.
```
