# 치이카와 이벤트 캐릭터 아트 v2

2026-10-18, built-in ImageGen 사용. 공식 원작 외형을 참고해 새로 생성한 도트 그림이며 공식 제공 게임 에셋이 아니다.

## 현재 적용 방식

후속 요청으로 도트 표현을 다시 적용했다. 현재 파일과 우사기 입 모양 교정 내용은 [ChiikawaPixelV3.md](ChiikawaPixelV3.md)를 따른다. 아래는 이전 원본 직접 축소 방식에 대한 작업 기록이다.

## 이전 적용 방식: 사용자가 선택한 원본 직접 축소

사용자가 지정한 `exec-f46344b5-0ad4-4bb9-aa14-91c0a7298d1d.png`와 프로젝트의 `ChiikawaCharacters-v2.png`는 SHA-256이 동일하다(`3DED1604453DB92D10784E9FB11AF7DF586EA9CCF37E197BDF352753911A3AFC`). 원본 파일을 변경하거나 새 그림을 생성하지 않았다.

기존 64×64 재도트화, Point 확대, 알파 강제 이진화를 제거했다. 고용 프로필 Sprite는 원본 아틀라스의 얼굴 영역을 직접 참조한다. 텍스처는 무압축과 부드러운 축소 필터를 사용한다. Windows 창은 원본 색·알파를 유지한 면적 평균으로 실제 표시 픽셀 크기에 한 번만 축소한다. 150/200%에서는 최종 창 해상도에 맞춰 원본부터 다시 렌더링한다. 모몽가도 선택된 원본 시트 모습으로 통일하고, 빙요요 그림은 이전 작업물로 보존만 한다.

## 사용 파일

- [ChiikawaCharacters-v2.png](../Assets/Resources/DOTORION/Events/Art/ChiikawaCharacters-v2.png): 1536×1024 RGBA. 열은 치이카와·하치와레·우사기·모몽가·시사·랏코, 행은 전투·휴식·식사·얼굴 프로필. 원본을 보존하고 런타임에서 실루엣별로 분리한다.
- [MomongaBinyoyo-v2.png](../Assets/Resources/DOTORION/Events/Art/MomongaBinyoyo-v2.png): 이전 모몽가 빙요요 장비 그림. 현재 미사용.
- `SubjugationArt.cs`: 원본 해상도의 색과 알파를 보존하고 최종 표시 크기별 축소 결과만 캐시한다. 프로필 Sprite는 원본을 직접 참조한다.
- `SubjugationArtImporter.cs`: 압축·NPOT 크기 변경을 끄고 CPU 읽기와 축소용 밉맵·Trilinear 필터를 지정한다.

## 참고 자료와 장비 구분

