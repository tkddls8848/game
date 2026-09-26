# CLAUDE.md — 엿듣기 추리 게임

**내러티브 미스터리 · 분위기 호러.** 어두운 저택의 십 분을 되감아 들으며 목소리에 이름을 붙인다.
포지셔닝과 문구 규칙은 `docs/POSITIONING.md`, 출시 판정 기준은 `DEVELOPMENT_PLAN_RELEASE.md`.

**엔진은 Unity로 확정**(2026-09-26). Godot 이식본은 제거했고 근거는 `docs/ENGINE_COMPARISON.md`에 남겼다.

이 파일은 **작업할 때 매번 필요한 사실**만 담는다.

---

## 환경

| 항목 | 값 |
|---|---|
| Unity 에디터 | `C:\Users\PSI\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe` |
| Unity Hub | MSIX 설치 — `AppData\Local\Packages\UnityTechnologies.UnityHub_*` |
| .NET SDK | `C:\Users\PSI\.dotnet\dotnet.exe` (8.0.425, 사용자 설치라 PATH에 없다) |
| 프로젝트 루트 | `C:\Users\PSI\orca\game\DetectivePrototype` |
| 저장소 루트 | `C:\Users\PSI\orca\game` |
| 입력 | 레거시 `Input` 클래스 (Input System 패키지 미사용) |
| 렌더 파이프라인 | **내장(Built-in)**. URP·PostProcessing 패키지 없음 — 후처리는 이미지 이펙트로 직접 건다 |

> **에디터 위치는 Hub의 `secondaryInstallPath`가 정한다.** 기본값(`C:\Program Files\Unity`)이 아니다.
> Hub가 MSIX로 설치돼 있어 설정이 패키지 컨테이너 안에 있다:
> `AppData\Local\Packages\UnityTechnologies.UnityHub_*\LocalCache\Roaming\UnityHub\secondaryInstallPath.json`
> 경로가 바뀌면 그 파일을 먼저 읽는다.
>
> **라이선스 필수.** 없으면 batchmode가 return code 198(`No valid Unity Editor license found`)로 거부된다.
> 활성화: Unity Hub 로그인 → Personal 라이선스 자동 발급.

---

## 검증 명령

### 1. 빠른 길 — 엔진 없이 (0.3초)

**대부분의 작업은 여기서 끝난다.** Unity를 띄우지 않고 순수 C# 로직 전부를 테스트한다.

```bash
python tools/sync_shared_logic.py            # 파일 목록 갱신 (--check 로 최신 여부만)
C:/Users/PSI/.dotnet/dotnet.exe test SharedLogicTests/SharedLogicTests.csproj
```

**311개 통과가 기준선.** Unity batchmode는 라이선스가 필요하고 프로젝트를 잠그고 분 단위로 걸리는데
이쪽은 0.3초다. 그 차이가 이 경로가 존재하는 이유다.

`SharedLogicTests`는 Unity를 참조하지 않는다. `Assets/Scripts` 아래에서
`using UnityEngine`/`using UnityEditor`가 **없는** 파일만 컴파일하고, Unity의 EditMode 테스트 파일을
그대로 돌린다. 복사가 아니라 원본을 직접 가리킨다.

> **여기에 자기 강제력이 있다.** 누가 로직 파일에 UnityEngine을 끌어들이면 그 파일이 목록에서 빠지고
> **테스트 빌드가 컴파일 오류로 터진다.** §18-1이 문서상의 약속이 아니라 게이트가 된다.

### 2. Unity batchmode (PowerShell)

Phase 완료를 주장하기 전에 **실제로 실행**한다.

#### ⚠️ 반드시 `Start-Process -Wait`로 실행할 것

`Unity.exe`는 **GUI 서브시스템 실행 파일**이다. `& $UNITY ...`로 부르면 종료를 기다리지 않고 즉시 반환한다:

* `$LASTEXITCODE`가 비어 있어 성공/실패를 판정할 수 없다
* 백그라운드 태스크가 "완료"로 표시되면서 살아 있는 Unity가 정리돼 로그가 끊긴다
* 다음 batchmode 호출이 `another Unity instance is running`으로 실패한다

`-logFile -`(stdout)도 신뢰할 수 없다. **로그는 파일로 받아서 읽는다.**

