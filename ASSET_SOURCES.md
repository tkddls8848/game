# 에셋 출처 목록

이 게임("사건 파일" 연출: 어두운 저택 평면도 + 바랜 종이 UI + 명조체)에 쓸 수 있는 **무료** 에셋 출처.
받은 파일은 `DetectivePrototype/Assets/Resources/GameData/art.json`이 가리키는 자리에 두면 바로 반영되고,
출처·라이선스는 `CREDITS.md`에 한 줄 적는다.

> 라이선스 문구는 2026-09 기준 검색 결과로 확인한 것이다. 내려받기 전에 각 페이지의 라이선스를 한 번 더 읽을 것.
> 특히 "무료판"은 개인 용도만 허용하는 경우가 있다.

**art.json의 자리는 지금 전부 채워져 있다.** 무엇이 어디서 왔는지는 `CREDITS.md`를 본다
(바닥 6장 ambientCG, 초상화 5장·단서 7장 Wikimedia Commons, 배경음 Musopen 쇼팽, 바람 OpenGameArt, 효과음 Kenney — 전부 CC0/퍼블릭 도메인).
아래 목록은 **갈아 끼울 후보를 찾을 때** 쓴다.

## 라이선스 읽는 법

| 표시 | 뜻 | 이 게임에서 |
|---|---|---|
| **CC0** / 퍼블릭 도메인 | 제한 없음, 크레딧 불필요 | 그냥 쓴다 |
| **OFL 1.1** (폰트) | 자유롭게 사용·포함 가능, 폰트 자체를 팔지만 않으면 됨 | 그냥 쓴다 |
| **CC BY** | 사용 가능하지만 **크레딧 표기 필수** | `CREDITS.md`와 게임 안 결과 화면에 표기 |
| Pixabay 라이선스 | 크레딧 불필요, 재배포 불가 | 그냥 쓴다 |
| "무료판 / 개인 용도" | 상업적 사용 불가 | 프로토타입에만 쓰고 배포 전에 교체 |

---

## 1. 그림 — 진중한 분위기 (현재 연출 방향)