- [공식 애니메이션 캐릭터 소개](https://www.anime-chiikawa.jp/chara.html), [공식 외형 라인업 이미지](https://www.anime-chiikawa.jp/images/characters/img_charapage.png): 얼굴·귀·눈·무늬·시사의 갈기·모몽가의 푸른 꼬리·랏코의 별 무늬와 흰 망토.
- [공식 치이카와 사스마타 상품](https://chiikawamarket.jp/products/4970381627768), [공식 하치와레 사스마타 상품](https://chiikawamarket.jp/products/4970381627775): 분홍/파랑 장비 구분.
- [공식 우사기 토벌봉 포함 상품](https://chiikawamarket.jp/zh-hant/products/4570118242662): 노란 막대형 토벌봉. 우사기에게 사스마타를 주지 않는다.
- [원작 나가노의 섬 편 장면](https://x.com/ngnchiikawa/status/1677263338303275008), [해당 원작 이미지](https://pbs.twimg.com/media/F0bWHVeaMAMAmzB.jpg): 랏코의 검과 모몽가가 사용한 빙요요의 손잡이·스프링·공. 고정 기본 장비라고 단정하지 않고 ‘섬 편 장비’라고 표기한다.
- 시사의 고정 토벌 무기는 확인하지 못했다. 창작 무기를 원작 장비로 제시하지 않고 ‘맨손 응원 · 이벤트 연출’로 명시한다. 현재 게임 수치나 역할을 바꾼 것은 아니다.

## 최종 생성 프롬프트 1: 공통 캐릭터 시트

Reference: 공식 애니메이션 라인업 `img_charapage.png`. Transparent background: true.

> Create a production PIXEL ART SPRITE SHEET for a very small desktop idle game. Use attached official character lineup ONLY as identity/shape/color reference. Faithfully match the original Nagano Chiikawa character proportions, enormous round mochi-shaped head flowing into a tiny plump body, very short arms and feet, oval dark-brown eyes with white lower highlights, tiny w-shaped mouth, pink striped blush, thin dark-brown pixel outlines. Do not turn them into generic cats or tall humanoid mascots. True crisp pixel artwork, ideally 64x64 logical pixels per character with nearest-neighbor enlarged pixels; no smooth painting, no gradients, no antialiasing. TRANSPARENT BACKGROUND, no checkerboard drawn. Output a regular EXACTLY SIX COLUMNS by FOUR ROWS grid of 24 isolated sprites, each cell equal square size, consistent positions and generous transparent gutters. No grid lines, no text, no labels, no decorative scene, no shadows, no UI. Each sprite entirely contained within its cell. Columns in this EXACT order, left to right on EVERY row: 1 Chiikawa, 2 Hachiware, 3 Usagi, 4 Momonga, 5 Shisa, 6 Rakko. In reference these are characters 1,2,3,4,9,8 respectively. Chiikawa white with small round ears; Hachiware blue cat ears and characteristic blue parted cap down to forehead with WHITE central face, not just blue ear tips; Usagi cream-yellow with LONG UPRIGHT ears with pink insides and cheeky expression; Momonga white flying squirrel, huge round eyes, blue inner ears and enormous bushy pale-blue tail with black detail lines; Shisa cream-yellow Okinawan lion, orange curly side mane, orange curly eyebrows and little pointed ears; Rakko cream-yellow fluffy sea otter with spiky fur edges, unmistakable FOUR-POINT STAR marking above the eye and white flowing cape/scarf as in reference, NO brown hair tuft, NO black cape. ROW 1: full bodies in ready-to-run combat stance facing slightly right, faces mostly visible. Chiikawa holds PINK U-shaped sasumata capture pole; Hachiware holds BLUE U-shaped sasumata capture pole; Usagi holds simple long YELLOW double-ended cylindrical staff, not a pitchfork, not a baseball bat; Momonga has NO artificial weapon, paws raised with fluffy tail; Shisa has NO artificial weapon, paws raised in encouraging pose; Rakko holds a SILVER short straight double-edged sword with dark grip and white cape. Weapons stay within each cell. ROW 2: the SAME six characters sitting curled up resting with eyes closed, relaxed, NO weapons, tails, ears, mane, and white cape preserve identities. ROW 3: the SAME six characters sitting eating small triangular onigiri or ramen bowl, NO weapons, original proportions and identity details. ROW 4: LARGE FACE PORTRAITS only, ears included, centered in cells, faces enlarged to use most of each cell; preserve original face details for each, Momonga large eyes, Shisa orange mane/eyebrows, Rakko star marking. Faces on row4 front-facing, calm happy expressions. Make this look like careful hand-authored game pixel art of the original Chiikawa cast, especially faithful face shapes and markings. Rigid six-column four-row alignment and consistent column identities are crucial for automated slicing.

## 최종 생성 프롬프트 2: 모몽가의 원작 장비

References: 새 캐릭터 시트 + 원작 섬 편 이미지. Transparent background: true. 이 결과를 모몽가 전투 프레임에 적용했다.

> Create ONE isolated combat sprite of Momonga from Chiikawa, on actual transparent background. Reference 1 is the pixel-art sheet: match EXACTLY the Momonga character at column4 row1 in identity, proportions, color palette, thin dark brown pixel outline, huge shining oval eyes with white lower highlights, tiny blue nose, pale blue inner ears, spiky white fluffy face, white plump body, enormous pale-blue fluffy tail with dark detail lines. Reference 2 is the original manga source ONLY for the weapon called binyoyo/びんよよ; do NOT copy the comic layout or other characters. CHANGE: Momonga should hold the canon island-issued binyoyo, a short light tan wooden handle with a small extended SILVER COILED SPRING and a small tan spherical ball at its end, a toy-like spring stick, NOT a sasumata, NOT a sword, NOT a yo-yo disc on a string. The wooden handle and released spring are visible in the original comic's top left panel, and the ball with a face is near Rakko's foot in the top right panel. Depict it compactly in front of Momonga, extending diagonally right from his raised paw, enough to recognize the wooden grip, metal spring, and round ball. Keep his own large pale-blue squirrel tail behind on right and clearly separate from weapon. Full body with feet and ears, eager mischievous smile, three-quarter front-facing towards right. Faithful Nagano character shape and SAME polished crisp 64x64-logical-pixel sprite style as reference1, enlarged with nearest-neighbor. Clean hard pixel edges, opaque character, transparent empty canvas, generous transparent margin. Only one character, one pose, one binyoyo, no labels, no UI, no shadows, no environment.