```powershell
$UNITY = "C:\Users\PSI\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe"
$PROJ  = "C:\Users\PSI\orca\game\DetectivePrototype"

function Invoke-Unity([string[]]$UnityArgs, [string]$Log) {
    $p = Start-Process -FilePath $UNITY -ArgumentList ($UnityArgs + @('-logFile', $Log)) `
         -Wait -PassThru -NoNewWindow
    return $p.ExitCode
}
```

| 목적 | 인자 | 통과 기준 |
|---|---|---|
| 컴파일 | `-batchmode -quit -nographics -projectPath $PROJ` | 로그에 `error CS` 0건 (`warning CS`도 0으로 유지) |
| EditMode 테스트 | `... -runTests -testPlatform EditMode -testResults $PROJ\TestResults.xml` | **320/320**. 상세는 XML의 `passed`/`failed` |
| 씬 재생성 | `... -executeMethod DetectiveEditor.SceneBuilder.RebuildMainScene` | 종료 코드 0 |
| Windows 빌드 | `... -executeMethod DetectiveEditor.BuildScript.BuildWindows` | 로그에 `[BuildScript] Succeeded` |

EditMode 320 = 공유 311 + Unity 전용 9(`GameDataTests`, Resources 로더를 직접 시험한다).

씬 빌더는 rooms.json 무결성 검사에 실패하면 **예외를 던지고 씬을 만들지 않는다**.
에디터 GUI에서는 `Tools/Detective/Rebuild Main Scene`, 데이터만 검사하려면 `Tools/Detective/Validate Game Data`.

빌드는 **반드시 `BuildScript`로 한다.** 사람이 에디터에서 누르면 Always Included Shaders 등록이 빠져
빌드에서만 화면이 자홍색으로 죽는 사고가 난다(이미 한 번 겪었다). `BuildScript`가 먼저 `RuntimeShaderSetup`을 돌린다.

### 3. 실행

```powershell
DetectivePrototype\Build\DetectivePrototype.exe -locale en -screen-width 1280 -screen-height 720 -screen-fullscreen 0
```

`-locale en`이 씬에 저장된 언어를 덮는다. 설정 화면이 아직 없고, 자동화 검증에서는 화면을 누를 수 없다.

---

## ⚠️ 규칙: 에디터가 열려 있으면 batchmode 금지

프로젝트 락 때문에 실패한다. 실행 전 `Get-Process Unity -ErrorAction SilentlyContinue`로 확인하고,
살아 있으면 사람에게 닫아 달라고 요청한 뒤 그동안 코드만 쓴다. (빠른 길 §1은 락과 무관하게 돈다.)

---

## 어셈블리 구조

```
Assets/Scripts/        DetectivePrototype.asmdef              (런타임, 네임스페이스 Detective.*)
Assets/Editor/         DetectivePrototype.Editor.asmdef       (에디터 전용, → DetectivePrototype)
Assets/Tests/EditMode/ DetectivePrototype.Tests.EditMode.asmdef (테스트, → DetectivePrototype)
SharedLogicTests/      (Unity 밖. 위 테스트 파일 + 순수 로직을 그대로 컴파일한다)
```

**이 경계를 깨지 말 것.** asmdef가 있는 어셈블리는 기본 어셈블리(Assembly-CSharp)를 참조할 수 없다.
런타임 코드는 반드시 `Assets/Scripts/` 아래에 두어야 테스트에서 참조된다.

---

## 설계 원칙 (요약, 전문은 DEVELOPMENT_PLAN.md §18)

1. **로직에서 UnityEngine 의존을 뺀다.** 시간·가청 판정·거리감·채점·목격은 순수 C#
   → GUI 없이 검증 가능. 이게 이 프로젝트의 핵심 제약이고 §검증 §1이 그것을 강제한다
2. **손으로만 만들 수 있는 에셋을 만들지 않는다.** 씬·빌드·시나리오 전부 스크립트가 만든다
   (`SceneBuilder.cs` · `BuildScript.cs` · `tools/build_case02.py`)
3. **사건 데이터는 JSON.** ScriptableObject(.asset)는 GUID YAML이라 텍스트로 안전하게 못 쓴다
4. Phase 하나씩. 각 커밋은 컴파일이 통과하는 상태
5. 시각은 **정수 밀리초**. **부동소수·문자열 시각 비교 금지**
   * 저택 사건(case_01)은 18:00~19:00을 10분 7칸으로 쓴다. `GameTime.StartMs`=0, `EndMs`=3,600,000.
     틱(0~6)은 **표시용 파생값**이고, 틱으로 적힌 데이터는 **경계 함수 `GameTime.TickToMs`로만** ms가 된다
     (`RevealMs`·`RelatedMs`·`ClaimMs`·`SightingMs`·`CaseAnswer.TimeMs`). `-1` = `GameTime.NoTime`
   * 엿듣기 사건(case_02)은 **회차 기준 ms**를 데이터에 직접 적는다(600,000 = 10분). 틱을 쓰지 않는다 —
     회차 전체가 틱 한 칸이라 칸으로는 "4분 1.5초"를 적을 수 없다
   * 시각을 받는 API는 ms, 칸을 받는 API는 이름에 `Tick`이 붙는다(`TickOf`·`TickLabel`·`ClampTick`)
6. **한국어가 원문, 영어는 덮어쓰기.** 코드는 `Localization.Text("key", "한국어 원문")` 꼴로 부른다 —
   표가 없거나 깨져도 화면이 비지 않고, 코드를 읽는 사람이 원문을 그 자리에서 본다.
   **정적 필드에 담지 말 것**(`const`·`static readonly`): 언어를 바꿔도 첫 값이 굳는다. 속성으로 둔다.
   조각을 이어 붙이지 말고 `{0}` 자리표시자를 쓴다 — 영어는 어순이 다르다
7. **장면이 1차 자료다.** 대본·동선·소리를 손으로 따로 적으면 반드시 어긋난다
   (두 사람이 대화하려면 그 시각에 같은 방에 있어야 한다). `tools/build_case02.py`가 장면만 받아 나머지를 뽑는다

---

## 데이터 위치

```
Assets/Resources/GameData/
    rooms.json
    art.json
    npcs/      npc_a.json ...
    evidence/  evidence.json
    dialogue/  dialogue_npc_a.json ...
    locale/    ui.en.json · content.en.json · case_02.script.en.json
    cases/     case_01.json
               case_02.json
               case_02/  script.json · tracks.json · events.json     ← 생성물
               slice/    script_slice.json                            ← 수직 슬라이스