### 초상화 · 단서 사진 (실제 회화·유물)

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [The Met Open Access](https://www.metmuseum.org/hubs/open-access) | 회화·유물 37만 점 이상. 19세기 유화 초상, 은식기, 약병 등 | CC0 | 검색할 때 **Open Access 필터**를 켠다. 인물 4명은 같은 시대·화풍(예: 1880~1910년)으로 고른다 |
| [Met 이미지 FAQ](https://www.metmuseum.org/policies/frequently-asked-questions-image-and-data-resources) | 사용 조건 상세 | — | 배포 전에 한 번 읽어 둘 것 |
| [Public Domain Image Archive](https://pdimagearchive.org/) | 여러 박물관의 퍼블릭 도메인 이미지 모음 | 항목별 확인 | Met 자료를 주제별로 찾기 쉽게 정리해 둠 |
| [Wikimedia Commons](https://commons.wikimedia.org/) | 회화·사진 | 항목별 확인(PD/CC) | 작가 사후 70년 지난 회화는 PD. 사진 재현물 조건은 항목마다 다름 |

이미지는 받은 뒤 세피아·비네팅으로 톤을 맞추면 출처가 달라도 한 게임처럼 보인다. 게임 코드가 초상화에 세피아 색을 곱하므로 원본을 그대로 넣어도 된다.

### 바닥 · 벽 질감

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [ambientCG](https://ambientcg.com/) | PBR 질감 2000+ (마루·대리석·카펫·석재) | CC0 | `Color` 맵(1K JPG)만 쓰면 된다. 예: [Wood Floor 040](https://ambientcg.com/view?id=WoodFloor040) |
| [Poly Haven Textures](https://polyhaven.com/textures) | 고품질 질감 | CC0 | ambientCG와 같은 방식 |
| [CC0 Textures](https://cc0-textures.com/) | 질감 모음 | CC0 | — |

넣을 때 임포트 설정: **Wrap Mode = Repeat, Mesh Type = Full Rect** (Tiled 바닥에 필요).

### 종이 · UI 장식

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [Kenney UI Pack](https://kenney.nl/assets/ui-pack) 등 Kenney 2D | UI 요소, 아이콘 | CC0 | 스타일이 밝은 편이라 색만 빌리거나 아이콘 정도만 |
| [game-icons.net](https://game-icons.net/) | 단색 아이콘 4000+ (와인잔, 열쇠, 약병…) | CC BY 3.0 | 노트 탭·단서 아이콘용. **크레딧 필요** |
| The Met / Wikimedia | 오래된 편지·장부·지도 스캔 | CC0/PD | 노트 배경으로 실제 종이 스캔을 쓰면 더 낫다 |

---

## 2. 그림 — 픽셀 아트 (다른 방향으로 갈 때 참고)

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [Noble Interiors (Kelano Studio)](https://kelano-studio.itch.io/noble-interior) | 빅토리아풍 가구 30종 × 8색, 64px | 무료, 상업 사용·수정 가능, 크레딧 선택 | 저택 분위기에 가장 가깝다 |
| [PIPOYA FREE RPG Character Sprites 32x32](https://pipoya.itch.io/pipoya-free-rpg-character-sprites-32x32) | 4방향 걷기 캐릭터 | 페이지 확인 | 탑다운 인물 |
| [32x32 Top-Down Character Animation Pack (freddyka)](https://freddyka.itch.io/32x32-top-down-character-animation-pack) | 걷기·대기 애니메이션 | 페이지 확인 | — |
| [Penzilla Top-Down Retro Interior](https://penzilla.itch.io/top-down-retro-interior) | 실내 소품 | **비상업만 무료**, 상업은 구매 + 크레딧 | 프로토타입 한정 |
| [LimeZu Modern Interiors 무료판](https://limezu.itch.io/moderninteriors) | 현대풍 실내 | **개인·테스트 용도만** | 분위기도 안 맞음. 참고만 |
| [itch.io CC0 탑다운 에셋](https://itch.io/game-assets/assets-cc0/tag-top-down) | 목록 | CC0 | 필터로 CC0만 볼 수 있다 |
| [itch.io 무료 실내 픽셀 아트](https://itch.io/game-assets/free/tag-interior/tag-pixel-art) · [mansion 태그](https://itch.io/game-assets/tag-mansion/tag-pixel-art) | 목록 | 항목별 | — |
| [OpenGameArt](https://opengameart.org/) | 그림·소리 모음 | 항목별(CC0/CC BY/GPL 혼재) | 라이선스 필터 필수 |

---

## 3. 폰트

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [Noto Serif KR](https://fonts.google.com/noto/specimen/Noto%2BSerif%2BKR) · [notofonts/noto-cjk](https://github.com/notofonts/noto-cjk) | 한글 명조. **이미 포함됨** (`Assets/Resources/Fonts/`) | OFL 1.1 | 현재 UI 폰트 |
| [눈누 (noonnu.cc)](https://noonnu.cc/) | 상업용 무료 한글 폰트 검색 | 폰트별 | "허용 범위: 임베딩·게임" 표시를 확인 |
| [갈무리 (Galmuri)](https://github.com/quiple/galmuri) | 한글 픽셀 폰트 | OFL 1.1 | 픽셀 아트로 갈 때 |
| [물마루 (Mulmaru)](https://github.com/mushsooni/mulmaru) | 게임용 한글 픽셀 폰트 | 페이지 확인 | 픽셀 아트로 갈 때 |
| [Google Fonts](https://fonts.google.com/?subset=korean) | 나눔명조·본고딕 등 | 대부분 OFL | 제목용 세리프를 하나 더 고를 때 |

---

## 4. 음악

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [Musopen](https://musopen.org/) | 클래식 녹음. [쇼팽 녹턴 Op.9](https://musopen.org/music/108-nocturnes-op-9/), [Op.48](https://musopen.org/music/111-nocturnes-op-48/) | 퍼블릭 도메인 | 녹음 자체의 재판매 금지, 크레딧 표기 요청 → `CREDITS.md`에 적는다. 탐색은 Op.9, 결과 화면은 Op.48 같은 어두운 곡 |
| [Musopen – Internet Archive 미러](https://archive.org/details/musopen-chopin) | 같은 음원 | 퍼블릭 도메인 | 원 사이트가 느릴 때 |
| [Kevin MacLeod (incompetech)](https://incompetech.com/music/royalty-free/music.html) | 미스터리·긴장 BGM 2000곡+ | **CC BY 4.0** | 타임라인 관찰 모드용. 게임 안 크레딧 필수 |
| [Free Music Archive](https://freemusicarchive.org/) | 독립 음악 | 곡별(CC) | 라이선스 필터 사용 |
| [Pixabay Music](https://pixabay.com/music/) | BGM | Pixabay 라이선스(크레딧 불필요) | 피아노·앰비언트 검색 |

---

## 5. 효과음 · 환경음

| 출처 | 내용 | 라이선스 | 메모 |
|---|---|---|---|
| [Pixabay 효과음](https://pixabay.com/sound-effects/) | [괘종시계](https://pixabay.com/sound-effects/search/grandfather%20clock/), 창밖 바람, 벽난로, 발소리 | Pixabay 라이선스 | `Audio/SFX/clock_chime`, `Audio/Ambient/winter_night` |
| [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) | 책장·천·삐걱임·발소리 50개 | CC0 | `Audio/SFX/page`, `inspect` |
| [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds) | UI 클릭 100개 | CC0 | 메뉴 이동음 |
| [freesound.org](https://freesound.org/) | 효과음 대규모 모음 | 항목별(CC0/CC BY) | 라이선스 필터 사용 |
| [jsfxr](https://sfxr.me/) | 브라우저에서 효과음 합성 | 만든 소리는 본인 것 | 게임 안 합성음이 마음에 안 들 때 |

---

## 6. 자리별 정리 (art.json과 1:1)

| art.json 항목 | 파일 | 지금 쓰는 출처 | 갈아 끼울 후보 |
|---|---|---|---|
| `npcs[].portrait` | `Art/Portraits/npc_*.png` (256×256) | Wikimedia Commons — 사전트·소른 초상 (PD) | The Met Open Access |
| `evidence[].image` | `Art/Evidence/ev_*.png` (256×256) | Wikimedia Commons (PD/CC0) | The Met Open Access |
| `rooms[].floorTexture` | `Art/Floors/*.jpg` (512×512) | ambientCG Color 맵 (CC0) | Poly Haven |
| `uiFont` | `Fonts/NotoSerifKR-Regular.otf` | Noto Serif KR (OFL 1.1) | 눈누 |
| `audio.bgmExplore / bgmTimeline / bgmResult` | `Audio/BGM/*.ogg` | Musopen 쇼팽 (CC0) | Kevin MacLeod (CC BY — 게임 안 표기 필요) |
| `audio.ambientLoop` | `Audio/Ambient/winter_night.ogg` | OpenGameArt wind (CC0) | Pixabay, freesound |
| `audio.sfxClockChime / sfxPage / sfxInspect` | `Audio/SFX/*.ogg` | Commons 종소리 (CC0), Kenney RPG Audio (CC0) | Pixabay, jsfxr |

경로는 전부 `Assets/Resources/` 기준이고 확장자는 art.json에 적지 않는다.
파일을 빼면 그 자리는 코드가 만든 대체물(생성 질감·실루엣·합성음)로 되돌아가므로 하나씩 갈아 끼워도 게임은 돈다.

새 파일을 넣으면 `Assets/Editor/DetectiveEditor/ArtAssetImporter.cs`가 임포트 설정을 자동으로 맞춘다
(바닥은 Wrap=Repeat · Mesh=Full Rect · PPU=질감 크기, UI 그림은 Sprite, 배경음은 Streaming).
기본값으로 이미 임포트된 파일은 `Tools/Detective/Reimport Art Assets`.
# 추가 다운로드 완료 (2026-09-25)

Kenney CC0 에셋 **64개**(효과음 40개, UI 아이콘 24개)를 추가 확보했다.
[용도·사용 경로](docs/ADDITIONAL_ASSETS.md) · [보기/듣기](docs/additional-assets.html) · [파일별 출처](docs/additional-assets.json).
기존 에셋 교체 없이 추가했으며 게임 내 연결은 아직 하지 않았다.
