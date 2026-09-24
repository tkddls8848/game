# 크레딧 · 외부 에셋

이 프로젝트가 저장소에 포함해 쓰는 외부 자료와 그 라이선스. 새 에셋을 넣으면 여기에 한 줄 추가한다.
(코드가 만드는 질감·실루엣·합성음은 이 프로젝트 자체의 산출물이라 따로 적지 않는다.)

| 자료 | 위치 | 출처 | 라이선스 |
|---|---|---|---|
| Noto Serif KR (Regular, KR 서브셋) | `DetectivePrototype/Assets/Resources/Fonts/NotoSerifKR-Regular.otf` | [notofonts/noto-cjk](https://github.com/notofonts/noto-cjk) | SIL Open Font License 1.1 — 전문은 같은 폴더의 `LICENSE-NotoSerifKR.txt` |

## 아직 비어 있는 자리 (직접 넣을 것)

`Assets/Resources/GameData/art.json`이 가리키는 경로에 파일을 두면 바로 반영된다. 파일이 없으면 코드가 만든 대체물이 나온다.

| 자리 | 경로(Resources 기준) | 추천 출처 | 라이선스 메모 |
|---|---|---|---|
| 인물 초상화 5장 | `Art/Portraits/npc_a.png` … `npc_victim.png` | [The Met Open Access](https://www.metmuseum.org/hubs/open-access) (CC0 필터) — 1880~1910년대 유화 초상 | CC0, 크레딧 불필요 |
| 단서 이미지 7장 | `Art/Evidence/ev_wineglass.png` … | The Met Open Access 유물 사진 | CC0 |
| 바닥 질감 | `art.json`의 `floorTexture`에 경로 지정 | [ambientCG](https://ambientcg.com/) | CC0 |
| BGM 3곡 | `Audio/BGM/explore`, `timeline`, `result` | [Musopen](https://musopen.org/) 쇼팽 녹턴 등 | 퍼블릭 도메인. Musopen은 크레딧 표기 요청 → 여기에 적는다 |
| 환경음 | `Audio/Ambient/winter_night` | [Pixabay](https://pixabay.com/sound-effects/) "wind window", "fireplace" | Pixabay 라이선스, 크레딧 불필요 |
| 괘종시계·종이·조사 효과음 | `Audio/SFX/clock_chime`, `page`, `inspect` | Pixabay, [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) | 크레딧 불필요 / CC0 |

CC BY 음원(예: Kevin MacLeod)을 쓰면 게임 안 결과 화면 크레딧에도 표기해야 한다.
