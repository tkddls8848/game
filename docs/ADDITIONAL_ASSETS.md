# 추가 확보 에셋 — 2026-09-25

현재의 2D 저택·엿듣기 추리 방향에 맞춰 Kenney의 CC0 에셋 64개를 선별했다.
다운로드와 파일 검증을 마쳤으며, 기존 장면·art.json·게임 동작에는 아직 연결하지 않았다.

**[아이콘 보기 / 효과음 듣기](additional-assets.html)** — 브라우저로 열면 종류별 필터와 개별 재생을 사용할 수 있다.

## 구성과 추천 용도

아래 경로는 `DetectivePrototype/Assets/Resources/` 기준이다.

| 경로 | 수량 | 추천 용도 |
|---|---:|---|
| `Audio/SFX/Additional/RPG/` | 28 OGG | 문 열기·닫기 4개, 삐걱임 3개, 발소리 10개, 책 5개, 옷감 3개, 걸쇠·동전·가죽 각 1개 |
| `Audio/SFX/Additional/Interface/` | 12 OGG | 클릭·선택·뒤로·창 열기/닫기·확정·오류·스크롤·전환·틱·토글 |
| `Art/Icons/Kenney/` | 24 PNG | 재생/일시정지/정지/되감기, 음량, 확대/축소, 인물, 잠금, 문, 전화, 설정 |

책과 옷감 소리는 수사 노트·단서 조사에, 재생 제어 아이콘은 시간 탐색 UI에 우선 사용하기 좋다.
발소리와 문 소리는 NPC 이동 연출 후보로 확보했다. 소리로 정답이 새어나오지 않도록 실제 연결 단계에서 청취 범위와 재생 시점을 정해야 한다.
효과음의 적합성·음량·체감은 미리보기에서 청취해 결정한다. 이번 확인은 디코딩 검증이며 청감 검수는 아니다.

## Unity에서 사용

- 프로젝트를 열면 기존 `ArtAssetImporter`가 추가 효과음을 Mono/Vorbis/DecompressOnLoad로 임포트한다.
- 같은 임포터에 `Art/Icons` 경로를 추가했다. 아이콘은 Single Sprite, Clamp, 밉맵 없음으로 자동 설정한다.
- 아이콘은 투명 배경의 흰색 원본이다. `Image.color`로 종이 위의 검은 잉크색이나 강조색을 지정할 수 있다.
- 예: `Resources.Load<Sprite>("Art/Icons/Kenney/pause")`, `Resources.Load<AudioClip>("Audio/SFX/Additional/RPG/bookOpen")`.
- `.meta`는 Unity가 최초 임포트할 때 생성한다. 생성 후 버전 관리에 포함해 GUID를 유지한다.
- 현재 Resources에 있는 선별본은 빌드 포함 대상이다. 최종 채택 시 사용하지 않는 후보는 Resources 밖으로 이동할 수 있다.

## 출처와 재확인

| 팩 | 공식 출처 | 라이선스 |
|---|---|---|
| RPG Audio | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| Interface Sounds | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| Game Icons | https://kenney.nl/assets/game-icons | CC0 1.0 |

공식 페이지와 다운로드 ZIP 안의 라이선스를 모두 확인했다. 각 라이선스는 개인·상업 프로젝트 사용을 허용하며 크레딧은 선택 사항이다.
원문은 `DetectivePrototype/Assets/ThirdParty/Kenney/*-LICENSE.txt`에 보존했다.
원본 ZIP 3개는 프로젝트 밖 `AssetDownloads/Kenney/`에 보관했다. Unity에 전체 팩이 중복 임포트되지 않는다.

[파일별 목록](additional-assets.json)에 원본 ZIP 내부 경로, Resources 경로, SHA-256, 파일 크기, 음원 길이 또는 이미지 크기를 기록했다.
선별본은 원본 바이트를 그대로 복사했다. 기존 `page.ogg`와 `inspect.ogg`에 쓰인 두 파일은 선별 대상에서 제외했다.

검증: ZIP 3개 CRC 검사, 효과음 40개 FFmpeg 전체 디코딩, PNG 24개 서명·크기 확인 완료.
Unity Editor 임포트와 게임 내 재생은 이번 작업에서 실행하지 않았다.
