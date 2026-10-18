# 사용자 승인 픽셀아트 적용

사용자가 토벌대 6명/적 10종 새 픽셀아트 시안을 확인하고 “좋아 진행해”로 적용을 승인했다. 이전 24도트 평균 축소/팔레트 변환은 승인 시안에 적용하지 않는다.

## 사용 파일

- `Assets/Resources/DOTORION/Events/Art/ChiikawaApprovedHeroes-v4.png`: 승인된 토벌대 시트 그대로 복사. 전투 모습과 관리창 얼굴 크롭에 사용한다.
- `Assets/Resources/DOTORION/Events/Art/ChiikawaApprovedEnemies-v4.png`: 승인된 적 10종 시트 그대로 복사.
- `Assets/Resources/DOTORION/Events/Art/ChiikawaApprovedStates-v4.png`: 승인된 토벌대 그림체로 추가 제작한 휴식/식사/쓰러짐 18종.

세 파일 모두 built-in image_gen으로 제작했다. 기존 그림은 보존한다. 원본색을 평균 내거나 별도 팔레트로 치환하지 않고 Point 방식으로 표시용 샘플을 만든다. 투명 외곽의 낮은 알파는 제거하여 메인 창 색상 키 합성에서 번짐을 방지한다. 토벌대·적·상태별 모습은 같은 최대 48px 출력 규격이며 얼굴 프로필은 최대 32px다. 이는 표시 크기이며 승인된 대형 PNG 시트의 전체 해상도와 다르다.

원본 시트의 균등 셀 경계를 넘는 무기/꼬리/귀 때문에 스프라이트 영역과 얼굴 영역을 명시적으로 기록한다. 전투/휴식/식사/쓰러짐/프로필 모두 새 시트에서 불러온다. 기존 전투 규칙, 상태 연동과 공격 모션은 유지한다.

## 생성 프롬프트

### 승인 토벌대

```text
Fresh ORIGINAL PIXEL SPRITE DESIGN, not a filter or a traced/downsampled version of the references. Make an approval concept sheet of SIX low-resolution Chiikawa game sprites, THREE COLUMNS x TWO ROWS, transparent background, each in a roomy square cell. Target 24-32-pixel-era handheld game character art. Extremely economical intentional pixel clusters, LARGE CRISP SQUARE PIXELS, 1 logical pixel dark outline, eyes only TWO DARK PIXELS EACH, mouth only THREE TO FIVE PIXELS. No oversized glossy anime eyes, no detailed tiny lines, no smooth curves. Around 25 discrete pixel steps from feet to top of head; Usagi ears may reach 32. Show those logical pixels enlarged about 12x on the preview sheet. Solid flat colors, at most one shadow color per body, NO antialias, gradient, texture, dithering, blur, mosaic or soft outlines. Design all features FOR the small canvas; don't preserve detailed illustration lines. Reference images are character IDENTITY references only, deliberately use far fewer, larger, cleaner pixel clusters than the references. Top row: Chiikawa white bear carrying pink U sasumata; Hachiware white and blue cat carrying blue U sasumata; Usagi cream-yellow long-eared rabbit carrying yellow staff with closed omega mouth and short lower curl from second reference. Bottom row: Momonga white/blue flying squirrel with big blue tail and blue nose, unarmed; Shisa cream and orange-maned Okinawan lion healer, unarmed; Rakko tan otter with forehead scar, white cape and sword. 3/4 right-facing standing idle battle poses, canonical silhouettes, warm cute original Chiikawa proportions. All six same pixel scale. Full body and weapons fully visible within each cell, generous clear separation. No labels, no UI, no ground or shadows. The goal is to look like a real tiny professionally authored GAME SPRITE, NOT an illustration whose edges happen to be stair-stepped.
```

### 승인 적 10종

