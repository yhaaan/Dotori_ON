# 토벌 전투 및 라운드 제어 (이전 구조)

현재 구현은 `Documentation/ChiikawaFrontline.md`를 따른다. 이 문서는 개별 체력과 통합 창 이전 구조 및 최초 적 2종의 제작 기록이다.

- 프로필 라운드는 기존 시간 텍스트 위의 독립된 라벨이다. 시간 텍스트의 폰트, 크기, 색, 정렬과 설정을 따라가며 시간 표시는 유지한다.
- 출전 멤버는 전투 중앙에 모이고, 전방 멤버와 적 사이의 거리를 좁혔다.
- 공격/피격 수치와 초록색 회복 수치를 표시한다. 성공은 1초, 실패는 재도전 대기 동안 표시하며 작업 외 상태에서는 전투와 대기 시간이 멈춘다.
- R. 라운드를 누르면 1~최고 도달 라운드 내에서 이동할 수 있다. 이동하면 해당 전투가 초기화된다.
- 화살표 토글 ON은 승리 후 다음 라운드, OFF는 동일 라운드 반복이다. OFF에서도 승리 보상을 받고 다음 라운드는 해금된다. 마우스를 올리면 설명이 나타난다.
- 관리창은 각 멤버의 최대체력, 공격력(시사는 회복량)을 보여준다. 미고용 멤버는 Lv.1 기준이다. 레벨업 시 체력과 공격/회복량이 증가한다. 전투 체력은 출전 멤버 최대체력 합계인 토벌대 공용 체력이며, 시사는 공용 체력을 회복한다.
- 기존 캐릭터 그림에 캐릭터별 네 단계 준비·후퇴·전진·복귀 키 포즈와 찌르기/도약/꼬리 타격/회복/베기 효과를 적용한다. 얼굴, 우사기 입 모양과 기존 무기 이미지를 유지한다.
- 저장 데이터 v1은 v2로 자동 이전한다. 재화/레벨/출전 멤버/라운드를 보존하고 최고 라운드와 자동 진행 설정을 추가한다. 파일 이름은 기존 경로를 유지한다.

## 적 스프라이트

`Assets/Resources/DOTORION/Events/Art/ChiikawaMonsters-v1.png`는 built-in image_gen으로 생성했다. 키메라와 그 아이(아노코)를 홀수/짝수 라운드에서 번갈아 표시한다.

외형 참고: [공식 키메라 상품](https://chiikawamarket.jp/products/4571609356950), [공식 아노코 상품](https://chiikawamarket.jp/products/4571609356943). 상품의 글자와 네온 스티커 효과는 게임 스프라이트에 포함하지 않는다.

최종 프롬프트:

```text
Use case: stylized-concept. Project sprite atlas. Reference image 1 is official Chiikawa Chimera; reference image 2 official Anoko. Draw just these TWO full body monsters as clean game pixel art on actual transparent background, no lettering, sticker, border, neon outlines or background. One horizontal row, Chimera LEFT and Anoko RIGHT, ample transparent gap, each centered in half of 1024x512 canvas and feet aligned. Chimera: pink round creature, small dark rounded ears, whiskers, small white wings, white belly, thin arrow tip tail. Anoko: white furry round creature with two ears, small orange teardrop forehead horn, very large round eyes, pink cheeks, clawed little hands and feet and striped long tapering tail. Preserve recognizable canonical shapes from references, not generic brown slime. Face toward left for enemies attacking player at left. Crisp low-resolution pixel steps, limited natural pastel colors, dark brown outline, readable at 48 pixels high, no soft shading. Entire body including wings and tail inside frame.
```