```

로딩: `Resources.Load<TextAsset>("GameData/...")` + `JsonUtility`.
`JsonUtility`는 Dictionary/다형성 불가 → **배열 + 문자열 ID 참조로 평평하게** 설계한다.
빠진 int를 0으로 채우므로 **시각 없는 int 필드는 `-1`을 명시**한다.

### 맵 (`rooms.json`)

* 좌표는 월드 유닛, `(x, y)`는 **좌하단 모서리**. 방은 축 정렬 사각형이다
* 벽 두께 `RoomLayout.WallThickness = 0.4`. **문(`doors`)의 짧은 변은 이보다 두꺼워야** 벽에 구멍이 뚫린다
* 벽은 손으로 배치하지 않는다. `RoomLayout.BuildAllWallSegments()`가 만든다
* 방 배치: 아래줄 `로비 | 식당 | 창고`, 가운데 가로 `복도`, 윗줄 `서재(room_victim) | 손님 방(room_suspect)`

**벽 맞닿음이 가청 판정의 근거다**(문이 아니다). 실제 인접 관계:

```
로비↔식당  식당↔창고  로비↔복도  식당↔복도  창고↔복도  복도↔서재  복도↔손님방
```

> **복도만 모든 방과 맞닿는다.** 서재 소리를 들을 수 있는 곳은 복도뿐이고,
> 서재↔손님방·서재↔아래층·로비↔창고는 서로 **무음**이다. 시나리오 설계의 축이 이것이다.

* JSON을 고친 뒤에는 **반드시 씬을 다시 만든다.** 씬은 rooms.json·evidence.json·npcs의 파생물이다

### 저택 사건 (`case_01`) — 인물·단서·대사

* `npcs/*.json`의 시각 필드는 **틱 단위**. `schedule[7]`=실제 방, `claims[7]`=주장하는 방(빈 문자열이면 사실대로)
* 목격 대사는 스케줄에서 자동 생성된다(같은 틱+같은 방). **거짓말하는 틱의 목격은 털어놓지 않는다**
* 대사 한 줄의 역할: `revealsClaims`(알리바이, 인물당 1개) · `requiresEvidence`(조건부) ·
  `claimTick/Room`(말 바꾸기) · `sightingTick/Target`(자동 목격 덮어쓰기)
* `*Tick` 필드는 코드에서 직접 읽지 말고 스키마의 `*Ms` 속성으로 읽는다. 예외는 `GameDataValidator`

**정답(스포일러):** 클라라(npc_a) / 금전(횡령 발각) / 18:30 / 서재 / 독살 / 립스틱 자국 와인잔.
클라라의 "계속 식당" 증언이 18:10 창고·18:20 복도·18:40 복도 목격과 모순되고, 18:30 알리바이는 본인 증언뿐이다.

### 엿듣기 사건 (`case_02`) — **생성물이다. 손으로 고치지 말 것**

`script.json` · `tracks.json` · `events.json` · `locale/case_02.script.en.json`은
**`tools/build_case02.py`가 만든다.** 고치려면 그 파일의 `SCENES`를 고치고 다시 돌린다.

```bash
python tools/build_case02.py            # 생성
python tools/build_case02.py --check    # 설계 문제만 확인 (쓰지 않는다)
```

* **장면에 누가 있는지가 곧 동선이다.** 그래서 "대화하는데 그 방에 없는 사람"이 구조적으로 불가능하다
* 한국어·영어를 **나란히 적는다.** 나중에 번역하면 대본이 바뀔 때마다 어긋난다
* `Line(..., at=ms)`로 절대 시각을 고정한다. 앞 대사가 밀려 그 시각에 닿지 못하면 **오류로 멈춘다**
* 문소리·발소리는 적지 않는다 — `MovementEvents`가 이동 트랙에서 뽑는다(§엿듣기)

현재 규모: 발화 190 · 이벤트 12 · 이동 22회 · 주고받는 대화 63% · 목소리별 33~46발화.

**정답(스포일러):** 헬렌 모로(npc_d, 목소리 v4) / 협박(1907년 진료 기록 조작) / 회차 241,500ms(8시 14분) /
서재 책상 오른쪽 서랍 / 약 바꿔치기(니트로→설탕) / 창고 석탄통 속 성 앨런 요양원 라벨 갈색 병.

사실 14개를 여섯 방에 갈라 두었다: 서재 4 · 식당 4 · 복도 2 · 창고 2 · 로비 1 · 손님방 1.
**1회차로 못 모으는 근거**: `fact_swap_moment`(복도)와 `fact_edmund_heart_pills`(식당)가 8시 14분에
동시에 울린다. 둘 다 발화가 하나뿐이라 어느 조합으로도 겹침을 피할 수 없다.

### 검사기 (데이터를 고치면 돈다 — 씬 빌더 + EditMode)

| 검사기 | 보는 것 |
|---|---|
| `GameDataValidator` | 참조 무결성 |
| `RoomLayoutValidator` | 방·문 배치 |
| `CaseSolvabilityChecker` | case_01: 범행 시각에 범인만 알리바이 없음 + 모순 2건 이상 |
| `ScriptValidator` | 대본 참조 무결성 |
| `SliceSolvability` | **필요한 사실이 어딘가에서는 들린다 · 한 방으로는 전부 못 듣는다 · 1회차로 못 모은다** |
| `MovementTrackValidator` | 말하는 사람이 그 시각 그 방에 있는가 |
| `ArtManifestValidator` | art.json의 방·인물·단서 참조 |
| `VoiceClipPlan.Check` | 음성을 넣었을 때 타이밍이 무너지지 않는가 |
| `LocaleCoverage` | 번역 진행률 (원문 복사도 따로 센다) |

---

## 엿듣기 (핵심 시스템)

### 들리는가 — `AudibilityModel`

같은 방 = `Full`(글자) · 벽 맞닿음 = `Muffled`(웅얼거림) · 그 밖 = `None`(무음).
**큰 소리(깨짐·몸싸움)는 벽을 넘어도 또렷하다** — 말은 벽을 넘으면 반드시 뭉개진다.

### 얼마나 크게 — `VoiceMix` · `SpeakerPositions`

거리는 **음량·정위·음색만** 바꾸고 **알아듣는지는 바꾸지 않는다.**

> `SliceSolvability`가 "한 방으로는 전부 못 듣는다"를 **방 기준으로 증명**해 뒀다. 거리가 같은 방 대사를
> 못 알아듣게 만들면 그 증명이 조용히 무효가 된다 — 게임은 멀쩡히 돌고 "풀 수 있다"는 보장만 사라진다.
> 그래서 같은 방은 `SameRoomFloor`(0.42) 아래로 내려가지 않는다.

* 감쇠는 **제곱**으로 떨어진다. 선형이면 멀리서도 또박또박 들려 거리감이 안 난다
* 벽 너머는 최대 0.34 + 저역 통과 900Hz→380Hz(멀수록 더 깎인다)
* 위치는 이동 트랙에서 온다. **트랙과 대본이 어긋나면 대본을 따른다**(발화의 방이 가청 판정의 근거다)
* Unity의 3D 오디오를 쓰지 않는다 — 엔진 공간화는 방과 벽을 모른다. `SpatialVoiceDirector`가
  2D AudioSource에 계산한 volume·panStereo·lowpass를 직접 얹는다

### 이동이 내는 소리 — `MovementEvents`

문소리·발소리를 **이동 트랙에서 기계적으로 뽑는다.** 손으로 적으면 트랙과 반드시 어긋난다.
경로는 `RoomLayout.FindDoorPath`가 안다 → **복도에 귀를 두면 누가 언제 지나갔는지 발소리로 셀 수 있다.**
id는 결정적이다(npc·시각·종류) — 실행마다 달라지면 저장 파일이 회차를 다시 열 때 맞지 않는다.
극적인 소리(깨짐 등)만 `events.json`에 손으로 적는다.

### 영상처럼 다룬다 — `PlaybackTransport`

앞으로만이 아니라 뒤로도 흐르고 배속이 걸린다(`PlayDirection`, 단계 0.25~4배).
`Advance`가 **부호 있는 값**을 돌려준다.

> **뒤로 감는 동안은 들은 것으로 치지 않는다.** 거꾸로 흐르는 말은 알아들을 수 없고, 이미 있는 규칙
> (건너뛴 구간은 듣지 않은 것)과 같은 이유다. 이걸 놓치면 게임은 멀쩡히 돌면서 되돌려 들을 이유만 사라진다.
> 화면에는 되감는 중에도 무엇이 울리는지 보여 준다(`Refresh(record: false)`).

처음(0)에 닿으면 `Paused`다. `Ended`로 두면 "다 봤다"가 되어 다음 재생이 처음으로 튄다.

---

## 연출

방향은 **"사건 파일"**: 어두운 저택 평면도 위에 바랜 종이 패널, 검은·붉은 잉크, 명조체(Noto Serif KR).
시안은 `docs/art-concepts/`(남은 7개 · 탈락 5개).

### 색과 크기 — `Assets/Scripts/Art/Palette.cs`

**화면에 쓰는 색을 새로 만들지 말고 여기서 가져간다.** 구현이 "가벼워 보인" 원인 넷을 값으로 고친 표다:
채도가 높았고, 글자에 4px 외곽선이 있었고, 환경광이 그림자를 씻어 냈고, 사람이 밝은 캡슐이었다.

### 후처리 — `Art/CameraGrade.cs` + `Assets/Shaders/DetectiveGrade.shader`

내장 파이프라인이라 이미지 이펙트로 직접 건다: 채도 0.72 · 대비 1.10 · 검정 들어 올림 · 타원 비네팅 ·
**정지한** 필름 입자(매 프레임 흔들면 TV 노이즈가 된다).
**셰이더가 없으면 원본을 그대로 통과시킨다** — 벗겨져도 화면이 죽지 않는다.
UI는 Screen Space Overlay라 효과를 받지 않는다(의도한 것: 세계는 가라앉고 글자는 읽혀야 한다).

### 에셋 (`art.json`)

* `art.json`이 방 바닥·초상화·단서 그림·소리·**이벤트 소리**의 Resources 경로를 정한다
* 자리는 전부 채워져 있다 — 바닥 6장(ambientCG), 초상화 5·단서 7장(Wikimedia Commons),
  BGM 3곡(Musopen 쇼팽)·환경음·효과음. 전부 CC0/퍼블릭 도메인, 출처는 `CREDITS.md`
* **파일이 없으면 코드가 만든 대체물**을 쓴다(`ProceduralTextures` · `ProceduralAudio`). BGM·환경음은 침묵
* 임포트 설정은 `ArtAssetImporter.cs`(AssetPostprocessor)가 맞춘다 — 손으로 맞추지 말 것.
  바닥은 **Wrap=Repeat · PPU=512**. 되돌리려면 `Tools/Detective/Reimport Art Assets`
* 그림·소리·폰트의 `.meta`는 첫 임포트 때 생기므로 **커밋해서 GUID를 고정**한다

---

## 조작

| 키 | 동작 |
|---|---|
| WASD / 화살표 | 이동 |
| E | 조사 · 대화 (대화 중에는 다음 줄) |
| **Space** | 회차 재생 · 정지 |
| **J / L** | 역재생 / 정주행 (영상 편집기 관습) |
| **, / .** | 5초 뒤로 / 앞으로 |
| **[ / ]** | 배속 단계 (0.25 ~ 4배) |
| R | 회차 처음부터 |
| T | 타임라인 관찰 모드 (←→ 시각, 1~7 바로 가기, Space 자동 재생). 에디터에서만 F9 = 실제 스케줄 |
| Tab (관찰 모드 안) | 청취(소나) 화면 ↔ 타임라인. 청취: WASD 귀 옮기기 |
| N | 수사 노트 (1~3 / ←→ 탭, ↑↓ 선택, PgUp/PgDn·Q/E 스크롤) |
| F | 고발 (↑↓ 항목, ←→ 선택, Enter 확정) → 결과에서 R = 재시작 |

실행 인자: `-locale ko|en`

---

## 현재 진행 상황

검증 기준선: **Unity 컴파일 `error CS` 0 · `warning CS` 0 / EditMode 320/320 / 공유 테스트 311/311 /
씬 재생성 exit 0 / 빌드 Succeeded**

- [x] **Phase 0~6 · U-0~U-2** — 맵·이동·조사·단서·수사 노트·대화·증언·목격·타임라인 복원·
      case_01·고발·채점, ms 시간 축, 엿듣기 청취점·가청 판정·소나 화면. 전부 Unity 6에서 검증됨
- [x] **회차를 영상처럼** — `PlaybackTransport` 역재생·배속, 되감는 동안 들은 것으로 치지 않음
- [x] **이동이 내는 소리** — `ScriptEvent`·`EventTimeline`·`MovementEvents`. 트랙에서 문소리·발소리를 뽑는다
- [x] **거리감 오디오** — `VoiceMix`·`SpeakerPositions`·`SpatialVoiceDirector`.
      음성 파일은 아직 없지만 **이동 소리는 지금 들린다**(SFX 팩이 있다)
- [x] **시나리오 전면 수정** — 장면을 1차 자료로 두고 동선·대본·소리를 파생(`tools/build_case02.py`).
      독백 43% → 주고받는 대화 63%, 이동 사람당 2.6 → 4.4회
- [x] **영어 로컬라이제이션** — `Localization`·`LocaleCoverage`. 대본 223/223(100%), UI 115문자열.
      `-locale en` 실행 확인 오류 0
- [x] **연출** — `Palette.cs` 단일 출처, `CameraGrade` 후처리, 색 눌러 앉힘
- [x] **포지셔닝** — `docs/POSITIONING.md`. "내러티브 미스터리 · 분위기 호러", 문구 규칙, Steam 태그
- [x] **엔진 결정** — Unity. `docs/ENGINE_COMPARISON.md`에 실측 근거
- [x] **빌드 자동화** — `DetectiveEditor.BuildScript.BuildWindows`

### 다음

- [ ] **한국어 음성 (막힘)** — Azure S0 키가 있어야 한다. 절차는 `docs/TTS_OPTIONS.md` §8,
      비교 청취는 `tools/tts_audition.py`. 본편 267발화 기준 $0.09 수준
- [ ] **U-3 목소리↔이름 배정 화면** — `VoiceAssignment`(로직)은 있고 UI가 없다
- [ ] **U-5 채점 연결 · U-6 다듬기** — 저장·설정·일시정지 포함. 종료 조건은 `DEVELOPMENT_PLAN_RELEASE.md`
- [ ] **사람이 Play로 확인** — 자동 검증은 전부 통과했지만 손으로 만져 본 적이 없다

### 알려진 미해결

* `docs/art-concepts/png/00-contact-sheet.png`가 탈락 5개를 아직 담고 있다.
  `shoot.js`가 만드는 파일이 아니고 playwright가 설치돼 있지 않다
* **비교 청취 도구가 둘이다.** `tools/tts_audition.py`(공급자 API로 음성 목록을 물어본다)와
  `tools/tts_generate.py --audition`(후보 이름을 하드코딩, Google Chirp 3 HD 목록 포함).
  다른 세션이 같은 기능을 따로 만들었고 둘 다 `25d95be`에 커밋됐다.
  합치는 방향은 "API를 진실로, 하드코딩 목록은 키 없이 훑어볼 때의 대비용"이다. 아직 안 했다