```text
Fresh ORIGINAL low-resolution PIXEL SPRITE DESIGN approval sheet, TEN Chiikawa-world monsters, FIVE columns x TWO rows, true transparent background. Use the reference images ONLY to recognize the subjects; redraw them completely in a tiny HANDHELD GAME SPRITE visual language, not a resized reference. Each full sprite only 24-32 logical pixels in its longest dimension, shown with BIG crisp square pixels. Economical deliberately placed clusters. ONE native pixel dark outline, eyes 1-2 pixels each, mouth 2-4 pixels. FLAT COLOR BLOCKS, ONE shadow color maximum per material. No gradients, antialiasing, noise, dithering, blurry averaged color or tiny detailed illustration contours. Simplify secondary details while keeping each silhouette canonical. SAME pixel grid and pixel density for ALL ten sprites, generous space between sprites, each entirely inside its own cell. Reading order: top row 1 PINK CHIMERA, small dark ears, whiskers, tiny white wings, white belly and thin arrow tail; 2 ANOKO, WHITE furry round creature with two small rounded ears, ONE ORANGE TEARDROP HORN ON FOREHEAD, simple big round eyes, claws and long striped tapering tail, ABSOLUTELY NO BIRD FEATHERS, NO EYESTALKS, NO YELLOW BEAK; 3 pale GREEN GOBLIN pointed head and ears, ragged tan skirt; 4 DEKATSUYO, yellow furry round body and white face, exactly TWO striped small horns on white face, four dark spider legs, simple fanged grin; 5 GIANT BIRD white tall feather body, long yellow duck bill, two tall BLACK STALKS with YELLOW ball tips above small face. Bottom row 6 TOFU MIMIC, ivory cube with score lines and tiny face, four long floppy limbs; 7 GIANT BEETLE, brown round armored segmented body with WHITE ROUND humanlike cheeky face and forked brown horn, six legs; 8 SIREN white feline face with black forehead and red inner ears, RED MERMAID TAIL; 9 YAMANBA wrinkled yellow old woman's face, messy white hair and muted olive kimono; 10 BLACK SHOOTING STAR, charcoal rounded 5-point star, two dots for eyes and small vertical mouth. Reference 1 contains Chimera and ANOKO. Reference 2 contains the remaining eight in sequence Goblin Dekatsuyo Bird Tofu / Beetle Siren Yamanba Star. Make Anoko clearly distinct from the bird. No heroes, labels, lettering, gridlines, backgrounds, ground or shadows. New small-sprite design with uncluttered facial features, not a pixelation effect.
```

### 상태별 모습

```text
Use case: identity-preserve. Create THREE STATE VARIANTS of the SIX approved Chiikawa pixel characters from reference 1. Keep their approved faces, silhouettes, palettes, outline thickness and deliberately placed hard pixel shapes. Reference 2 overrides Usagi's distinctive closed omega mouth plus short lower curl. Do not add gradient, blur, soft shading, texture, glow, checkerboard, lettering, labels, UI, ground or shadows. Genuine transparent background.
Sprite atlas with exactly SIX equal columns and THREE equal rows, generous transparent gutters. All 18 full-body sprites fully inside cells, consistent scale and feet baseline within each row. Columns in order: Chiikawa white bear; Hachiware white/blue cat; Usagi cream long-eared rabbit; Momonga white/blue flying squirrel with large blue tail and blue nose; Shisa cream/orange maned lion healer; Rakko tan otter with forehead scar and white cape.
Row 1 REST: peacefully seated, eyes closed, paws relaxed, no weapons held. Usagi's mouth is closed omega with short lower curl; not a generic cat mouth.
Row 2 MEAL: seated happily eating; Chiikawa Hachiware Usagi Shisa Rakko hold rice balls, Momonga holds a small bowl with chopsticks. Show same identities, no text, no weapons.
Row 3 DEFEAT: lying on side with limbs relaxed and eyes closed or X-shaped, unmistakably knocked down; not simply sitting resting. Dropped canonical weapon beside Chiikawa pink U sasumata, Hachiware blue U sasumata, Usagi yellow staff, Rakko sword. Momonga and Shisa unarmed. No gore. Preserve same pixel-art face design and Usagi mouth.
Use LARGE deliberate crisp pixel clusters with 1-native-pixel outlines and at most one shadow per color. Do not introduce smooth illustration gradients. The original approved combat sheet is not an edit target; output only these 18 new state sprites.
```

